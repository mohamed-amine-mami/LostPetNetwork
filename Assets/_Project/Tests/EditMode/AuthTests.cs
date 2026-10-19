using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Profile;
using LostPetNetwork.Services.Supabase;
using NUnit.Framework;
using UnityEngine;

namespace LostPetNetwork.Tests
{
    public class AuthValidatorTests
    {
        [TestCase("a@b.co")]
        [TestCase("first.last+tag@example.com")]
        [TestCase("  padded@example.com  ")]
        public void Email_valid(string email) => Assert.IsNull(AuthValidator.ValidateEmail(email));

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("no-at-sign")]
        [TestCase("a@b")]
        [TestCase("a b@c.com")]
        [TestCase("@nope.com")]
        public void Email_invalid(string email) => Assert.IsNotNull(AuthValidator.ValidateEmail(email));

        [Test]
        public void Email_null_is_invalid() => Assert.IsNotNull(AuthValidator.ValidateEmail(null));

        [TestCase("abcdefg1")]
        [TestCase("Passw0rd!")]
        [TestCase("1234567a")]
        public void NewPassword_valid(string pw) => Assert.IsNull(AuthValidator.ValidateNewPassword(pw));

        [TestCase("")]
        [TestCase("short1")]
        [TestCase("onlyletters")]
        [TestCase("12345678")]
        public void NewPassword_invalid(string pw) => Assert.IsNotNull(AuthValidator.ValidateNewPassword(pw));

        [Test]
        public void SignInPassword_only_requires_something()
        {
            Assert.IsNull(AuthValidator.ValidatePasswordForSignIn("x"));
            Assert.IsNotNull(AuthValidator.ValidatePasswordForSignIn(""));
        }

        [TestCase("Al", true)]
        [TestCase("A", false)]
        [TestCase("  ", false)]
        public void DisplayName(string name, bool valid) =>
            Assert.AreEqual(valid, AuthValidator.ValidateDisplayName(name) == null);

        [Test]
        public void DisplayName_too_long() => Assert.IsNotNull(AuthValidator.ValidateDisplayName(new string('x', 51)));

        [TestCase("123456", true)]
        [TestCase("12345678", true)]
        [TestCase("12345", false)]
        [TestCase("12a456", false)]
        [TestCase("", false)]
        public void ResetCode(string code, bool valid) =>
            Assert.AreEqual(valid, AuthValidator.ValidateResetCode(code) == null);
    }

    public class ProfileValidatorTests
    {
        [Test]
        public void DisplayName_rules()
        {
            Assert.IsNull(ProfileValidator.ValidateDisplayName("Mo"));
            Assert.IsNotNull(ProfileValidator.ValidateDisplayName("M"));
            Assert.IsNotNull(ProfileValidator.ValidateDisplayName(new string('a', 51)));
        }

        [Test]
        public void City_is_optional_but_limited()
        {
            Assert.IsNull(ProfileValidator.ValidateCity(""));
            Assert.IsNull(ProfileValidator.ValidateCity("Tunis"));
            Assert.IsNotNull(ProfileValidator.ValidateCity(new string('c', 81)));
        }

        [TestCase("", true)]
        [TestCase("+216 20 123 456", true)]
        [TestCase("(555) 123-4567", true)]
        [TestCase("abc", false)]
        [TestCase("12", false)]
        public void Phone(string phone, bool valid) =>
            Assert.AreEqual(valid, ProfileValidator.ValidatePhone(phone) == null);
    }

    public class SessionTests
    {
        string _backup;

        [SetUp]
        public void SetUp() => _backup = PlayerPrefs.GetString("lpn.auth.session.v1", null);

        [TearDown]
        public void TearDown()
        {
            if (string.IsNullOrEmpty(_backup)) PlayerPrefs.DeleteKey("lpn.auth.session.v1");
            else PlayerPrefs.SetString("lpn.auth.session.v1", _backup);
            PlayerPrefs.Save();
        }

        [Test]
        public void Store_round_trips_and_clears()
        {
            var s = new AuthSession { accessToken = "a", refreshToken = "r", userId = "u", email = "e@x.com", expiresAtUnix = 123 };
            SessionStore.Save(s);
            var loaded = SessionStore.Load();
            Assert.IsNotNull(loaded);
            Assert.AreEqual("u", loaded.userId);
            Assert.AreEqual("r", loaded.refreshToken);
            SessionStore.Clear();
            Assert.IsNull(SessionStore.Load());
        }

        [Test]
        public void Corrupt_or_incomplete_data_loads_as_null()
        {
            PlayerPrefs.SetString("lpn.auth.session.v1", "{not json");
            Assert.IsNull(SessionStore.Load());
            PlayerPrefs.SetString("lpn.auth.session.v1", "{\"accessToken\":\"a\"}");
            Assert.IsNull(SessionStore.Load());
        }

        [Test]
        public void Expiry_window()
        {
            var soon = new AuthSession { expiresAtUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 30 };
            var later = new AuthSession { expiresAtUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600 };
            Assert.IsTrue(soon.IsExpiringWithin(60));
            Assert.IsFalse(later.IsExpiringWithin(60));
        }

        [Test]
        public void Stored_session_never_contains_a_password_field()
        {
            var json = JsonUtility.ToJson(new AuthSession { refreshToken = "r", userId = "u" });
            StringAssert.DoesNotContain("password", json.ToLowerInvariant());
        }
    }

    public class ErrorMappingTests
    {
        static HttpResponse Resp(long status, string body, bool net = false) => new HttpResponse(status, body, net);

        [Test]
        public void Network_error_has_friendly_text() =>
            StringAssert.Contains("internet", SupabaseHttp.FriendlyError(Resp(0, "", true)));

        [TestCase("invalid_credentials", "incorrect")]
        [TestCase("email_not_confirmed", "confirm your email")]
        [TestCase("user_already_exists", "already exists")]
        [TestCase("weak_password", "too weak")]
        [TestCase("over_email_send_rate_limit", "Too many emails")]
        [TestCase("otp_expired", "expired")]
        [TestCase("session_not_found", "session has expired")]
        [TestCase("email_address_invalid", "valid email")]
        public void Known_error_codes(string code, string expectedFragment)
        {
            string body = "{\"code\":400,\"error_code\":\"" + code + "\",\"msg\":\"x\"}";
            StringAssert.Contains(expectedFragment, SupabaseHttp.FriendlyError(Resp(400, body)));
        }

        [Test]
        public void Status_fallbacks()
        {
            StringAssert.Contains("Too many", SupabaseHttp.FriendlyError(Resp(429, "{}")));
            StringAssert.Contains("server", SupabaseHttp.FriendlyError(Resp(503, "")));
            StringAssert.Contains("session", SupabaseHttp.FriendlyError(Resp(401, "")));
            StringAssert.Contains("Something went wrong", SupabaseHttp.FriendlyError(Resp(418, "garbage")));
        }

        [Test]
        public void Config_has_no_secret_key()
        {
            StringAssert.StartsWith("sb_publishable_", SupabaseConfig.PublishableKey);
            StringAssert.DoesNotContain("service_role", SupabaseConfig.PublishableKey);
            StringAssert.StartsWith("https://", SupabaseConfig.Url);
        }
    }
}
