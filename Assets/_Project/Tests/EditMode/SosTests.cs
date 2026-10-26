using System;
using System.Linq;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.Services.Sensors;
using LostPetNetwork.Services.Sos;
using NUnit.Framework;
using UnityEngine;

namespace LostPetNetwork.Tests
{
    public class ShakeDetectorTests
    {
        static readonly Vector3 Rest = new Vector3(0f, -1f, 0f);
        static readonly Vector3 Jolt = new Vector3(2.8f, -1f, 0.5f);

        static bool Run(ShakeDetector d, params (Vector3 a, float t)[] readings)
        {
            bool fired = false;
            foreach (var r in readings) fired |= d.Feed(r.a, r.t);
            return fired;
        }

        [Test]
        public void ARealShakeIsDetected()
        {
            var d = new ShakeDetector();
            Assert.IsTrue(Run(d, (Jolt, 0.0f), (-Jolt, 0.15f), (Jolt, 0.3f), (-Jolt, 0.45f)));
        }

        [Test]
        public void RestingAndWalkingDoNotTrigger()
        {
            var d = new ShakeDetector();
            for (int i = 0; i < 300; i++)
            {
                var walking = new Vector3(0.3f * Mathf.Sin(i * 0.4f), -1f + 0.4f * Mathf.Sin(i * 0.8f), 0.2f);
                Assert.IsFalse(d.Feed(walking, i * 0.02f));
            }
        }

        [Test]
        public void ASingleBumpDoesNotTrigger()
        {
            var d = new ShakeDetector();
            Assert.IsFalse(Run(d, (Jolt, 0f), (Rest, 0.1f), (Rest, 0.2f), (Rest, 3f)));
        }

        [Test]
        public void JoltsTooFarApartDoNotAddUp()
        {
            var d = new ShakeDetector();
            Assert.IsFalse(Run(d, (Jolt, 0f), (Jolt, 1f), (Jolt, 2f), (Jolt, 3f), (Jolt, 4f)));
        }

        [Test]
        public void TheSameJoltSeenInManyReadingsCountsOnce()
        {
            var d = new ShakeDetector();
            var burst = Enumerable.Range(0, 40).Select(i => (Jolt, i * 0.002f)).ToArray();
            Assert.IsFalse(Run(d, burst));
        }

        [Test]
        public void AfterAShakeItStaysQuietForAWhile()
        {
            var d = new ShakeDetector(cooldown: 4f);
            Assert.IsTrue(Run(d, (Jolt, 0f), (Jolt, 0.15f), (Jolt, 0.3f), (Jolt, 0.45f)));
            Assert.IsFalse(Run(d, (Jolt, 1f), (Jolt, 1.15f), (Jolt, 1.3f), (Jolt, 1.45f)), "inside the cooldown");
            Assert.IsTrue(Run(d, (Jolt, 6f), (Jolt, 6.15f), (Jolt, 6.3f), (Jolt, 6.45f)), "after the cooldown");
        }

        [Test]
        public void ResetForgetsEarlierJolts()
        {
            var d = new ShakeDetector();
            Run(d, (Jolt, 0f), (Jolt, 0.15f), (Jolt, 0.3f));
            d.Reset();
            Assert.IsFalse(d.Feed(Jolt, 0.45f));
        }
    }

    public class WavCodecTests
    {
        [Test]
        public void SamplesSurviveARoundTrip()
        {
            var samples = Enumerable.Range(0, 1600).Select(i => Mathf.Sin(i * 0.05f) * 0.8f).ToArray();
            var wav = WavCodec.Encode(samples, 16000);
            Assert.AreEqual(44 + 1600 * 2, wav.Length);
            Assert.IsTrue(WavCodec.TryDecode(wav, out var back, out int rate));
            Assert.AreEqual(16000, rate);
            Assert.AreEqual(1600, back.Length);
            for (int i = 0; i < samples.Length; i++) Assert.AreEqual(samples[i], back[i], 0.001f);
        }

