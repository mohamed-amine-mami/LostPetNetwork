using System.Reflection;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.UI;
using NUnit.Framework;
using UnityEngine;

namespace LostPetNetwork.Tests
{
    /// <summary>Navigation/back-stack/auth-guard logic of the ScreenManager without any network access.</summary>
    public class NavigationTests
    {
        GameObject _go;
        ScreenManager _manager;
        string _prefsBackup;

        static void SetSession(AuthSession session)
        {
            typeof(AuthService).GetProperty("Session").GetSetMethod(true).Invoke(AuthService.Instance, new object[] { session });
        }

        [SetUp]
        public void SetUp()
        {
            _prefsBackup = PlayerPrefs.GetString("lpn.auth.session.v1", null);
            _go = new GameObject("TestScreenManager");
            _manager = _go.AddComponent<ScreenManager>();
            AppSession.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            SetSession(null);
            Object.DestroyImmediate(_go);
            if (string.IsNullOrEmpty(_prefsBackup)) PlayerPrefs.DeleteKey("lpn.auth.session.v1");
            else PlayerPrefs.SetString("lpn.auth.session.v1", _prefsBackup);
        }

        static AuthSession FakeSession() => new AuthSession
        {
            accessToken = "a", refreshToken = "r", userId = "u", email = "t@example.com",
            expiresAtUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600
        };

        [Test]
        public void Signed_out_users_cannot_open_app_screens()
        {
            SetSession(null);
            foreach (var id in new[] { ScreenId.Home, ScreenId.Explore, ScreenId.Map, ScreenId.Report, ScreenId.Camera, ScreenId.Details, ScreenId.MyReports, ScreenId.Profile })
            {
                _manager.Show(id);
                Assert.AreEqual(ScreenId.SignIn, _manager.Current, $"{id} must redirect to SignIn when signed out");
            }
            _manager.Push(ScreenId.Details);
            Assert.AreEqual(ScreenId.SignIn, _manager.Current);
        }

        [Test]
        public void Auth_screens_are_reachable_when_signed_out()
        {
            _manager.Show(ScreenId.SignUp);
            Assert.AreEqual(ScreenId.SignUp, _manager.Current);
            _manager.Show(ScreenId.ResetPassword);
            Assert.AreEqual(ScreenId.ResetPassword, _manager.Current);
        }

        [Test]
        public void Signed_in_push_and_back_stack()
        {
            SetSession(FakeSession());
            _manager.Show(ScreenId.Home);
            _manager.Push(ScreenId.Details);
            Assert.AreEqual(ScreenId.Details, _manager.Current);
            Assert.AreEqual(1, _manager.HistoryDepth);
            Assert.IsTrue(_manager.Back());
            Assert.AreEqual(ScreenId.Home, _manager.Current);
            Assert.IsFalse(_manager.Back(), "nothing left to go back to");
        }

        [Test]
        public void Show_clears_history_and_system_back_goes_home()
        {
            SetSession(FakeSession());
            _manager.Show(ScreenId.Home);
            _manager.Push(ScreenId.Report);
            _manager.Push(ScreenId.Camera);
            Assert.AreEqual(2, _manager.HistoryDepth);
            _manager.Show(ScreenId.Explore);
            Assert.AreEqual(0, _manager.HistoryDepth);
            _manager.HandleSystemBack();
            Assert.AreEqual(ScreenId.Home, _manager.Current);
        }

        [Test]
        public void System_back_from_auth_subscreens_returns_to_sign_in()
        {
            _manager.Show(ScreenId.SignUp);
            _manager.HandleSystemBack();
            Assert.AreEqual(ScreenId.SignIn, _manager.Current);
            _manager.Show(ScreenId.ResetPassword);
            _manager.HandleSystemBack();
            Assert.AreEqual(ScreenId.SignIn, _manager.Current);
        }

        [Test]
        public void Camera_back_returns_to_report_form()
        {
            SetSession(FakeSession());
            _manager.Show(ScreenId.Home);
            _manager.Push(ScreenId.Report);
            _manager.Push(ScreenId.Camera);
            _manager.Back();
            Assert.AreEqual(ScreenId.Report, _manager.Current);
        }

        [Test]
        public void Draft_survives_navigation_but_resets_with_session()
        {
            AppSession.StartDraft(Services.Reports.ReportType.Found);
            AppSession.Draft.LocationText = "Lac 2";
            Assert.AreEqual(Services.Reports.ReportType.Found, AppSession.Draft.Type);
            AppSession.Reset();
            Assert.AreEqual(Services.Reports.ReportType.Lost, AppSession.Draft.Type);
            Assert.AreEqual(string.Empty, AppSession.Draft.LocationText);
        }
    }
}
