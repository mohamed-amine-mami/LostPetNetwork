using System.Collections;
using System.Linq;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Profile;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LostPetNetwork.Tests.PlayMode
{
    /// <summary>
    /// Drives the real Main scene like a user (button clicks, typed text). Uses the DEMO report repository and a
    /// fake in-memory session, so no network calls are made. Real-Supabase checks are done separately.
    /// </summary>
    public class WorkflowTests
    {
        ScreenManager _manager;
        string _prefsBackup;

        // ---------------------------------------------------------------- helpers

        static Transform Find(string path)
        {
            var canvas = GameObject.Find("Canvas");
            Assert.IsNotNull(canvas, "Canvas missing");
            var t = canvas.transform.Find("SafeArea/" + path);
            Assert.IsNotNull(t, "UI path not found: " + path);
            return t;
        }

        static void Click(string path) => Find(path).GetComponent<Button>().onClick.Invoke();
        static void Type(string path, string text) => Find(path).GetComponent<TMP_InputField>().text = text;
        static string Text(string path) => Find(path).GetComponentInChildren<TMP_Text>(true).text;
        static bool Active(string path) => Find(path).gameObject.activeInHierarchy;
        static int Children(string path) => Find(path).childCount;

        static void SetSession(AuthSession s) =>
            typeof(AuthService).GetProperty("Session").GetSetMethod(true).Invoke(AuthService.Instance, new object[] { s });

        static void SetCachedProfile(UserProfile p) =>
            typeof(ProfileService).GetProperty("Cached").GetSetMethod(true).Invoke(ProfileService.Instance, new object[] { p });

        static AuthSession Fake() => new AuthSession
        {
            accessToken = "", refreshToken = "test-refresh", userId = "test-user", email = "tester@example.com",
            expiresAtUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600
        };

        IEnumerator Frames(int n = 3) { for (int i = 0; i < n; i++) yield return null; }

        IEnumerator Boot(bool signedIn)
        {
            PlayerPrefs.DeleteKey("lpn.auth.session.v1");
            SetSession(null);
            SetCachedProfile(null);
            ReportServices.Repository = new MockReportRepository(latencyMs: 0);
            AppSession.Reset();
            CameraScreen.SkipHardware = true;
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            _manager = Object.FindAnyObjectByType<ScreenManager>();
            Assert.IsNotNull(_manager);
            if (signedIn)
            {
                SetCachedProfile(new UserProfile { id = "test-user", display_name = "Test User", city = "Tunis", created_at = "2026-10-18T10:00:00Z" });
                SetSession(Fake());
                _manager.Show(ScreenId.Home);
            }
            yield return Frames();
        }

        [SetUp]
        public void SetUp() => _prefsBackup = PlayerPrefs.GetString("lpn.auth.session.v1", null);

        [TearDown]
        public void TearDown()
        {
            SetSession(null);
            SetCachedProfile(null);
            CameraScreen.SkipHardware = false;
            ReportServices.Repository = new MockReportRepository();
            if (string.IsNullOrEmpty(_prefsBackup)) PlayerPrefs.DeleteKey("lpn.auth.session.v1");
            else PlayerPrefs.SetString("lpn.auth.session.v1", _prefsBackup);
            PlayerPrefs.Save();
        }

        // ---------------------------------------------------------------- signed out

        [UnityTest]
        public IEnumerator SignedOut_start_shows_sign_in_without_tab_bar()
        {
            yield return Boot(false);
            Assert.AreEqual(ScreenId.SignIn, _manager.Current);
            Assert.IsTrue(Active("SignIn"));
            Assert.IsFalse(Active("TabBar"));
        }

        [UnityTest]
        public IEnumerator SignIn_validation_blocks_empty_form_without_network()
        {
            yield return Boot(false);
            Click("SignIn/Viewport/Content/SignInButton");
            yield return Frames();
            Assert.IsTrue(Active("SignIn/Viewport/Content/Email/Error"));
            Assert.IsTrue(Active("SignIn/Viewport/Content/Password/Error"));
            Assert.AreEqual(ScreenId.SignIn, _manager.Current);
            Assert.IsTrue(Find("SignIn/Viewport/Content/SignInButton").GetComponent<Button>().interactable, "not stuck in loading");
        }

        [UnityTest]
        public IEnumerator Auth_screens_link_to_each_other()
        {
            yield return Boot(false);
            Click("SignIn/Viewport/Content/Footer/Link");
            Assert.AreEqual(ScreenId.SignUp, _manager.Current);
            Click("SignUp/Viewport/Content/Footer/Link");
            Assert.AreEqual(ScreenId.SignIn, _manager.Current);
            Click("SignIn/Viewport/Content/ForgotPassword");
            Assert.AreEqual(ScreenId.ResetPassword, _manager.Current);
            Click("ResetPassword/Viewport/Content/BackToSignIn");
            Assert.AreEqual(ScreenId.SignIn, _manager.Current);
        }

        [UnityTest]
        public IEnumerator SignUp_validation_without_network()
        {
            yield return Boot(false);
            _manager.Show(ScreenId.SignUp);
            yield return Frames();
            Type("SignUp/Viewport/Content/Name/Box", "Ab");
            Type("SignUp/Viewport/Content/Email/Box", "someone@example.com");
            Type("SignUp/Viewport/Content/Password/Box", "onlyletters");
            Click("SignUp/Viewport/Content/CreateButton");
            yield return Frames();
            StringAssert.Contains("letter and one number", Text("SignUp/Viewport/Content/Password/Error"));
            Assert.AreEqual(ScreenId.SignUp, _manager.Current);
        }

        [UnityTest]
        public IEnumerator SignedOut_cannot_navigate_into_the_app()
        {
            yield return Boot(false);
            _manager.Show(ScreenId.Home);
            Assert.AreEqual(ScreenId.SignIn, _manager.Current);
            _manager.Push(ScreenId.Profile);
            Assert.AreEqual(ScreenId.SignIn, _manager.Current);
            Assert.IsFalse(Active("TabBar"));
        }

        // ---------------------------------------------------------------- signed in

        [UnityTest]
        public IEnumerator SignedIn_lands_on_home_with_tab_bar_and_demo_cards()
        {
            yield return Boot(true);
            Assert.AreEqual(ScreenId.Home, _manager.Current);
            Assert.IsTrue(Active("TabBar"));
            StringAssert.Contains("Test", Text("Home/Viewport/Content/Greeting"));
            Assert.AreEqual(2, Children("Home/Viewport/Content/List"), "two preview cards");
        }

        [UnityTest]
        public IEnumerator Tabs_navigate_between_top_level_screens()
        {
            yield return Boot(true);
            Click("TabBar/Tabs/Tab-Explore");
            Assert.AreEqual(ScreenId.Explore, _manager.Current);
            Click("TabBar/Tabs/Tab-My reports");
            Assert.AreEqual(ScreenId.MyReports, _manager.Current);
            Click("TabBar/Tabs/Tab-Profile");
            Assert.AreEqual(ScreenId.Profile, _manager.Current);
            Click("TabBar/Tabs/Tab-Home");
            Assert.AreEqual(ScreenId.Home, _manager.Current);
            Assert.AreEqual(0, _manager.HistoryDepth);
        }

        [UnityTest]
        public IEnumerator Explore_lists_filters_searches_and_shows_empty_state()
        {
            yield return Boot(true);
            Click("TabBar/Tabs/Tab-Explore");
            yield return Frames(5);
            Assert.AreEqual(3, Children("Explore/Viewport/Content/List"));
            StringAssert.Contains("3 reports", Text("Explore/Viewport/Content/Count"));
            StringAssert.Contains("Demo data", Text("Explore/Viewport/Content/Count"));

            Click("Explore/Viewport/Content/Filters/Chip-Lost");
            yield return Frames();
            Assert.AreEqual(2, Children("Explore/Viewport/Content/List"));

            Click("Explore/Viewport/Content/Filters/Chip-All");
            Type("Explore/Viewport/Content/Search", "luna");
            yield return Frames();
            Assert.AreEqual(1, Children("Explore/Viewport/Content/List"));

            Type("Explore/Viewport/Content/Search", "zzzz-nothing");
            yield return Frames();
            Assert.AreEqual(0, Children("Explore/Viewport/Content/List"));
            Assert.IsTrue(Active("Explore/Viewport/Content/EmptyState"));
        }

        [UnityTest]
        public IEnumerator Details_opens_from_explore_and_back_returns()
        {
            yield return Boot(true);
            Click("TabBar/Tabs/Tab-Explore");
            yield return Frames(5);
            Find("Explore/Viewport/Content/List").GetChild(0).GetComponent<Button>().onClick.Invoke();
            yield return Frames();
            Assert.AreEqual(ScreenId.Details, _manager.Current);
            Assert.IsFalse(Active("TabBar"), "details hides the tab bar");
            Assert.IsNotEmpty(Text("Details/Scroll/Viewport/Content/Body/TitleRow/Name"));
            Click("Details/BackButton");
            Assert.AreEqual(ScreenId.Explore, _manager.Current);
        }

        [UnityTest]
        public IEnumerator Details_of_someone_elses_report_sends_a_demo_contact_request_once()
        {
            yield return Boot(true);
            AppSession.SelectedReport = (ReportServices.Repository.GetAllAsync().Result).First(r => r.Id == "demo-1");
            _manager.Push(ScreenId.Details);
            yield return Frames();
            StringAssert.Contains("seen", Text("Details/ContactBar/ActionButton"));
            Click("Details/ContactBar/ActionButton");
            yield return Frames(4);
            StringAssert.Contains("recorded", Text("Details/Scroll/Viewport/Content/Body/Banner"));
            Click("Details/ContactBar/ActionButton");
            yield return Frames(4);
            StringAssert.Contains("already", Text("Details/Scroll/Viewport/Content/Body/Banner"));
        }

        [UnityTest]
        public IEnumerator Map_shows_pins_and_selected_card_then_closes()
        {
            yield return Boot(true);
            Click("TabBar/Tabs/Tab-Explore");
            yield return Frames(3);
            Click("Explore/Viewport/Content/Header/MapButton");
            yield return Frames(5);
            Assert.AreEqual(ScreenId.Map, _manager.Current);
            Assert.AreEqual(3, Children("Map/Pins"));
            Assert.IsTrue(Active("Map/SelectedCardHost"));
            Click("Map/Controls/SearchRow/CloseButton");
            Assert.AreEqual(ScreenId.Explore, _manager.Current);
        }

        [UnityTest]
        public IEnumerator Report_flow_validate_review_edit_post_and_mark_reunited()
        {
            yield return Boot(true);
            Click("Home/Viewport/Content/Hero/Actions/ReportLostButton");
            yield return Frames();
            Assert.AreEqual(ScreenId.Report, _manager.Current);

            // validation
            Click("Report/Form/BottomBar/ReviewButton");
            yield return Frames();
            Assert.IsTrue(Active("Report/Form/Viewport/Content/Location/Error"));
            Assert.IsFalse(Active("Report/Review"));

            // fill, review, edit, review again
            Type("Report/Form/Viewport/Content/Location/Box", "Menzah 6, Tunis");
            Click("Report/Form/BottomBar/ReviewButton");
            yield return Frames();
            Assert.IsTrue(Active("Report/Review"));
            StringAssert.Contains("Menzah 6", Text("Report/Review/Viewport/Content/SummaryCard/Summary"));
            Click("Report/Review/BottomBar/EditButton");
            yield return Frames();
            Assert.IsTrue(Active("Report/Form"));
            Click("Report/Form/BottomBar/ReviewButton");
            yield return Frames();

            // post
            Click("Report/Review/BottomBar/PostButton");
            yield return Frames(6);
            Assert.AreEqual(ScreenId.MyReports, _manager.Current);
            Assert.IsTrue(Active("MyReports/Viewport/Content/Banner"));
            Assert.AreEqual(1, Children("MyReports/Viewport/Content/List"));
            Assert.AreEqual(1, ReportServices.Repository.GetMineAsync("test-user").Result.Count);

            // own report details -> mark reunited
            Find("MyReports/Viewport/Content/List").GetChild(0).GetComponent<Button>().onClick.Invoke();
            yield return Frames();
            Assert.AreEqual("Mark as reunited", Text("Details/ContactBar/ActionButton"));
            Click("Details/ContactBar/ActionButton");
            yield return Frames(6);
            Assert.AreEqual("REUNITED", Text("Details/Scroll/Viewport/Content/Body/TitleRow/Badge"));
            Assert.IsFalse(Active("Details/ContactBar/ActionButton"));
        }

        [UnityTest]
        public IEnumerator Report_can_be_cancelled_and_draft_is_discarded()
        {
            yield return Boot(true);
            Click("Home/Viewport/Content/Hero/Actions/ReportFoundButton");
            yield return Frames();
            Assert.AreEqual(ReportType.Found, AppSession.Draft.Type);
            Type("Report/Form/Viewport/Content/Location/Box", "Lac 2");
            Click("Report/Form/Viewport/Content/Header/BackButton");
            yield return Frames();
            Assert.AreEqual(ScreenId.Home, _manager.Current);
            Assert.AreEqual(string.Empty, AppSession.Draft.LocationText);
            Assert.AreEqual(0, ReportServices.Repository.GetMineAsync("test-user").Result.Count);
        }

        [UnityTest]
        public IEnumerator Camera_capture_returns_to_report_with_photo()
        {
            yield return Boot(true);
            Click("Home/Viewport/Content/Hero/Actions/ReportLostButton");
            yield return Frames();
            Type("Report/Form/Viewport/Content/Location/Box", "Lac 2");
            Click("Report/Form/Viewport/Content/PhotoBox");
            yield return Frames();
            Assert.AreEqual(ScreenId.Camera, _manager.Current);

            // SIMULATED capture: automated tests have no camera device.
            var tex = new Texture2D(640, 480, TextureFormat.RGBA32, false);
            Object.FindAnyObjectByType<CameraScreen>().CommitCapture(tex);
            yield return Frames();
            Assert.AreEqual(ScreenId.Report, _manager.Current);
            Assert.IsTrue(AppSession.Draft.HasPhoto);
            Assert.IsTrue(Active("Report/Form/Viewport/Content/PhotoBox/Cover"));
            Assert.AreEqual("Lac 2", Find("Report/Form/Viewport/Content/Location/Box").GetComponent<TMP_InputField>().text, "typed text survives the camera trip");
        }

        [UnityTest]
        public IEnumerator Camera_gallery_button_explains_the_limitation()
        {
            yield return Boot(true);
            _manager.Push(ScreenId.Report);
            _manager.Push(ScreenId.Camera);
            yield return Frames();
            Click("Camera/Controls/GalleryButton");
            Assert.IsTrue(Active("Camera/StatusPanel"));
            StringAssert.Contains("native picker", Text("Camera/StatusPanel"));
        }

        [UnityTest]
        public IEnumerator MyReports_shows_empty_state_with_report_action()
        {
            yield return Boot(true);
            Click("TabBar/Tabs/Tab-My reports");
            yield return Frames(5);
            Assert.AreEqual(0, Children("MyReports/Viewport/Content/List"));
            Assert.IsTrue(Active("MyReports/Viewport/Content/EmptyState"));
            Click("MyReports/Viewport/Content/EmptyState/Action");
            Assert.AreEqual(ScreenId.Report, _manager.Current);
        }

        [UnityTest]
        public IEnumerator Profile_shows_real_profile_fields_and_validates_edits_without_network()
        {
            yield return Boot(true);
            Click("TabBar/Tabs/Tab-Profile");
            yield return Frames(4);
            Assert.AreEqual("Test User", Text("Profile/Viewport/Content/View/Head/Info/Name"));
            Assert.AreEqual("Tunis", Text("Profile/Viewport/Content/View/Head/Info/City"));
            Assert.AreEqual("tester@example.com", Text("Profile/Viewport/Content/View/Head/Info/Email"));

            Click("Profile/Viewport/Content/View/EditButton");
            Assert.IsTrue(Active("Profile/Viewport/Content/Edit"));
            Type("Profile/Viewport/Content/Edit/Name/Box", "A");
            Type("Profile/Viewport/Content/Edit/Phone/Box", "abc");
            Click("Profile/Viewport/Content/Edit/SaveButton");
            yield return Frames();
            Assert.IsTrue(Active("Profile/Viewport/Content/Edit/Name/Error"));
            Assert.IsTrue(Active("Profile/Viewport/Content/Edit/Phone/Error"));
            Click("Profile/Viewport/Content/Edit/CancelButton");
            Assert.IsTrue(Active("Profile/Viewport/Content/View"));
        }

        [UnityTest]
        public IEnumerator SignOut_returns_to_sign_in_and_blocks_the_app()
        {
            yield return Boot(true);
            Click("TabBar/Tabs/Tab-Profile");
            yield return Frames(3);
            Click("Profile/Viewport/Content/View/SignOutButton"); // fake session has no access token: no network call
            yield return Frames(6);
            Assert.AreEqual(ScreenId.SignIn, _manager.Current);
            Assert.IsFalse(AuthService.Instance.IsSignedIn);
            Assert.IsFalse(Active("TabBar"));
            _manager.Show(ScreenId.Profile);
            Assert.AreEqual(ScreenId.SignIn, _manager.Current);
            Assert.IsNull(ProfileService.Instance.Cached, "profile cache cleared on sign-out");
        }
    }
}