        [Test]
        public void LoudSamplesAreClippedNotWrapped()
        {
            Assert.IsTrue(WavCodec.TryDecode(WavCodec.Encode(new[] { 5f, -5f }, 8000), out var back, out _));
            Assert.Greater(back[0], 0.99f);
            Assert.Less(back[1], -0.99f);
        }

        [Test]
        public void HeaderLooksLikeARealWavFile()
        {
            var wav = WavCodec.Encode(new float[100], 16000);
            Assert.AreEqual("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
            Assert.AreEqual("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
            Assert.AreEqual("data", System.Text.Encoding.ASCII.GetString(wav, 36, 4));
        }

        [TestCase(null)]
        [TestCase(new byte[] { 1, 2, 3 })]
        public void NonWavDataIsRejected(byte[] data) => Assert.IsFalse(WavCodec.TryDecode(data, out _, out _));

        [Test]
        public void StereoOrTruncatedFilesAreRejectedSafely()
        {
            var wav = WavCodec.Encode(new float[50], 16000);
            wav[22] = 2; // claim stereo
            Assert.IsFalse(WavCodec.TryDecode(wav, out _, out _));
            var truncated = WavCodec.Encode(new float[50], 16000).Take(60).ToArray();
            Assert.IsTrue(WavCodec.TryDecode(truncated, out var s, out _));
            Assert.AreEqual(8, s.Length, "only the samples that are present are read");
        }
    }

    public class SosMapperTests
    {
        const string RowsJson = "[{\"id\":\"s1\",\"reporter_id\":\"u1\",\"reporter_name\":\"Yassine M.\",\"latitude\":36.81,\"longitude\":10.17," +
            "\"note\":\"Injured dog\",\"audio_path\":\"u1/a.wav\",\"status\":\"responding\",\"responder_id\":\"u2\",\"created_at\":\"2026-10-26T10:00:00+00:00\"}," +
            "{\"id\":\"s2\",\"reporter_id\":\"u3\",\"reporter_name\":\"Ines\",\"latitude\":36.8,\"longitude\":10.2,\"note\":null,\"audio_path\":null," +
            "\"status\":\"open\",\"responder_id\":null,\"created_at\":\"2026-10-26T11:00:00+00:00\"}]";

        [Test]
        public void RowsBecomeAlerts()
        {
            var alerts = SosMapper.ParseRows(RowsJson).Select(SosMapper.ToAlert).ToList();
            Assert.AreEqual(2, alerts.Count);
            Assert.AreEqual(SosStatus.Responding, alerts[0].Status);
            Assert.AreEqual("u2", alerts[0].ResponderId);
            Assert.AreEqual("u1/a.wav", alerts[0].AudioPath);
            Assert.AreEqual("Help is on the way", alerts[0].StatusText);
            Assert.AreEqual(SosStatus.Open, alerts[1].Status);
            Assert.IsNull(alerts[1].Note);
            Assert.IsNull(alerts[1].ResponderId);
            Assert.IsNull(alerts[1].AudioPath);
            Assert.IsTrue(alerts[1].IsActive);
        }

        [Test]
        public void InsertBodyHasOnlyAllowedColumns()
        {
            var draft = new SosDraft { HasLocation = true, Latitude = 36.8, Longitude = 10.1, Note = " dog \"hurt\" " };
            string body = SosMapper.BuildInsertBody(draft, "Ines", "u/x.wav");
            StringAssert.Contains("\"reporter_name\":\"Ines\"", body);
            StringAssert.Contains("\"latitude\":36.8", body);
            StringAssert.Contains("\"note\":\"dog \\\"hurt\\\"\"", body);
            StringAssert.Contains("\"audio_path\":\"u/x.wav\"", body);
            foreach (var forbidden in new[] { "\"reporter_id\"", "\"status\"", "\"responder_id\"", "\"id\"" })
                StringAssert.DoesNotContain(forbidden, body);
            StringAssert.Contains("\"audio_path\":null", SosMapper.BuildInsertBody(draft, "x", null));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("nope")]
        public void UnreadableAnswersGiveNoAlerts(string json) => Assert.AreEqual(0, SosMapper.ParseRows(json).Count);

        [Test]
        public void DraftNeedsALocationAndAShortNote()
        {
            Assert.IsNotNull(new SosDraft().Validate());
            Assert.IsNull(new SosDraft { HasLocation = true }.Validate());
            Assert.IsNotNull(new SosDraft { HasLocation = true, Note = new string('x', 301) }.Validate());
        }

        [Test]
        public void TimeAgoIsReadable()
        {
            var now = DateTime.UtcNow;
            Assert.AreEqual("just now", new SosAlert { CreatedAtUtc = now }.TimeAgo(now));
            Assert.AreEqual("12 min ago", new SosAlert { CreatedAtUtc = now.AddMinutes(-12) }.TimeAgo(now));
            Assert.AreEqual("3h ago", new SosAlert { CreatedAtUtc = now.AddHours(-3) }.TimeAgo(now));
        }
    }

    public class MockSosRepositoryTests
    {
        static SosDraft Draft() => new SosDraft { HasLocation = true, Latitude = 36.8, Longitude = 10.1, Note = "Hurt cat" };

        [Test]
        public void SeedHasOneOpenAndOneRespondingAlert()
        {
            var all = new MockSosRepository(latencyMs: 0).GetActiveAsync().Result;
            Assert.AreEqual(2, all.Count);
            Assert.AreEqual(1, all.Count(a => a.Status == SosStatus.Open));
            Assert.IsTrue(all.All(a => a.IsDemo));
        }

        [Test]
        public void ACreatedAlertIsOpenAndOnlyOneAtATimeIsAllowed()
        {
            var repo = new MockSosRepository(latencyMs: 0, seed: false);
            var alert = repo.CreateAsync(Draft(), "u1", "Me").Result;
            Assert.AreEqual(SosStatus.Open, alert.Status);
            var ex = Assert.Throws<AggregateException>(() => repo.CreateAsync(Draft(), "u1", "Me").Wait());
            Assert.IsInstanceOf<RepositoryException>(ex.InnerException);
            Assert.IsNotNull(repo.CreateAsync(Draft(), "u2", "Other").Result, "another user can still raise one");
        }

        [Test]
        public void OnlyTheFirstResponderCanClaimAndResolvingRemovesTheAlert()
        {
            var repo = new MockSosRepository(latencyMs: 0, seed: false);
            var alert = repo.CreateAsync(Draft(), "u1", "Me").Result;
            Assert.AreEqual(SosStatus.Responding, repo.ClaimAsync(alert.Id, "rescuer").Result.Status);
            Assert.IsNull(repo.ClaimAsync(alert.Id, "someone-else").Result, "already taken");
            repo.ResolveAsync(alert.Id).Wait();
            Assert.AreEqual(0, repo.GetActiveAsync().Result.Count);
            Assert.AreEqual(1, repo.CreateAsync(Draft(), "u1", "Me").Result.Id.Length > 0 ? 1 : 0, "reporter may raise a new one after resolving");
        }

        [Test]
        public void VoiceNotesComeBack()
        {
            var repo = new MockSosRepository(latencyMs: 0, seed: false);
            var draft = Draft();
            draft.AudioWav = new byte[] { 1, 2, 3 };
            var alert = repo.CreateAsync(draft, "u1", "Me").Result;
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, repo.GetAudioAsync(alert).Result);
        }

        [Test]
        public void InvalidDraftsAreRejected() =>
            Assert.Throws<AggregateException>(() => new MockSosRepository(latencyMs: 0).CreateAsync(new SosDraft(), "u", "n").Wait());

        [Test]
        public void ServicesSwitchBetweenDemoAndSupabase()
        {
            var before = SosServices.Repository;
            try
            {
                SosServices.UseSupabase();
                Assert.IsFalse(SosServices.Repository.IsDemo);
                SosServices.UseDemo();
                Assert.IsTrue(SosServices.Repository.IsDemo);
            }
            finally { SosServices.Repository = before; }
        }
    }
}
