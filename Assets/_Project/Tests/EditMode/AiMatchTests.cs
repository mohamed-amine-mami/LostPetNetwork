using LostPetNetwork.Services.Ai;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.UI;
using NUnit.Framework;

namespace LostPetNetwork.Tests
{
    public class AiMatchParseTests
    {
        [Test]
        public void MatchesAreReadClampedAndSortedBestFirst()
        {
            const string body = "{\"matches\":[{\"id\":\"a\",\"score\":40,\"reason\":\"similar colour\"},{\"id\":\"b\",\"score\":130,\"reason\":\"same collar\"},{\"id\":\"\",\"score\":90,\"reason\":\"no id\"}],\"model\":\"m\"}";
            var r = AiMatchService.Parse(200, body, false);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(2, r.Matches.Count, "entries without an id are dropped");
            Assert.AreEqual("b", r.Matches[0].ReportId);
            Assert.AreEqual(100, r.Matches[0].Score, "scores are limited to 100");
            Assert.AreEqual("a", r.Matches[1].ReportId);
            Assert.AreEqual("same collar", r.Matches[0].Reason);
        }

        [Test]
        public void NoMatchesIsASuccessfulEmptyAnswer()
        {
            var r = AiMatchService.Parse(200, "{\"matches\":[],\"model\":\"m\"}", false);
            Assert.IsTrue(r.Ok);
            Assert.IsEmpty(r.Matches);
        }

        [Test]
        public void ServerErrorCodesBecomeFriendlyMessages()
        {
            var notSet = AiMatchService.Parse(503, "{\"error\":\"not_configured\",\"message\":\"AI matching isn't set up yet.\"}", false);
            Assert.IsFalse(notSet.Ok);
            Assert.IsTrue(notSet.NotConfigured);
            Assert.AreEqual("AI matching isn't set up yet.", notSet.Message);

            var limited = AiMatchService.Parse(429, "{\"error\":\"rate_limited\"}", false);
            Assert.AreEqual("rate_limited", limited.ErrorCode);
            StringAssert.Contains("Try again", limited.Message);
        }

        [Test]
        public void AFunctionThatIsNotDeployedCountsAsNotSetUp()
        {
            var r = AiMatchService.Parse(404, "{\"message\":\"Requested function was not found\"}", false);
            Assert.IsTrue(r.NotConfigured);
        }

        [Test]
        public void NetworkProblemsAndGarbageAreHandled()
        {
            Assert.AreEqual("network", AiMatchService.Parse(0, "", true).ErrorCode);
            var garbage = AiMatchService.Parse(502, "<html>bad gateway</html>", false);
            Assert.IsFalse(garbage.Ok);
            Assert.IsNotEmpty(garbage.Message);
            Assert.IsFalse(AiMatchService.Parse(200, "not json", false).Ok);
        }

        [Test]
        public void TheFunctionAddressPointsAtTheProject() =>
            StringAssert.EndsWith("/functions/v1/match-pets", AiMatchService.FunctionUrl);

        [Test]
        public void OnlyOwnersOfActiveReportsWithAPhotoOnRealDataSeeTheHelper()
        {
            var report = new PetReport { OwnerUserId = "me", Status = ReportStatus.Active, PhotoPath = "me/x.jpg" };
            Assert.IsTrue(DetailsScreen.CanFindMatches(report, "me", demoData: false));
            Assert.IsFalse(DetailsScreen.CanFindMatches(report, "someone", false), "not the owner");
            Assert.IsFalse(DetailsScreen.CanFindMatches(report, "me", true), "demo data has no photos in the cloud");
            report.Status = ReportStatus.Reunited;
            Assert.IsFalse(DetailsScreen.CanFindMatches(report, "me", false), "already reunited");
            Assert.IsFalse(DetailsScreen.CanFindMatches(new PetReport { OwnerUserId = "me" }, "me", false), "no photo");
            Assert.IsTrue(DetailsScreen.CanFindMatches(new PetReport { OwnerUserId = "me", PhotoBytes = new byte[] { 1 } }, "me", false));
            Assert.IsFalse(DetailsScreen.CanFindMatches(null, "me", false));
        }
    }
}
