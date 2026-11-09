using System.Linq;
using LostPetNetwork.Services.Sensors;
using LostPetNetwork.Services.Vets;
using NUnit.Framework;

namespace LostPetNetwork.Tests
{
    public class VetTests
    {
        static VetClinic Clinic(string name, double lat, double lon, bool all = false, string phone = null) =>
            new VetClinic { Id = name, Name = name, Latitude = lat, Longitude = lon, Is24h = all, Phone = phone };

        // Tunis centre, Ariana, Manouba: roughly 6 km and 9 km from the first point.
        static readonly GeoFix Me = new GeoFix(36.8065, 10.1815);
        static readonly VetClinic Ariana = Clinic("Ariana", 36.8665, 10.1647);
        static readonly VetClinic Manouba = Clinic("Manouba", 36.8089, 10.0863, all: true);
        static readonly VetClinic Near = Clinic("Near", 36.8070, 10.1820);

        [Test]
        public void NearestSortsByDistanceAndLabelsIt()
        {
            var result = VetFinder.Nearest(new[] { Ariana, Manouba, Near }, Me);
            CollectionAssert.AreEqual(new[] { "Near", "Ariana", "Manouba" }, result.Select(v => v.Clinic.Name).ToArray());
            StringAssert.EndsWith("m away", result[0].DistanceLabel);
            StringAssert.EndsWith("km away", result[1].DistanceLabel);
        }

        [Test]
        public void WithoutAPositionTheListIsAlphabeticalAndHasNoDistance()
        {
            var result = VetFinder.Nearest(new[] { Manouba, Near, Ariana }, null);
            CollectionAssert.AreEqual(new[] { "Ariana", "Manouba", "Near" }, result.Select(v => v.Clinic.Name).ToArray());
            Assert.IsNull(result[0].DistanceLabel);
        }

        [Test]
        public void TheFilterKeepsOnlyClinicsOpenAllDay()
        {
            var result = VetFinder.Nearest(new[] { Ariana, Manouba, Near }, Me, only24h: true);
            CollectionAssert.AreEqual(new[] { "Manouba" }, result.Select(v => v.Clinic.Name).ToArray());
        }

        [Test]
        public void ClinicsWithoutCoordinatesAreSkippedAndTheListIsCapped()
        {
            var many = Enumerable.Range(0, 50).Select(i => Clinic("C" + i, 36.8 + i * 0.001, 10.18)).ToList();
            many.Add(Clinic("Nowhere", 0, 0));
            var result = VetFinder.Nearest(many, Me, max: 30);
            Assert.AreEqual(30, result.Count);
            Assert.IsFalse(result.Any(v => v.Clinic.Name == "Nowhere"));
        }

        [TestCase("71 123 456", null)]
        [TestCase("+216 71 123 456", "+21671123456")]
        [TestCase("0021671123456", "+21671123456")]
        [TestCase("71123456", "+21671123456")]
        [TestCase("abc", null)]
        [TestCase("", null)]
        [TestCase("123", null)]
        public void PhoneNumbersAreCleanedForTheDialer(string raw, string expected)
        {
            // 8 digits entered with spaces ("71 123 456" is only 8 digits) is a local Tunisian number too.
            if (raw == "71 123 456") expected = "+21671123456";
            Assert.AreEqual(expected, VetClinic.CleanPhone(raw));
        }

        [Test]
        public void DialAndDirectionsLinksAreBuilt()
        {
            var c = Clinic("X", 36.86652, 10.16478, phone: "+21671123456");
            Assert.AreEqual("tel:+21671123456", c.DialUrl());
            Assert.AreEqual("https://www.google.com/maps/dir/?api=1&destination=36.86652,10.16478", c.DirectionsUrl());
            Assert.IsNull(Clinic("Y", 1, 1).DialUrl());
        }

        [Test]
        public void PlaceJoinsStreetAndTownWithoutRepeatingTheTown()
        {
            Assert.AreEqual("Rue X, Ariana", new VetClinic { Address = "Rue X", City = "Ariana" }.Place);
            Assert.AreEqual("Rue X, 2080 Ariana", new VetClinic { Address = "Rue X, 2080 Ariana", City = "Ariana" }.Place);
            Assert.AreEqual("Ariana", new VetClinic { City = "Ariana" }.Place);
            Assert.AreEqual("", new VetClinic().Place);
        }

        [Test]
        public void RowsFromSupabaseAreParsedAndEmptyValuesBecomeNull()
        {
            const string json = "[{\"id\":\"1\",\"name\":\" Clinique A \",\"address\":\"\",\"city\":\"Ariana\",\"governorate\":\"Ariana\"," +
                                "\"phone\":null,\"latitude\":36.86,\"longitude\":10.16,\"opening_hours\":null,\"is_24_7\":true,\"website\":null,\"maps_url\":null}]";
            var rows = VetMapper.ParseRows(json);
            Assert.AreEqual(1, rows.Count);
            var clinic = VetMapper.ToClinic(rows[0]);
            Assert.AreEqual("Clinique A", clinic.Name);
            Assert.IsNull(clinic.Address);
            Assert.IsNull(clinic.Phone);
            Assert.IsTrue(clinic.Is24h);
            Assert.AreEqual(36.86, clinic.Latitude, 1e-9);
            Assert.AreEqual(0, VetMapper.ParseRows("not json").Count);
            Assert.AreEqual(0, VetMapper.ParseRows("").Count);
        }

