using System;
using System.Collections.Generic;
using LostPetNetwork.Services.Adoption;
using LostPetNetwork.Services.Auth;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.Services.Sos;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostPetNetwork.UI
{
    public enum ScreenId { Splash, SignIn, SignUp, ResetPassword, Home, Explore, Map, Report, Camera, Details, MyReports, Profile, AvatarCreator, PetCreator, Adoption, AdoptionDetails, AdopterProfile, AdoptionCreate, Sos, SosCreate, SosDetails }

    [Serializable]
    public class ScreenEntry
    {
        public ScreenId id;
        public GameObject root;
    }

    /// <summary>
    /// Shows one screen at a time, keeps a back stack for drill-down screens, drives the tab bar and routes
    /// between the authentication screens and the signed-in app based on the auth session.
    /// </summary>
    public class ScreenManager : MonoBehaviour
    {
        [SerializeField] List<ScreenEntry> screens = new List<ScreenEntry>();
        [SerializeField] TabBarView tabBar;

        static readonly HashSet<ScreenId> TabScreens = new HashSet<ScreenId>
        {
            ScreenId.Home, ScreenId.Explore, ScreenId.Map, ScreenId.MyReports, ScreenId.Profile, ScreenId.Adoption
        };

        static readonly HashSet<ScreenId> SignedInScreens = new HashSet<ScreenId>
        {
            ScreenId.Home, ScreenId.Explore, ScreenId.Map, ScreenId.Report, ScreenId.Camera,
            ScreenId.Details, ScreenId.MyReports, ScreenId.Profile, ScreenId.AvatarCreator, ScreenId.PetCreator,
            ScreenId.Adoption, ScreenId.AdoptionDetails, ScreenId.AdopterProfile, ScreenId.AdoptionCreate,
            ScreenId.Sos, ScreenId.SosCreate, ScreenId.SosDetails
        };

        /// <summary>True while a full-screen AR scene is open: the system Back button and shakes belong to it.</summary>
        public static bool InputBlocked { get; set; }

        readonly Stack<ScreenId> _history = new Stack<ScreenId>();

        public ScreenId Current { get; private set; } = ScreenId.Splash;
        public int HistoryDepth => _history.Count;

        async void Start()
        {
            Activate(ScreenId.Splash);
            ShakeToSos.Attach(this);
            AuthService.Instance.SessionChanged += OnSessionChanged;
            try { await AuthService.Instance.RestoreSessionAsync(); }
            catch (Exception e) { Debug.LogException(e); }

            if (this == null) return;
            // RestoreSessionAsync raises SessionChanged on success; handle the signed-out case here.
            if (Current == ScreenId.Splash)
                Show(AuthService.Instance.IsSignedIn ? ScreenId.Home : ScreenId.SignIn);
        }

        void OnDestroy()
        {
            AuthService.Instance.SessionChanged -= OnSessionChanged;
        }

        void Update()
        {
            // Android's system Back button arrives as Escape in the Input System.
            if (InputBlocked) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) HandleSystemBack();
        }

        void OnSessionChanged(AuthSession session)
        {
            if (this == null) return;
            if (session != null)
            {
                ReportServices.UseSupabase();
                AdoptionServices.UseSupabase();
                SosServices.UseSupabase();
                if (!SignedInScreens.Contains(Current)) Show(ScreenId.Home);
            }
            else
            {
                ReportServices.UseDemo();
                AdoptionServices.UseDemo();
                SosServices.UseDemo();
                AppSession.Reset();
                Show(ScreenId.SignIn);
            }
        }

        /// <summary>Navigate to a top-level screen and clear the back stack.</summary>
        public void Show(ScreenId id)
        {
            _history.Clear();
            Activate(id);
        }

        /// <summary>Navigate to a drill-down screen, remembering where we came from.</summary>
        public void Push(ScreenId id)
        {
            if (Current != ScreenId.Splash) _history.Push(Current);
            Activate(id);
        }

        /// <summary>Go back one step. Returns false when there is nothing to go back to.</summary>
        public bool Back()
        {
            if (_history.Count == 0) return false;
            Activate(_history.Pop());
            return true;
        }

        public void HandleSystemBack()
        {
            if (Back()) return;
            switch (Current)
            {
                case ScreenId.SignUp:
                case ScreenId.ResetPassword: Show(ScreenId.SignIn); break;
                case ScreenId.Explore:
                case ScreenId.MyReports:
                case ScreenId.Profile:
                case ScreenId.Map:
                case ScreenId.Report: Show(ScreenId.Home); break;
                case ScreenId.AvatarCreator: Show(ScreenId.Profile); break;
                case ScreenId.PetCreator: Show(AppSession.PetLookTarget == PetLookTarget.Adoption ? ScreenId.AdoptionCreate : ScreenId.Report); break;
                case ScreenId.AdoptionDetails:
                case ScreenId.AdopterProfile:
                case ScreenId.AdoptionCreate: Show(ScreenId.Adoption); break;
                case ScreenId.Adoption: Show(ScreenId.Home); break;
                case ScreenId.Sos: Show(ScreenId.Home); break;
                case ScreenId.SosCreate:
                case ScreenId.SosDetails: Show(ScreenId.Sos); break;
            }
        }

        void Activate(ScreenId id)
        {
            // Signed-out users can never see the app screens.
            if (SignedInScreens.Contains(id) && !AuthService.Instance.IsSignedIn) { _history.Clear(); id = ScreenId.SignIn; }
            Current = id;
            foreach (var entry in screens)
                if (entry.root != null) entry.root.SetActive(entry.id == id);
            if (tabBar != null)
            {
                bool showTabs = TabScreens.Contains(id);
                tabBar.gameObject.SetActive(showTabs);
                if (showTabs) tabBar.Highlight(id);
            }
        }

        // Convenience hooks for Button.onClick wiring.
        public void ShowSignIn() => Show(ScreenId.SignIn);
        public void ShowSignUp() => Show(ScreenId.SignUp);
        public void ShowResetPassword() => Show(ScreenId.ResetPassword);
    }
}
