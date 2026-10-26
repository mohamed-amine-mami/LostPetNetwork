using System;
using System.Collections;
using System.Collections.Generic;
using LostPetNetwork.Services.Reports;
using LostPetNetwork.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;

namespace LostPetNetwork.AR
{
    /// <summary>
    /// The optional AR module. It decides whether this phone can run AR, starts and stops the AR session around
    /// the AR scenes (so the normal app never touches the AR camera), and shows or hides the main UI.
    /// </summary>
    public sealed class ArService : IArService
    {
        public const string PlaceScene = "ArPlace";
        public const string CompassScene = "ArCompass";

        static readonly ArService Instance = new ArService();
        static readonly List<Behaviour> HiddenMain = new List<Behaviour>();
        static Action<PetReport> _onClosed;
        static bool _open;

        /// <summary>The app cannot receive the system Back button or shakes while AR is on screen.</summary>
        public static bool IsOpen => _open;

        public bool IsSupported { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            ArLauncher.Service = Instance;
            DeviceSensorRunner.Instance.StartCoroutine(Instance.CheckSupport());
        }

        /// <summary>Asks the phone whether ARCore / ARKit can run here. Takes about a second, once per launch.</summary>
        IEnumerator CheckSupport()
        {
            yield return null;
            var settings = XRGeneralSettings.Instance;
            if (settings == null || settings.Manager == null) yield break;

            var manager = settings.Manager;
            if (Application.isEditor)
            {
                // The Editor runs AR Foundation's XR Simulation, so AR can be tried on a computer.
                IsSupported = true;
                yield break;
            }

            bool initialisedHere = false;
            if (manager.activeLoader == null)
            {
                yield return manager.InitializeLoader();
                initialisedHere = manager.activeLoader != null;
            }
            if (manager.activeLoader == null) yield break;

#pragma warning disable CS0618 // the replacement (TryStartAsync) starts a session; this only asks whether the phone can run one
            var check = ARSession.CheckAvailability();
            while (true)
            {
                bool more;
                try { more = check.MoveNext(); }
                catch (Exception e)
                {
                    Debug.LogWarning("AR availability check failed: " + e.Message);
                    break;
                }
                if (!more) break;
                yield return check.Current;
            }

            var state = ARSession.state;
            IsSupported = state == ARSessionState.Ready || state == ARSessionState.SessionInitializing ||
                          state == ARSessionState.SessionTracking || state == ARSessionState.NeedsInstall ||
                          state == ARSessionState.Installing;
#pragma warning restore CS0618
            if (initialisedHere) manager.DeinitializeLoader();
        }

        public void Open(ArMode mode, Action<PetReport> onClosed)
        {
            if (_open || !IsSupported) return;
            _open = true;
            ScreenManager.InputBlocked = true;
            _onClosed = onClosed;
            DeviceSensorRunner.Instance.StartCoroutine(OpenRoutine(mode == ArMode.Radar || mode == ArMode.Guide ? CompassScene : PlaceScene));
        }

        IEnumerator OpenRoutine(string scene)
        {
            HideMainUi();
            var manager = XRGeneralSettings.Instance?.Manager;
            if (manager == null)
            {
                Fail();
                yield break;
            }
            if (manager.activeLoader == null) yield return manager.InitializeLoader();
            if (manager.activeLoader == null)
            {
                Fail();
                yield break;
            }
            manager.StartSubsystems();
            var load = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
            yield return load;
        }

        static void Fail()
        {
            Debug.LogWarning("The AR session could not start.");
            RestoreMainUi();
            _open = false;
            ScreenManager.InputBlocked = false;
        }

        /// <summary>Called by an AR scene when the user leaves it.</summary>
        public static void Close(string scene)
        {
            if (!_open) return;
            DeviceSensorRunner.Instance.StartCoroutine(CloseRoutine(scene));
        }

        static IEnumerator CloseRoutine(string scene)
        {
            var manager = XRGeneralSettings.Instance?.Manager;
            if (manager != null && manager.activeLoader != null)
            {
                manager.StopSubsystems();
                manager.DeinitializeLoader();
            }
            var scn = SceneManager.GetSceneByName(scene);
            if (scn.isLoaded) yield return SceneManager.UnloadSceneAsync(scn);
            RestoreMainUi();
            _open = false;
            ScreenManager.InputBlocked = false;
            var callback = _onClosed;
            _onClosed = null;
            var report = ArRequest.OpenAfterClose;
            ArRequest.OpenAfterClose = null;
            callback?.Invoke(report);
        }

        static void HideMainUi()
        {
            HiddenMain.Clear();
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>())
                if (canvas.enabled && canvas.renderMode == RenderMode.ScreenSpaceOverlay) { canvas.enabled = false; HiddenMain.Add(canvas); }
            foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>())
                if (cam.enabled) { cam.enabled = false; HiddenMain.Add(cam); }
        }

        static void RestoreMainUi()
        {
            foreach (var b in HiddenMain)
                if (b != null) b.enabled = true;
            HiddenMain.Clear();
        }
    }
}
