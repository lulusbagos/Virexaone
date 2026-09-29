using System;
using UnityEngine;

namespace Virexa.FMS.Mobile
{
    /// <summary>
    /// Automatic Scene Bootstrapper for Virexa Mobile.
    /// Runs immediately when Play Mode starts in ANY scene without needing manual hierarchy setup.
    /// Instantiates all required In-Cabin Managers and the Dashboard UI.
    /// </summary>
    public static class OperatorMobileBootstrapper
    {
        // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void AutoInitializeMobileDashboard()
        {
            // Check if OperatorInCabinUI is already present
            var existingUI = UnityEngine.Object.FindAnyObjectByType<OperatorInCabinUI>();
            if (existingUI != null) return;

            // Create Master In-Cabin Cockpit GameObject
            GameObject cockpitRoot = new GameObject("--- VIREXA_MOBILE_IN_CABIN_COCKPIT ---");
            UnityEngine.Object.DontDestroyOnLoad(cockpitRoot);

            // Attach all essential mobile components
            cockpitRoot.AddComponent<OperatorDataPersistence>();
            cockpitRoot.AddComponent<OperatorAudioFeedbackManager>();
            cockpitRoot.AddComponent<OperatorTouchController>();
            cockpitRoot.AddComponent<OperatorMobileNetworkClient>();
            cockpitRoot.AddComponent<FMSFleetMessenger>();
            cockpitRoot.AddComponent<OperatorNavigationCompass>();
            
            // Attach Master UI
            var ui = cockpitRoot.AddComponent<OperatorInCabinUI>();
            ui.isLoggedIn = false; // Start on Login NIK screen

            Debug.Log("<color=#00FFA3><b>[Virexa Mobile] In-Cabin Operator Dashboard Berhasil Diinisialisasi Otomatis!</b></color>");
        }
    }
}
