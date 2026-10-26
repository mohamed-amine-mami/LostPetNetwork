using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using LostPetNetwork.Services.Ai;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Adoption;
using LostPetNetwork.Services.Avatar;
using LostPetNetwork.Services.Sensors;
using LostPetNetwork.Services.Sos;
using LostPetNetwork.Services.Pets;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.Services.Supabase;
using LostPetNetwork.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LostPetNetwork.Tests.PlayMode
{
    /// <summary>
    /// Runs the real client code against the hosted Supabase project with a test account. The credentials are
    /// never stored in the project: they are handed over in memory (Editor SessionState keys
    /// "lpn.live.email" / "lpn.live.password"), and the tests are skipped when they are missing.
    /// Everything the tests create is deleted again.
    /// </summary>
    public class LiveSupabaseTests
    {
        string _email, _password, _prefsBackup;
        string _reportId, _photoPath, _token, _listingId, _sosAudioPath;

        static IEnumerator Wait(Task task)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) throw task.Exception.GetBaseException();
        }

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            _email = UnityEditor.SessionState.GetString("lpn.live.email", string.Empty);
            _password = UnityEditor.SessionState.GetString("lpn.live.password", string.Empty);
#endif
            if (string.IsNullOrEmpty(_email) || string.IsNullOrEmpty(_password))
                Assert.Ignore("Live test credentials were not provided.");
            _prefsBackup = PlayerPrefs.GetString("lpn.auth.session.v1", string.Empty);
        }

        /// <summary>Safety net: removes anything a failed test left behind.</summary>
        [UnityTearDown]
        public IEnumerator DeleteLeftovers()
        {
            if (AuthService.Instance.IsSignedIn && !string.IsNullOrEmpty(_token) && (_reportId != null || _listingId != null || _sosAudioPath != null)) yield return Cleanup();
            _reportId = null;
            _listingId = null;
            _sosAudioPath = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (string.IsNullOrEmpty(_prefsBackup)) PlayerPrefs.DeleteKey("lpn.auth.session.v1");
            else PlayerPrefs.SetString("lpn.auth.session.v1", _prefsBackup);
            PlayerPrefs.Save();
            ReportServices.UseDemo();
            AdoptionServices.UseDemo();
            SosServices.UseDemo();
        }

        IEnumerator SignIn()
        {
            var task = AuthService.Instance.SignInAsync(_email, _password);
            yield return Wait(task);
            Assert.IsTrue(task.Result.Ok, "sign-in: " + task.Result.Error);
            _token = AuthService.Instance.Session.accessToken;
            ReportServices.UseSupabase();
        }

        IEnumerator Cleanup()
        {
            if (_reportId != null)
            {
                if (_photoPath != null)
                    yield return Wait(SupabaseHttp.SendAsync("DELETE", $"{SupabaseConfig.StorageUrl}/object/report-photos/{_photoPath}", accessToken: _token));
                yield return Wait(SupabaseHttp.SendAsync("DELETE", $"{SupabaseConfig.RestUrl}/reports?id=eq.{_reportId}", accessToken: _token));
            }
            if (_listingId != null)
                yield return Wait(SupabaseHttp.SendAsync("DELETE", $"{SupabaseConfig.RestUrl}/adoption_listings?id=eq.{_listingId}", accessToken: _token));
            yield return Wait(SupabaseHttp.SendAsync("DELETE",
                $"{SupabaseConfig.RestUrl}/adopter_profiles?user_id=eq.{AuthService.Instance.UserId}", accessToken: _token));
            if (_sosAudioPath != null)
                yield return Wait(SupabaseHttp.SendAsync("DELETE", $"{SupabaseConfig.StorageUrl}/object/sos-audio/{_sosAudioPath}", accessToken: _token));
            yield return Wait(SupabaseHttp.SendAsync("PATCH",
                $"{SupabaseConfig.RestUrl}/profiles?id=eq.{AuthService.Instance.UserId}", "{\"avatar\":null}", _token));
        }

        [UnityTest]
        public IEnumerator A_report_with_photo_and_pet_look_round_trips_through_the_real_database()
        {
            yield return SignIn();
            {
                var tex = new Texture2D(64, 64, TextureFormat.RGB24, false);
                var pixels = new Color32[64 * 64];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(200, 150, 60, 255);
                tex.SetPixels32(pixels);
                tex.Apply();
                byte[] jpeg = tex.EncodeToJPG(80);
                Object.Destroy(tex);

                var look = new PetConfig { species = 1, coat = 6, pattern = 1, size = 0, collar = 2 };
                var draft = new ReportDraft
                {
                    Type = ReportType.Found, Species = PetSpecies.Cat, PetName = "Live \"test\" cat",
                    Description = "Created by an automated test; deleted right after.", LocationText = "Test street, Tunis",
                    Latitude = 36.8065, Longitude = 10.1815, PhotoBytes = jpeg, PetAvatarJson = look.ToJson()
                };

                var create = ReportServices.Repository.CreateAsync(draft, AuthService.Instance.UserId, "Live Tester");
                yield return Wait(create);
                var created = create.Result;
                _reportId = created.Id;
                _photoPath = created.PhotoPath;
                Assert.IsFalse(created.IsDemo);
                Assert.IsFalse(created.PhotoUploadFailed);
                Assert.IsNotNull(created.PhotoPath, "photo was uploaded and linked");
                Assert.AreEqual(AuthService.Instance.UserId, created.OwnerUserId);

                var mine = ReportServices.Repository.GetMineAsync(AuthService.Instance.UserId);
                yield return Wait(mine);
                var found = mine.Result.First(r => r.Id == _reportId);
                Assert.AreEqual("Live \"test\" cat", found.PetName);
                Assert.AreEqual(ReportType.Found, found.Type);
                Assert.AreEqual(PetSpecies.Cat, found.Species);
                Assert.AreEqual(36.8065, found.Latitude, 1e-6);
                Assert.IsTrue(PetConfig.TryParse(found.PetAvatarJson, out var back));
                Assert.IsTrue(look.SameAs(back), "pet look survived the round trip");

                var all = ReportServices.Repository.GetAllAsync();
                yield return Wait(all);
                var inAll = all.Result.First(r => r.Id == _reportId);
                Assert.IsNotNull(inAll.PhotoBytes, "photo was downloaded back from private storage");
                Assert.Greater(inAll.PhotoBytes.Length, 100);
                var sprite = PhotoLibrary.FromJpeg(inAll.PhotoBytes);
                Assert.IsNotNull(sprite, "the downloaded bytes are a valid picture");

                var contact = ReportServices.Repository.RequestContactAsync(_reportId, AuthService.Instance.UserId);
                yield return Wait(contact);
                Assert.IsFalse(contact.Result, "owners cannot contact themselves");

                var reunited = ReportServices.Repository.MarkReunitedAsync(_reportId);
                yield return Wait(reunited);
                Assert.AreEqual(ReportStatus.Reunited, reunited.Result.Status);

                var single = ReportServices.Repository.GetAsync(_reportId);
                yield return Wait(single);
                Assert.AreEqual(ReportStatus.Reunited, single.Result.Status);
            }

            string id = _reportId;
            yield return Cleanup();
            _reportId = null;
            var gone = ReportServices.Repository.GetAsync(id);
            yield return Wait(gone);
            Assert.IsNull(gone.Result, "the test report was deleted");
        }

        [UnityTest]
        public IEnumerator The_avatar_is_saved_in_the_account_and_comes_back()
        {
            yield return SignIn();
            {
                var avatar = new AvatarConfig { skin = 4, hairStyle = 3, hairColor = 4, eyes = 2, outfitStyle = 2, outfitColor = 5, accessory = 3 };
                var push = AvatarSync.PushAsync(avatar);
                yield return Wait(push);
                Assert.IsTrue(push.Result.Ok, push.Result.Error);
                Assert.IsFalse(push.Result.Skipped);

                var fetch = AvatarSync.FetchAsync();
                yield return Wait(fetch);
                Assert.IsTrue(fetch.Result.Ok, fetch.Result.Error);
                Assert.IsNotNull(fetch.Result.Config);
                Assert.IsTrue(avatar.SameAs(fetch.Result.Config));
            }

            yield return Cleanup();
            var after = AvatarSync.FetchAsync();
            yield return Wait(after);
            Assert.IsTrue(after.Result.Ok);
            Assert.IsNull(after.Result.Config, "avatar cleared again");
        }

        [UnityTest]
        public IEnumerator An_adoption_listing_and_the_home_profile_round_trip_through_the_real_database()
        {
            yield return SignIn();
            AdoptionServices.UseSupabase();
            var draft = new AdoptionDraft
            {
                Name = "Live \"Rex\"", Species = 0, AgeMonths = 30, Size = 2, Energy = Level.High, GoodWithKids = true,
                GoodWithPets = false, NeedsGarden = true, LocationText = "Test street, Tunis", Latitude = 36.8, Longitude = 10.2,
                Description = "Created by an automated test; deleted right after."
            };
            var create = AdoptionServices.Repository.CreateAsync(draft, AuthService.Instance.UserId, "Live Tester");
            yield return Wait(create);
            _listingId = create.Result.Id;
            Assert.IsFalse(create.Result.IsDemo);
            Assert.AreEqual("Live \"Rex\"", create.Result.Name);
            Assert.AreEqual(2, create.Result.Size);
            Assert.AreEqual(Level.High, create.Result.Energy);
            Assert.IsFalse(create.Result.GoodWithPets);
            Assert.IsTrue(create.Result.NeedsGarden);
            Assert.IsTrue(LostPetNetwork.Services.Pets.PetConfig.TryParse(create.Result.PetAvatarJson, out var look), "default 3D look was stored");
            Assert.AreEqual(2, look.size);

            var available = AdoptionServices.Repository.GetAvailableAsync();
            yield return Wait(available);
            Assert.IsTrue(available.Result.Any(l => l.Id == _listingId));
            var mine = AdoptionServices.Repository.GetMineAsync(AuthService.Instance.UserId);
            yield return Wait(mine);
            Assert.IsTrue(mine.Result.Any(l => l.Id == _listingId));

            var ownRequest = AdoptionServices.Repository.RequestAsync(_listingId, null);
            yield return Wait(ownRequest);
            Assert.IsFalse(ownRequest.Result, "owners cannot request their own listing");

            var home = new AdopterProfile { HomeType = HomeType.House, HomeSizeM2 = 110, HasGarden = true, HasKids = true, HasOtherPets = false, Activity = Level.High };
            yield return Wait(AdoptionServices.Repository.SaveMyHomeAsync(home));
            home.HomeSizeM2 = 60; // saving again updates the same row
            yield return Wait(AdoptionServices.Repository.SaveMyHomeAsync(home));
            var back = AdoptionServices.Repository.GetMyHomeAsync();
            yield return Wait(back);
            Assert.AreEqual(60, back.Result.HomeSizeM2);
            Assert.AreEqual(HomeType.House, back.Result.HomeType);
            Assert.IsTrue(back.Result.HasKids);
            Assert.AreEqual(Level.High, back.Result.Activity);

            var adopted = AdoptionServices.Repository.MarkAdoptedAsync(_listingId);
            yield return Wait(adopted);
            Assert.AreEqual(ListingStatus.Adopted, adopted.Result.Status);
            var after = AdoptionServices.Repository.GetAvailableAsync();
            yield return Wait(after);
            Assert.IsFalse(after.Result.Any(l => l.Id == _listingId), "adopted pets leave the list");

            string id = _listingId;
            yield return Cleanup();
            _listingId = null;
            var gone = AdoptionServices.Repository.GetMineAsync(AuthService.Instance.UserId);
            yield return Wait(gone);
            Assert.IsFalse(gone.Result.Any(l => l.Id == id));
            var noHome = AdoptionServices.Repository.GetMyHomeAsync();
            yield return Wait(noHome);
            Assert.IsNull(noHome.Result);
        }

        [UnityTest]
        public IEnumerator An_SOS_alert_with_a_voice_note_round_trips_and_only_one_can_be_open()
        {
            yield return SignIn();
            SosServices.UseSupabase();
            var wav = WavCodec.Encode(Enumerable.Range(0, 3200).Select(i => Mathf.Sin(i * 0.1f) * 0.5f).ToArray(), 16000);
            var draft = new SosDraft { HasLocation = true, Latitude = 36.8065, Longitude = 10.1815, Note = "Live test alert, please ignore", AudioWav = wav };
            var create = SosServices.Repository.CreateAsync(draft, AuthService.Instance.UserId, "Live Tester");
            yield return Wait(create);
            var alert = create.Result;
            Assert.AreEqual(SosStatus.Open, alert.Status);
            Assert.IsFalse(string.IsNullOrEmpty(alert.AudioPath), "voice note was uploaded");
            _sosAudioPath = alert.AudioPath;

            var second = SosServices.Repository.CreateAsync(draft, AuthService.Instance.UserId, "Live Tester");
            while (!second.IsCompleted) yield return null;
            Assert.IsTrue(second.IsFaulted, "a second open alert is refused");
            StringAssert.Contains("already have an open alert", second.Exception.GetBaseException().Message);

            var active = SosServices.Repository.GetActiveAsync();
            yield return Wait(active);
            Assert.IsTrue(active.Result.Any(a => a.Id == alert.Id));

            var audio = SosServices.Repository.GetAudioAsync(alert);
            yield return Wait(audio);
            CollectionAssert.AreEqual(wav, audio.Result, "the voice note came back byte for byte");

            var claimed = SosServices.Repository.ClaimAsync(alert.Id, AuthService.Instance.UserId);
            yield return Wait(claimed);
            Assert.AreEqual(SosStatus.Responding, claimed.Result.Status);
            var resolved = SosServices.Repository.ResolveAsync(alert.Id);
            yield return Wait(resolved);
            Assert.AreEqual(SosStatus.Resolved, resolved.Result.Status);
            var after = SosServices.Repository.GetActiveAsync();
            yield return Wait(after);
            Assert.IsFalse(after.Result.Any(a => a.Id == alert.Id));

            var again = SosServices.Repository.CreateAsync(draft, AuthService.Instance.UserId, "Live Tester");
            yield return Wait(again);
            Assert.AreEqual(SosStatus.Open, again.Result.Status, "a new alert is allowed once the first is resolved");
            yield return Wait(SosServices.Repository.ResolveAsync(again.Result.Id));
        }

        [UnityTest]
        public IEnumerator The_AI_helper_matches_a_found_report_with_a_lost_one_through_the_deployed_function()
        {
            yield return SignIn();
            byte[] jpeg = System.IO.File.ReadAllBytes(System.IO.Path.Combine(Application.dataPath, "_Project/Art/Photos/biscuit.jpg"));
            byte[] other = System.IO.File.ReadAllBytes(System.IO.Path.Combine(Application.dataPath, "_Project/Art/Photos/max.jpg"));
            string userId = AuthService.Instance.UserId;
            string secondId = null, secondPhoto = null, thirdId = null, thirdPhoto = null;

            ReportDraft Draft(ReportType type, string name, byte[] photo) => new ReportDraft
            {
                Type = type, Species = PetSpecies.Dog, PetName = name, Description = "Automated AI test; deleted right after.",
                LocationText = "Test street, Tunis", Latitude = 36.8065, Longitude = 10.1815, PhotoBytes = photo
            };

            var lost = ReportServices.Repository.CreateAsync(Draft(ReportType.Lost, "AITEST lost", jpeg), userId, "Live Tester");
            yield return Wait(lost);
            _reportId = lost.Result.Id;
            _photoPath = lost.Result.PhotoPath;
            var found = ReportServices.Repository.CreateAsync(Draft(ReportType.Found, "AITEST found same", jpeg), userId, "Live Tester");
            yield return Wait(found);
            secondId = found.Result.Id;
            secondPhoto = found.Result.PhotoPath;
            var found2 = ReportServices.Repository.CreateAsync(Draft(ReportType.Found, "AITEST found other", other), userId, "Live Tester");
            yield return Wait(found2);
            thirdId = found2.Result.Id;
            thirdPhoto = found2.Result.PhotoPath;

            var ask = AiMatchService.FindAsync(_reportId);
            yield return Wait(ask);
            var result = ask.Result;

            // Clean up the extra reports first, then the usual safety net removes the first one.
            foreach (var (rid, path) in new[] { (secondId, secondPhoto), (thirdId, thirdPhoto) })
            {
                if (path != null) yield return Wait(SupabaseHttp.SendAsync("DELETE", $"{SupabaseConfig.StorageUrl}/object/report-photos/{path}", accessToken: _token));
                yield return Wait(SupabaseHttp.SendAsync("DELETE", $"{SupabaseConfig.RestUrl}/reports?id=eq.{rid}", accessToken: _token));
            }

            Assert.IsTrue(result.Ok, "AI call: " + result.ErrorCode + " " + result.Message);
            var same = result.Matches.FirstOrDefault(m => m.ReportId == secondId);
            var different = result.Matches.FirstOrDefault(m => m.ReportId == thirdId);
            Assert.IsNotNull(same, "the matching photo was scored");
            Assert.GreaterOrEqual(same.Score, 80, "same dog: " + same.Reason);
            Assert.IsNotNull(different, "the other dog was scored");
            Assert.Less(different.Score, 40, "different dog: " + different.Reason);
            Assert.AreEqual(secondId, result.Matches[0].ReportId, "best match comes first");
        }

        [UnityTest]
        public IEnumerator Wrong_password_is_rejected_with_a_friendly_message()
        {
            var task = AuthService.Instance.SignInAsync(_email, _password + "-wrong");
            yield return Wait(task);
            Assert.IsFalse(task.Result.Ok);
            Assert.AreEqual("Email or password is incorrect.", task.Result.Error);
        }
    }
}
