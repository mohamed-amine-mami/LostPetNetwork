using System.Collections;
using System.Linq;
using LostPetNetwork.Services.Adoption;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Pets;
using LostPetNetwork.Services.Profile;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LostPetNetwork.Tests.PlayMode
{
    /// <summary>Drives the adoption screens in the real Main scene with demo data and a fake signed-in user.</summary>
    public class AdoptionWorkflowTests
    {
        ScreenManager _manager;

        static Transform Find(string path)
        {
            var canvas = GameObject.Find("Canvas");
            Assert.IsNotNull(canvas, "Canvas missing");
            var t = canvas.transform.Find("SafeArea/" + path);
            Assert.IsNotNull(t, "UI path not found: " + path);
            return t;
        }

        static void Click(string path) => Find(path).GetComponent<Button>().onClick.Invoke();
        static bool Active(string path) => Find(path).gameObject.activeInHierarchy;
        static string Label(string path) => Find(path).GetComponentInChildren<TMPro.TMP_Text>(true).text;

        static void SetSession(AuthSession s) =>
            typeof(AuthService).GetProperty("Session").GetSetMethod(true).Invoke(AuthService.Instance, new object[] { s });

        static void SetCachedProfile(UserProfile p) =>
            typeof(ProfileService).GetProperty("Cached").GetSetMethod(true).Invoke(ProfileService.Instance, new object[] { p });

        IEnumerator Frames(int n = 3) { for (int i = 0; i < n; i++) yield return null; }

        IEnumerator Boot(string userId = "adopt-tester")
        {
            PlayerPrefs.DeleteKey("lpn.auth.session.v1");
            SetSession(null);
            ReportServices.Repository = new MockReportRepository(latencyMs: 0);
            AdoptionServices.Repository = new MockAdoptionRepository(latencyMs: 0);
            AppSession.Reset();
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            _manager = Object.FindAnyObjectByType<ScreenManager>();
            SetCachedProfile(new UserProfile { id = userId, display_name = "Adopt Tester", city = "Tunis", created_at = "2026-10-18T10:00:00Z" });
            SetSession(new AuthSession
            {
                accessToken = "", refreshToken = "r", userId = userId, email = "adopt@example.com",
                expiresAtUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600
            });
            _manager.Show(ScreenId.Home);
            yield return Frames(3);
        }

        [TearDown]
        public void TearDown()
        {
            SetSession(null);
            SetCachedProfile(null);
            ReportServices.Repository = new MockReportRepository();
            AdoptionServices.Repository = new MockAdoptionRepository();
            AppSession.Reset();
        }

        [UnityTest]
        public IEnumerator Adopt_tab_lists_the_demo_pets_without_match_scores_until_the_home_is_set()
        {
            yield return Boot();
            Click("TabBar/Tabs/Tab-Adopt");
            yield return Frames(6);
            Assert.AreEqual(ScreenId.Adoption, _manager.Current);
            Assert.IsTrue(Active("TabBar"));
            var cards = Find("Adoption/Viewport/Content/List").GetComponentsInChildren<AdoptionCardView>();
            Assert.AreEqual(3, cards.Length);
            foreach (var c in cards) Assert.IsFalse(c.transform.Find("Face/Stage/MatchPill").gameObject.activeSelf);
            StringAssert.Contains("3 pets", Label("Adoption/Viewport/Content/Count"));
        }

        [UnityTest]
        public IEnumerator Filter_chips_show_only_dogs_or_cats()
        {
            yield return Boot();
            _manager.Show(ScreenId.Adoption);
            yield return Frames(6);
            Click("Adoption/Viewport/Content/Filters/Chip-Cats");
            yield return Frames(3);
            var cards = Find("Adoption/Viewport/Content/List").GetComponentsInChildren<AdoptionCardView>();
            Assert.AreEqual(1, cards.Length);
            Assert.IsTrue(cards[0].Listing.IsCat);
            Click("Adoption/Viewport/Content/Filters/Chip-Dogs");
            yield return Frames(3);
            Assert.AreEqual(2, Find("Adoption/Viewport/Content/List").GetComponentsInChildren<AdoptionCardView>().Length);
        }

        [UnityTest]
        public IEnumerator Saving_my_home_shows_match_scores_and_orders_by_best_fit()
        {
            yield return Boot();
            _manager.Show(ScreenId.Adoption);
            yield return Frames(4);
            Click("Adoption/Viewport/Content/Actions/HomeButton");
            yield return Frames(3);
            Assert.AreEqual(ScreenId.AdopterProfile, _manager.Current);

            // A small calm flat without children or pets: the calm cat should fit best.
            Click("AdopterProfile/Viewport/Content/Size/Chip-< 40 m²");
            Click("AdopterProfile/Viewport/Content/Activity/Chip-Calm");
            Click("AdopterProfile/Viewport/Content/Kids/Chip-No");
            Click("AdopterProfile/Viewport/Content/Pets/Chip-No");
            Click("AdopterProfile/Viewport/Content/Garden/Chip-No");
            Click("AdopterProfile/Viewport/Content/SaveButton");
            yield return Frames(6);

            Assert.AreEqual(ScreenId.Adoption, _manager.Current);
            Assert.IsNotNull(AppSession.MyHome);
            Assert.AreEqual(30, AppSession.MyHome.HomeSizeM2);
            Assert.AreEqual(Level.Low, AppSession.MyHome.Activity);
            var cards = Find("Adoption/Viewport/Content/List").GetComponentsInChildren<AdoptionCardView>();
            Assert.AreEqual("Misty", cards[0].Listing.Name, "the calm cat is the best fit for a small calm flat");
            Assert.IsTrue(cards[0].transform.Find("Face/Stage/MatchPill").gameObject.activeSelf);
            StringAssert.EndsWith("% match", Label("Adoption/Viewport/Content/List/" + cards[0].name + "/Face/Stage/MatchPill"));
        }

        [UnityTest]
        public IEnumerator Details_show_the_3D_pet_and_the_compatibility_breakdown()
        {
            yield return Boot();
            AppSession.MyHome = new AdopterProfile { HomeSizeM2 = 110, HasGarden = true, Activity = Level.High };
            AppSession.MyHomeLoaded = true;
            AppSession.SelectedListing = AdoptionServices.Repository.GetAvailableAsync().Result.First(l => l.Name == "Rocky");
            _manager.Show(ScreenId.AdoptionDetails);
            yield return Frames(4);

            Assert.AreEqual("Rocky", Label("AdoptionDetails/Scroll/Viewport/Content/Name"));
            Assert.IsTrue(Active("AdoptionDetails/Scroll/Viewport/Content/MatchCard"));
            Assert.IsFalse(Active("AdoptionDetails/Scroll/Viewport/Content/HomePrompt"));
            StringAssert.EndsWith("%", Label("AdoptionDetails/Scroll/Viewport/Content/MatchCard/ScoreRow/Score"));
            Assert.AreEqual("Space", Label("AdoptionDetails/Scroll/Viewport/Content/MatchCard/Factor-0/Top/Title"));
            StringAssert.Contains("/", Label("AdoptionDetails/Scroll/Viewport/Content/MatchCard/Factor-1/Top/Points"));
            Assert.IsNotEmpty(Label("AdoptionDetails/Scroll/Viewport/Content/MatchCard/Factor-2/Note"));

            var preview = Find("AdoptionDetails/Scroll/Viewport/Content/Stage/Preview").GetComponent<Preview3D>();
            Assert.IsNotNull(preview.Content);
            var shot = preview.Capture();
            int opaque = shot.GetPixels32().Count(p => p.a > 200);
            Object.Destroy(shot);
            Assert.Greater(opaque, shot.width * shot.height / 30, "the pet is drawn");
        }

        [UnityTest]
        public IEnumerator Details_without_a_home_ask_to_set_it_up()
        {
            yield return Boot();
            AppSession.SelectedListing = AdoptionServices.Repository.GetAvailableAsync().Result[0];
            _manager.Show(ScreenId.AdoptionDetails);
            yield return Frames(3);
            Assert.IsTrue(Active("AdoptionDetails/Scroll/Viewport/Content/HomePrompt"));
            Assert.IsFalse(Active("AdoptionDetails/Scroll/Viewport/Content/MatchCard"));
            Click("AdoptionDetails/Scroll/Viewport/Content/HomePrompt/SetupHomeButton");
            yield return Frames(3);
            Assert.AreEqual(ScreenId.AdopterProfile, _manager.Current);
        }

        [UnityTest]
        public IEnumerator Requesting_to_adopt_works_once_and_the_AR_button_stays_hidden_without_AR()
        {
            yield return Boot();
            AppSession.SelectedListing = AdoptionServices.Repository.GetAvailableAsync().Result[0];
            _manager.Show(ScreenId.AdoptionDetails);
            yield return Frames(3);
            Assert.IsFalse(Active("AdoptionDetails/ActionBar/ArButton") && !ArLauncher.CanTryAtHome);
            StringAssert.StartsWith("Request to adopt", Label("AdoptionDetails/ActionBar/RequestButton"));
            Click("AdoptionDetails/ActionBar/RequestButton");
            yield return Frames(4);
            StringAssert.Contains("recorded", Label("AdoptionDetails/Scroll/Viewport/Content/Banner"));
            Click("AdoptionDetails/ActionBar/RequestButton");
            yield return Frames(4);
            StringAssert.Contains("already", Label("AdoptionDetails/Scroll/Viewport/Content/Banner"));
        }

        [UnityTest]
        public IEnumerator Listing_a_pet_validates_then_adds_it_and_the_owner_can_mark_it_adopted()
        {
            yield return Boot("lister");
            _manager.Show(ScreenId.Adoption);
            yield return Frames(4);
            Click("Adoption/Viewport/Content/Actions/ListButton");
            yield return Frames(3);
            Assert.AreEqual(ScreenId.AdoptionCreate, _manager.Current);

            Click("AdoptionCreate/Viewport/Content/SubmitButton");
            yield return Frames(3);
            Assert.IsTrue(Active("AdoptionCreate/Viewport/Content/Name/Error"));
            Assert.IsTrue(Active("AdoptionCreate/Viewport/Content/Location/Error"));
            Assert.AreEqual(ScreenId.AdoptionCreate, _manager.Current);

            Find("AdoptionCreate/Viewport/Content/Name/Box").GetComponent<TMPro.TMP_InputField>().text = "Bella";
            Find("AdoptionCreate/Viewport/Content/Location/Box").GetComponent<TMPro.TMP_InputField>().text = "Sousse centre";
            Click("AdoptionCreate/Viewport/Content/Species/Chip-Cat");
            Click("AdoptionCreate/Viewport/Content/Size/Chip-Small");
            Click("AdoptionCreate/Viewport/Content/Submit".Replace("Submit", "SubmitButton"));
            yield return Frames(6);

            Assert.AreEqual(ScreenId.Adoption, _manager.Current);
            var mine = AdoptionServices.Repository.GetMineAsync("lister").Result;
            Assert.AreEqual(1, mine.Count);
            Assert.AreEqual("Bella", mine[0].Name);
            Assert.IsTrue(mine[0].IsCat);
            Assert.AreEqual(0, mine[0].Size);
            Assert.IsTrue(PetConfig.TryParse(mine[0].PetAvatarJson, out var look), "a default 3D look was created");
            Assert.AreEqual(1, look.species);

            AppSession.SelectedListing = mine[0];
            _manager.Show(ScreenId.AdoptionDetails);
            yield return Frames(3);
            Assert.AreEqual("Mark as adopted", Label("AdoptionDetails/ActionBar/RequestButton"));
            Click("AdoptionDetails/ActionBar/RequestButton");
            yield return Frames(4);
            Assert.AreEqual(ListingStatus.Adopted, AdoptionServices.Repository.GetMineAsync("lister").Result[0].Status);
        }

        [UnityTest]
        public IEnumerator The_3D_look_for_a_listing_is_kept_in_the_adoption_draft_not_the_report_draft()
        {
            yield return Boot("lister");
            AppSession.AdoptionDraft = new AdoptionDraft { Species = 1 };
            _manager.Show(ScreenId.AdoptionCreate);
            yield return Frames(3);
            Click("AdoptionCreate/Viewport/Content/Pet3DBox");
            yield return Frames(4);
            Assert.AreEqual(ScreenId.PetCreator, _manager.Current);
            var creator = Object.FindAnyObjectByType<PetCreatorScreen>();
            Assert.IsTrue(creator.Config.IsCat, "starts from the listing's species");
            Click("PetCreator/Viewport/Content/Categories/Chip-Coat");
            yield return Frames(2);
            Click("PetCreator/Viewport/Content/Panel-Coat/Option-PetCoat/Swatches/Swatch-5");
            Click("PetCreator/Viewport/Content/SaveButton");
            yield return Frames(4);
            Assert.AreEqual(ScreenId.AdoptionCreate, _manager.Current);
            Assert.IsTrue(PetConfig.TryParse(AppSession.AdoptionDraft.PetAvatarJson, out var look));
            Assert.AreEqual(5, look.coat);
            Assert.IsNull(AppSession.Draft.PetAvatarJson);
        }
    }
}
