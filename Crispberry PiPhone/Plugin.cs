using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Crispberry_PiPhone
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(ClosetCatalog.MoreCustomizationsGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(ClosetSkinSliders.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInProcess("PEAK.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "tony4twentys.Crispberry_PiPhone";
        public const string PluginName = "Crispberry PiPhone";
        public const string PluginVersion = "0.17.81";
        public const string OsName = "Crispberry OS";

        public static string OsVersionLabel
        {
            get { return OsName + " " + PluginVersion; }
        }

        /// <summary>Photon RaiseEvent byte for texts, calls, and voice payloads.</summary>
        internal const byte PhotonEventCode = 185;
        internal const string EventMagic = PluginGuid;

        public static Plugin Instance;

        public ConfigEntry<KeyCode> MenuKey;
        public ConfigEntry<KeyCode> MenuModifier;
        public ConfigEntry<bool> ShowPauseMenuButton;
        public ConfigEntry<KeyCode> CameraCursorKey;
        public ConfigEntry<KeyCode> CameraFlipKey;
        public ConfigEntry<KeyCode> CameraRotateKey;
        public ConfigEntry<KeyCode> CameraShutterKey;
        public ConfigEntry<KeyCode> AnswerKey;
        public ConfigEntry<KeyCode> DeclineKey;
        public ConfigEntry<KeyCode> LandscapeKey;
        public ConfigEntry<string> TenorApiKey;

        internal static bool CapturingHotkey;
        internal static int CaptureEndedFrame = -1;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            MenuKey = Config.Bind("Controls", "MenuKey", KeyCode.P, "Opens the phone. Used with MenuModifier.");
            MenuModifier = Config.Bind("Controls", "MenuModifier", KeyCode.LeftAlt, "Hold this with MenuKey. Set to None for the key alone.");
            ShowPauseMenuButton = Config.Bind("Controls", "ShowPauseMenuButton", true, "Add a PiPhone button to the pause menu.");
            CameraCursorKey = Config.Bind("Camera", "CursorKey", KeyCode.LeftAlt, "While Camera is open: show or hide the cursor. Only this app uses it.");
            CameraFlipKey = Config.Bind("Camera", "FlipKey", KeyCode.F, "While Camera is open: flip front/back. Does not stop a recording.");
            CameraRotateKey = Config.Bind("Camera", "RotateKey", KeyCode.R, "While Camera is open: tall/wide. Does not stop a recording.");
            CameraShutterKey = Config.Bind("Camera", "ShutterKey", KeyCode.V, "While Camera is open: photo snaps, video starts or stops.");
            AnswerKey = Config.Bind("Alerts", "AnswerKey", KeyCode.F1, "Answer an incoming call, or open the latest text in Messages.");
            DeclineKey = Config.Bind("Alerts", "DeclineKey", KeyCode.F2, "Decline an incoming call, or dismiss a text banner.");
                LandscapeKey = Config.Bind("Controls", "LandscapeKey", KeyCode.F3, "While the phone is open: switch portrait and landscape (same phone shape).");
            TenorApiKey = Config.Bind("Network", "TenorApiKey", "", "Optional Google Tenor API key for GIF search. Leave blank to use the built-in Tenor search page.");

            PhoneStore.Load();
            PhoneTheme.Load();
            PhoneLang.Ensure();
            PhoneTones.Load();
            PhoneContacts.Ensure();
            VoiceIo.Ensure();
            MusicPlayer.Ensure();
            PhoneNet.Ensure();
            PhoneCast.Ensure();
            CallService.Ensure();
            AlertHud.Ensure();
            PhoneMenu.EnsureCreated();
            BuiltinApps.Register();
            PhoneKeys.RegisterBuiltins();
            PhoneKeys.Load();

            _harmony = new Harmony(PluginGuid);
            try
            {
                _harmony.PatchAll(typeof(Plugin).Assembly);
            }
            catch (Exception ex)
            {
                LogError("Harmony PatchAll failed: " + ex.Message);
            }
            SceneManager.sceneLoaded += OnSceneLoaded;
            LogInfo(PluginName + " v" + PluginVersion + " loaded. Open with " + FormatHotkey() + ".");
        }

        private void Update()
        {
            PhoneMenu.TickUseLock();
            PiPhoneApi.TickApps();
            if (PhoneKeys.TickCapture())
                return;
            PhoneKeys.FirePressed();
            if (CameraApp.IsOpen)
                CameraApp.TickHotkeys();
        }

        internal static string FormatCameraKeys()
        {
            return PhoneKeys.Format(PhoneKeys.CamCursor) + " cursor · "
                + PhoneKeys.Format(PhoneKeys.CamFlip) + " flip · "
                + PhoneKeys.Format(PhoneKeys.CamRotate) + " rotate · "
                + PhoneKeys.Format(PhoneKeys.CamShutter) + " shutter · wheel zoom";
        }

        internal static string FormatLandscapeKey()
        {
            return PhoneKeys.Format(PhoneKeys.Landscape);
        }

        internal static string FormatAlertKeys(bool text = false)
        {
            string yes = PhoneKeys.Format(PhoneKeys.Answer);
            string no = PhoneKeys.Format(PhoneKeys.Decline);
            return text ? (yes + " open  ·  " + no + " dismiss") : (yes + " answer  ·  " + no + " decline");
        }

        public static string FormatHotkey()
        {
            return PhoneKeys.Format(PhoneKeys.Open);
        }

        internal static string Pretty(KeyCode key)
        {
            return PhoneKeys.Pretty(key);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            try { _harmony?.UnpatchSelf(); } catch { }
            if (Instance == this)
                Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            PhoneUi.InvalidateFont();
            PhoneMenu.OnAfterSceneLoad();
        }

        public static bool GetShowPauseMenuButton()
        {
            return Instance == null || Instance.ShowPauseMenuButton == null || Instance.ShowPauseMenuButton.Value;
        }

        public static void SetShowPauseMenuButton(bool show)
        {
            if (Instance != null && Instance.ShowPauseMenuButton != null)
                Instance.ShowPauseMenuButton.Value = show;
        }

        public static void LogInfo(string message)
        {
            if (!PhoneTheme.WriteLogs)
                return;
            if (Instance != null)
                Instance.Logger.LogInfo(message);
            else
                Debug.Log("[PiPhone] " + message);
        }

        public static void LogError(string message)
        {
            if (Instance != null)
                Instance.Logger.LogError(message);
            else
                Debug.LogError("[PiPhone] " + message);
        }
    }
}