        // Rows copied from the real table: Arabic text, an escaped quote inside an address and a missing town.
        const string RealRows =
            "[{\"id\":\"22c3\",\"name\":\"Animalus Clinique Vétérinaire ENNASR\",\"address\":\"15 Rue sod maarab, Ariana 2037\",\"city\":\"Ennasr\",\"governorate\":\"Ariana\",\"phone\":\"+21692520802\",\"latitude\":36.8656627,\"longitude\":10.1641856,\"opening_hours\":\"Open 24 hours (Mon-Sun)\",\"is_24_7\":true,\"website\":\"https://wfdafywe.genspark.space/\",\"maps_url\":null}," +
            "{\"id\":\"0c15\",\"name\":\"Manouba Vet (Cabinet Vétérinaire Manouba) العيادة البيطرية بمنوبة\",\"address\":\"impasse ennozha, Ibn Zaydoun, Manouba 2010\",\"city\":\"Manouba\",\"governorate\":\"Manouba\",\"phone\":\"+21622004462\",\"latitude\":36.8028527,\"longitude\":10.10213,\"opening_hours\":\"Mon-Sat 09:00-21:00\",\"is_24_7\":false,\"website\":null,\"maps_url\":null}," +
            "{\"id\":\"6dbc\",\"name\":\"Clinique Dr Hssini\",\"address\":\"cité chake 2080 près du caserne\\\"مدرسة السجون والإصلاحية، un kilomètre, Raoued 2080\",\"city\":\"Raoued\",\"governorate\":\"Ariana\",\"phone\":\"+21629732343\",\"latitude\":36.937706,\"longitude\":10.1554534,\"opening_hours\":null,\"is_24_7\":false,\"website\":null,\"maps_url\":null}," +
            "{\"id\":\"cc4e\",\"name\":\"Cabinet Vétérinaire\",\"address\":\"Route، Nationale 7, P7\",\"city\":null,\"governorate\":\"Manouba\",\"phone\":\"+21693774496\",\"latitude\":36.8362275,\"longitude\":10.0145336,\"opening_hours\":\"Open 24 hours (Mon-Sun)\",\"is_24_7\":true,\"website\":null,\"maps_url\":null}]";

        [Test]
        public void RealRowsWithArabicTextParseAndShowNoArabicLetters()
        {
            var clinics = VetMapper.ParseRows(RealRows).Select(VetMapper.ToClinic).ToList();
            Assert.AreEqual(4, clinics.Count);
            Assert.AreEqual("Manouba Vet (Cabinet Vétérinaire Manouba)", clinics[1].Name);
            Assert.AreEqual("Route, Nationale 7, P7", clinics[3].Address);
            Assert.AreEqual("Route, Nationale 7, P7", clinics[3].Place, "no town to add when the town is missing");
            StringAssert.DoesNotContain("مدرسة", clinics[2].Address);
            StringAssert.StartsWith("cité chake 2080 près du caserne", clinics[2].Address);
            foreach (var c in clinics) foreach (char ch in c.Name + c.Place) Assert.IsFalse(ch >= '؀' && ch <= 'ۿ', "Arabic letter left in: " + c.Name);
        }

        [Test]
        public void FromCentralEnnasrTheRealClinicsComeInDistanceOrder()
        {
            var clinics = VetMapper.ParseRows(RealRows).Select(VetMapper.ToClinic).ToList();
            var result = VetFinder.Nearest(clinics, new GeoFix(36.8625, 10.1650));
            CollectionAssert.AreEqual(new[] { "22c3", "6dbc", "0c15", "cc4e" }, result.Select(v => v.Clinic.Id).ToArray());
            Assert.AreEqual("Animalus Clinique Vétérinaire ENNASR", result[0].Clinic.Name);
            StringAssert.EndsWith("m away", result[0].DistanceLabel);
            Assert.AreEqual("Cabinet Vétérinaire", result[3].Clinic.Name, "the Kalâat-side Manouba clinic is the farthest, about 12 km away");
        }

        [Test]
        public void DisplayCleaningKeepsFrenchAccentsAndDropsEmptyResults()
        {
            Assert.AreEqual("Clinique Vétérinaire Hédi Nouira", VetMapper.ForDisplay(" Clinique Vétérinaire Hédi Nouira "));
            Assert.IsNull(VetMapper.ForDisplay("العيادة"));
            Assert.IsNull(VetMapper.ForDisplay("  "));
            Assert.AreEqual("Rue X, Ariana", VetMapper.ForDisplay("Rue X،  Ariana"));
        }
    }
}
