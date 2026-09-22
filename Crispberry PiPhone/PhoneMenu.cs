using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UObject = UnityEngine.Object;

namespace Crispberry_PiPhone
{
    internal sealed class PhoneMenu : MonoBehaviour, IPiPhoneHost
    {
        private const int SortOrder = 28000;
        private const string PauseChipName = "PiP_PausePhone";
        private const float PhoneWidth = 420f;
        private const float PhoneHeight = 860f;

        private static PhoneMenu _instance;
        private static readonly List<PiPhoneNavButton> _navExtras = new List<PiPhoneNavButton>();
        private static int _toggleFrame = -1;

        private bool _visible;
        private bool _built;
        private string _openAppId;
        private readonly List<string> _recents = new List<string>(8);

        private RectTransform _bezel;
        private GameObject _homeRoot;
        private GameObject _appRoot;
        private GameObject _recentsRoot;
        private RectTransform _appContent;
        private RectTransform _homeGrid;
        private RectTransform _dock;
        private RectTransform _drawerGrid;
        private TextMeshProUGUI _statusTime;
        private TextMeshProUGUI _statusRight;
        private TextMeshProUGUI _appTitle;
        private TextMeshProUGUI _toast;
        private float _toastUntil;
        private Image _bezelImg;
        private Image _wallpaperImg;
        private Image _navImg;
        private bool _navStacked;
        private bool _navDocked;
        private float _navBarHeight = 44f;
        private float _navBarWidth = 56f;
        private Image _statusImg;
        private readonly List<Image> _navFills = new List<Image>();
        private readonly List<Image> _navGlyphs = new List<Image>();
        private Button _navRotate;
        private Image _appBg;
        private Image _headerImg;
        private Image _dockImg;
        private Image _veil;
        private GameObject _toastRoot;
        private Sprite _customWallpaper;
        private Sprite[] _wpSprites;
        private Coroutine _wpPlay;
        private PhoneGif.Clip _wpClip;
        private string _wpApplied = string.Empty;
        private float _nextStatus;
        private GameObject _shadeRoot;
        private RectTransform _statusBar;
        private RectTransform _pagesRt;
        private GameObject _callLayer;
        private GameObject _callBar;
        private TextMeshProUGUI _callBarLabel;
        private bool _callScreen = true;
        private bool _emojiWarmStarted;
        private RectTransform _volUpRt;
        private RectTransform _volDnRt;
        private RectTransform _ringerRt;
        private RectTransform _punchRt;
        private TextMeshProUGUI _callTitle;
        private TextMeshProUGUI _callSub;
        private GameObject _incomingBtns;
        private GameObject _activeBtns;
        private Button _muteBtn;
        private GameObject _vmBtn;
        private RawImage _callRemote;
        private RawImage _callLocal;
        private CursorLockMode _prevLock;
        private bool _prevCursorVisible;
        private Vector2 _frozenLook;
        private bool _hasFrozenLook;
        private GameObject _iconMenu;
        private bool _landscape;
        private bool _fullscreen;
        private bool _immersive;
        private bool _userPinned;
        private bool _userLandscape;
        private bool _raisingOrient;
        private bool _lastLand;
        private bool _lastFull;
        private bool _lastImm;
        private bool _navPeek;
        private bool _wallpaperForcedPause;
        private RectTransform _navHandle;
        private TextMeshProUGUI _navHandleLabel;
        private GameObject _callFace;
        private BezelDrag _bezelDrag;
        private bool _shadeReady;
        private CanvasGroup _canvasGroup;
        private PiPhonePlayThrough _playMode;

        public static bool IsOpen
        {
            get { return _instance != null && _instance._visible && _instance.gameObject.activeInHierarchy; }
        }

        public static IPiPhoneHost InstanceHost
        {
            get { return _instance; }
        }

        public static string CurrentAppId
        {
            get { return _instance != null ? _instance._openAppId : null; }
        }

        public static bool IsLandscape
        {
            get { return _instance != null && _instance._landscape; }
        }

        public static bool IsFullscreen
        {
            get { return _instance != null && _instance._fullscreen; }
        }

        internal static float InnerWidth
        {
            get
            {
                if (_instance != null && _instance._appContent != null && _instance._appContent.rect.width > 32f)
                    return _instance._appContent.rect.width;
                return IsLandscape ? PhoneHeight - 52f : PhoneWidth - 52f;
            }
        }

        internal static bool WantsPlayThrough
        {
            get { return EffectivePlayMode() != PiPhonePlayThrough.Off; }
        }

        internal static bool WantsPlayCursor
        {
            get
            {
                PiPhonePlayThrough mode = EffectivePlayMode();
                return mode == PiPhonePlayThrough.Off || mode == PiPhonePlayThrough.WalkAndCursor;
            }
        }

        internal static PiPhonePlayThrough EffectivePlayMode()
        {
            if (!IsOpen || !PlayThroughAllowed())
                return PiPhonePlayThrough.Off;
            PiPhonePlayThrough mode = _instance != null ? _instance._playMode : PiPhonePlayThrough.Off;
            if (mode == PiPhonePlayThrough.Off && (CallService.IsOnCall || CallVideo.Chasing))
                return PiPhonePlayThrough.Walk;
            return mode;
        }

        internal static bool PlayThroughAllowed()
        {
            string id = CurrentAppId;
            if (string.IsNullOrEmpty(id))
                return true;
            PiPhoneApp app;
            if (!PiPhoneApi.TryGetApp(id, out app) || app == null)
                return true;
            return app.AllowPlayThrough;
        }

        public static bool IsPlayThrough
        {
            get { return WantsPlayThrough; }
        }

        public static PiPhonePlayThrough PlayThroughMode
        {
            get { return EffectivePlayMode(); }
        }

        public static void SetPlayThrough(bool on)
        {
            SetPlayThrough(on ? PiPhonePlayThrough.Walk : PiPhonePlayThrough.Off);
        }

        public static void SetPlayThrough(PiPhonePlayThrough mode)
        {
            EnsureCreated();
            if (mode != PiPhonePlayThrough.Off && !PlayThroughAllowed())
            {
                Toast("This app needs the cursor.");
                SyncPlayThrough();
                return;
            }
            if (_instance != null)
                _instance._playMode = mode;
            CameraApp.UiLocked = mode != PiPhonePlayThrough.Walk;
            SyncPlayThrough();
        }

        public static void TogglePlayThrough()
        {
            if (!IsOpen)
                return;
            PiPhonePlayThrough current = _instance != null ? _instance._playMode : PiPhonePlayThrough.Off;
            PiPhonePlayThrough next = current;
            if (current == PiPhonePlayThrough.Off)
                next = PiPhonePlayThrough.Walk;
            else if (current == PiPhonePlayThrough.Walk)
                next = PiPhonePlayThrough.WalkAndCursor;
            else
                next = PiPhonePlayThrough.Off;
            if (next != PiPhonePlayThrough.Off && !PlayThroughAllowed())
            {
                Toast("This app needs the cursor.");
                return;
            }
            SetPlayThrough(next);
            string key = PhoneKeys.Format(PhoneKeys.CamCursor);
            if (next == PiPhonePlayThrough.Walk)
                Toast("Looking around. " + key + " for walk + cursor.");
            else if (next == PiPhonePlayThrough.WalkAndCursor)
                Toast("Walk, look, and tap. " + key + " for phone only.");
            else
                Toast("Cursor out. " + key + " to look around.");
        }

        public static bool IsImmersive
        {
            get { return _instance != null && _instance._immersive; }
        }

        public RectTransform Content
        {
            get { return _appContent; }
        }

        public bool IsMasterClient
        {
            get
            {
                try
                {
                    if (!PhotonNetwork.IsConnected)
                        return true;
                    return PhotonNetwork.IsMasterClient;
                }
                catch
                {
                    return true;
                }
            }
        }

        public static void EnsureCreated()
        {
            if (_instance != null)
                return;
            var go = new GameObject("PiPhone");
            UObject.DontDestroyOnLoad(go);
            _instance = go.AddComponent<PhoneMenu>();
            go.SetActive(false);
            PhoneTheme.Changed += OnThemeChanged;
            PhoneLang.Changed += OnAppsChanged;
        }

        public static bool CanUsePhone()
        {
            try
            {
                Character local = Character.localCharacter;
                if (local == null)
                    return true;
                if (local.IsGhost)
                    return true;
                if (local.data == null)
                    return true;
                if (local.data.dead || local.data.passedOut || local.data.fullyPassedOut || !local.data.fullyConscious)
                    return false;
                return true;
            }
            catch
            {
                return true;
            }
        }

        internal static void TickUseLock()
        {
            if (CanUsePhone())
                return;
            if (CallService.IsBusy && CallService.State != CallService.Phase.Incoming)
                CallService.HangUp();
            if (_instance != null && _instance._visible)
                _instance.CloseInternal(true);
        }

        public static void Toggle()
        {
            if (Time.frameCount == _toggleFrame)
                return;
            _toggleFrame = Time.frameCount;
            EnsureCreated();
            if (_instance._visible)
                _instance.CloseInternal();
            else if (!CanUsePhone())
                Toast("You're unconscious.");
            else
                _instance.OpenInternal();
        }

        public static void Open()
        {
            if (!CanUsePhone())
            {
                Toast("You're unconscious.");
                return;
            }
            EnsureCreated();
            if (!_instance._visible)
                _instance.OpenInternal();
        }

        public static void Close()
        {
            if (_instance != null)
                _instance.CloseInternal();
        }

        public static void GoHome()
        {
            if (_instance != null && _instance._visible)
                _instance.ShowHome();
        }

        public static void GoBack()
        {
            if (_instance != null && _instance._visible)
                _instance.HandleBack();
        }

        public static bool OpenApp(string id)
        {
            if (!CanUsePhone())
            {
                Toast("You're unconscious.");
                return false;
            }
            EnsureCreated();
            if (!_instance._visible)
                _instance.OpenInternal();
            return _instance.OpenAppInternal(id);
        }

        public static void OnAppsChanged()
        {
            if (_instance != null && _instance._built)
            {
                _instance.RebuildHome();
                if (_instance._recentsRoot != null && _instance._recentsRoot.activeSelf)
                    _instance.RebuildDrawer();
            }
        }

        public static void Toast(string message)
        {
            if (_instance != null && _instance._visible)
                _instance.ShowToast(message);
        }

        public static void RefreshCallUi()
        {
            if (_instance == null)
                return;
            if (_instance._visible)
                _instance.SyncCallLayer();
            AlertHud.Sync();
        }

        public static void RefreshLiveChrome()
        {
            if (_instance == null || !_instance._built)
                return;
            _instance.ApplyChrome(false);
        }

        private static void OnThemeChanged()
        {
            if (_instance == null || !_instance._built)
                return;
            string wp = PhoneTheme.WallpaperFile ?? string.Empty;
            PhoneShade.ForgetIcons();
            _instance.FillNavKeys();
            _instance.ApplyChrome(_instance._wpApplied != wp);
            _instance.RebuildHome();
            if (_instance._shadeRoot != null && _instance._shadeRoot.activeSelf)
                _instance.RebuildShade();
            if (_instance._recentsRoot != null && _instance._recentsRoot.activeSelf)
                _instance.RebuildDrawer();
        }

        public static void OnAfterSceneLoad()
        {
            if (_instance == null)
                return;
            try
            {
                if (_instance._visible)
                    _instance.CloseInternal();
                CallService.HangUp();
            }
            catch
            {
            }
        }

        public void SetTitle(string title)
        {
            if (_appTitle != null)
                _appTitle.text = title ?? string.Empty;
        }

        void IPiPhoneHost.GoHome()
        {
            ShowHome();
        }

        void IPiPhoneHost.GoBack()
        {
            HandleBack();
        }

        public void ClosePhone()
        {
            CloseInternal();
        }

        public void ShowToast(string message)
        {
            PhoneNotify.Quiet(BannerTitle(), message ?? string.Empty);
        }

        public static void ShowBanner(string title, string body)
        {
            if (_instance == null || !_instance._visible)
                return;
            string text = string.IsNullOrEmpty(title) ? (body ?? string.Empty) : title;
            if (!string.IsNullOrEmpty(body) && !string.IsNullOrEmpty(title) && body != title)
                text = title + "\n" + body;
            if (_instance._toast != null)
                _instance._toast.text = text;
            if (_instance._toastRoot != null)
                _instance._toastRoot.SetActive(true);
            else if (_instance._toast != null)
                _instance._toast.gameObject.SetActive(true);
            _instance._toastUntil = Time.unscaledTime + 3.2f;
            if (_instance._toastRoot != null)
                _instance._toastRoot.transform.SetAsLastSibling();
            if (_instance._statusBar != null)
                _instance._statusBar.SetAsLastSibling();
        }

        private string BannerTitle()
        {
            PiPhoneApp app;
            if (!string.IsNullOrEmpty(_openAppId) && PiPhoneApi.TryGetApp(_openAppId, out app) && app != null)
                return PhoneLang.AppName(app);
            return "PiPhone";
        }

        public Coroutine StartHostCoroutine(IEnumerator routine)
        {
            if (routine == null)
                return null;
            return StartCoroutine(routine);
        }

        public Button CreateButton(Transform parent, string label, UnityAction onClick, Vector2 size)
        {
            return PhoneUi.CreateButton(parent, label, onClick, size);
        }

        void IPiPhoneHost.SetLandscape(bool landscape)
        {
            SetLandscape(landscape);
        }

        void IPiPhoneHost.SetFullscreen(bool fullscreen)
        {
            SetFullscreen(fullscreen);
        }

        void IPiPhoneHost.SetImmersive(bool immersive)
        {
            SetImmersive(immersive);
        }

        bool IPiPhoneHost.IsLandscape
        {
            get { return _landscape; }
        }

        bool IPiPhoneHost.IsFullscreen
        {
            get { return _fullscreen; }
        }

        void IPiPhoneHost.ToggleLandscape()
        {
            ToggleUserLandscape();
        }

        void IPiPhoneHost.SetPlayThrough(bool on)
        {
            SetPlayThrough(on);
        }

        void IPiPhoneHost.SetPlayThrough(PiPhonePlayThrough mode)
        {
            SetPlayThrough(mode);
        }

        void IPiPhoneHost.TogglePlayThrough()
        {
            TogglePlayThrough();
        }

        bool IPiPhoneHost.IsPlayThrough
        {
            get { return IsPlayThrough; }
        }

        PiPhonePlayThrough IPiPhoneHost.PlayThroughMode
        {
            get { return PlayThroughMode; }
        }

        void IPiPhoneHost.SetWallpaperPaused(bool paused)
        {
            SetWallpaperPaused(paused);
        }

        private void OpenInternal()
        {
            if (!_built)
                Build();
            _visible = true;
            gameObject.SetActive(true);
            _prevLock = Cursor.lockState;
            _prevCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ApplyInputBlock(true);
            CaptureLook();
            _navPeek = false;
            _lastLand = !_landscape;
            ApplyPresentation();
            ShowHome();
            if (CallService.IsOnCall || CallVideo.Chasing)
                _callScreen = true;
            RefreshStatus();
            StartWallpaperPlay();
            SyncMusicBar();
            EnsureEmojiWarm();
            SyncCallLayer();
            AlertHud.Sync();
        }

        private void OnEnable()
        {
            StartWallpaperPlay();
        }

        private void EnsureEmojiWarm()
        {
            if (_emojiWarmStarted)
                return;
            _emojiWarmStarted = true;
            StartCoroutine(WarmEmoji());
        }

        private IEnumerator WarmEmoji()
        {
            PhoneEmoji.Ensure();
            while (!PhoneEmoji.WarmDone)
            {
                PhoneEmoji.WarmOnePage();
                if (PhoneEmoji.WarmDone)
                    yield break;
                if (PhoneEmoji.Rushing)
                    yield return null;
                else
                    yield return new WaitForSecondsRealtime(0.1f);
            }
        }

        private void OnDimmerClick()
        {
            if (EffectivePlayMode() != PiPhonePlayThrough.Off)
                return;
            CloseInternal();
        }

        private void CloseInternal()
        {
            CloseInternal(false);
        }

        private void CloseInternal(bool force)
        {
            if (!_visible)
                return;
            if (!force && (CallService.State == CallService.Phase.Incoming || CallService.State == CallService.Phase.Dialing))
            {
                ShowToast("Answer, decline, or hang up first.");
                return;
            }
            LeaveCurrentApp();
            CallVideo.DropToVoice();
            PhoneVideo.StopAll();
            VoiceIo.StopPlay();
            PhoneSounds.StopPreview();
            _userPinned = false;
            _userLandscape = false;
            _landscape = false;
            _fullscreen = false;
            _navPeek = false;
            _wallpaperForcedPause = false;
            _playMode = PiPhonePlayThrough.Off;
            CameraApp.UiLocked = true;
            PhoneShade.SizeOpen = false;
            _lastLand = true;
            ApplyPresentation();
            _visible = false;
            _hasFrozenLook = false;
            PhoneUi.ReleaseUiFocus();
            ApplyInputBlock(false);
            Cursor.lockState = _prevLock;
            Cursor.visible = _prevCursorVisible;
            gameObject.SetActive(false);
            AlertHud.Sync();
        }

        private void OnDisable()
        {
            _wpPlay = null;
        }

        private void HandleBack()
        {
            if (HoldCallScreen())
                return;
            HandleBack(true);
        }

        private void HandleBack(bool dismissNav)
        {
            if (HoldCallScreen())
                return;
            if (dismissNav && _navPeek && !NavPinnedByDock())
            {
                CollapseNavPeek();
                return;
            }
            if (_shadeRoot != null && _shadeRoot.activeSelf)
            {
                _shadeRoot.SetActive(false);
                return;
            }
            if (_recentsRoot != null && _recentsRoot.activeSelf)
            {
                ShowHome();
                return;
            }
            if (!string.IsNullOrEmpty(_openAppId))
            {
                PiPhoneApp open;
                if (PiPhoneApi.TryGetApp(_openAppId, out open) && open != null && open.OnBack != null)
                {
                    try
                    {
                        if (open.OnBack())
                            return;
                    }
                    catch (Exception ex)
                    {
                        Plugin.LogError("App '" + _openAppId + "' OnBack: " + ex.Message);
                    }
                }
                if (DialerApp.TryGoBack())
                    return;
                if (SettingsApp.TryGoBack())
                    return;
                if (PhotosApp.TryGoBack())
                    return;
                if (SnakeApp.TryGoBack())
                    return;
                if (PhoneGames.TryGoBack())
                    return;
                if (SoundsApp.TryGoBack())
                    return;
                if (MessagesApp.TryGoBack())
                    return;
                if (AppStoreApp.TryGoBack())
                    return;
                if (ClosetApp.TryGoBack())
                    return;
                ShowHome();
                return;
            }
            CloseInternal();
        }

        private bool HoldCallScreen()
        {
            if (CallVideo.Chasing)
                return true;
            if (CallService.State == CallService.Phase.Incoming || CallService.State == CallService.Phase.Dialing)
                return true;
            if (CallService.IsOnCall && _callScreen)
            {
                MinimizeCall();
                return true;
            }
            return false;
        }

        private void MinimizeCall()
        {
            if (CallVideo.Chasing)
                return;
            if (CallService.State == CallService.Phase.Incoming || CallService.State == CallService.Phase.Dialing)
                return;
            if (!CallService.IsOnCall)
                return;
            CallVideo.DropToVoice();
            _callScreen = false;
            SyncCallLayer();
        }

        private void ShowHome()
        {
            if (CallService.IsOnCall && _callScreen && CallService.State != CallService.Phase.Incoming && CallService.State != CallService.Phase.Dialing && !CallVideo.Chasing)
                MinimizeCall();
            LeaveCurrentApp();
            HideIconMenu();
            if (_homeRoot != null)
                _homeRoot.SetActive(true);
            if (_appRoot != null)
                _appRoot.SetActive(false);
            if (_recentsRoot != null)
                _recentsRoot.SetActive(false);
            if (_shadeRoot != null)
                _shadeRoot.SetActive(false);
            RefreshStatus();
            SyncPlayThrough();
            SyncBackgroundWork();
            ApplyChromeInsets();
        }

        private void ShowDrawer()
        {
            if (CallService.IsOnCall && _callScreen && !CallVideo.Chasing)
                MinimizeCall();
            LeaveCurrentApp();
            if (_homeRoot != null)
                _homeRoot.SetActive(false);
            if (_appRoot != null)
                _appRoot.SetActive(false);
            if (_recentsRoot != null)
            {
                RebuildDrawer();
                _recentsRoot.SetActive(true);
            }
            SyncPlayThrough();
            SyncBackgroundWork();
            ApplyChromeInsets();
        }

        private bool OpenAppInternal(string id)
        {
            PiPhoneApp app;
            if (!PiPhoneApi.TryGetApp(id, out app) || app == null)
                return false;
            if (!PhoneStore.IsInstalled(id))
            {
                ShowToast("Install it from Apps first.");
                if (id != BuiltinApps.StoreId)
                    return OpenAppInternal(BuiltinApps.StoreId);
                return false;
            }

            if (CallService.IsOnCall && _callScreen && !CallVideo.Chasing)
                MinimizeCall();
            if (_homeRoot != null)
                _homeRoot.SetActive(false);
            if (_recentsRoot != null)
                _recentsRoot.SetActive(false);
            if (_appRoot != null)
                _appRoot.SetActive(true);

            LeaveCurrentApp();
            _openAppId = app.Id;
            RememberRecent(app.Id);
            SetTitle(PhoneLang.AppName(app));
            ApplyAppPresentation(app);
            ClearContent();
            try
            {
                if (app.OnOpen != null)
                    app.OnOpen(this);
            }
            catch (Exception ex)
            {
                Plugin.LogError("App '" + app.Id + "' failed to open: " + ex.Message);
                ShowToast("That app crashed.");
            }
            if (app.ApplyPhoneFonts)
                PhoneUi.ApplyAllFonts(_appContent);
            SyncPlayThrough();
            SyncBackgroundWork();
            ApplyChromeInsets();
            return true;
        }

        private void LeaveCurrentApp()
        {
            if (string.IsNullOrEmpty(_openAppId))
                return;
            PiPhoneApp app;
            if (PiPhoneApi.TryGetApp(_openAppId, out app) && app != null && app.OnClose != null)
            {
                try { app.OnClose(); }
                catch (Exception ex) { Plugin.LogError("App '" + _openAppId + "' OnClose: " + ex.Message); }
            }
            _openAppId = null;
            _wallpaperForcedPause = false;
            ResetAppPresentation();
            ClearContent();
        }

        private void ClearContent()
        {
            if (_appContent == null)
                return;
            for (int i = _appContent.childCount - 1; i >= 0; i--)
                UObject.Destroy(_appContent.GetChild(i).gameObject);
        }

        private void RememberRecent(string id)
        {
            _recents.Remove(id);
            _recents.Insert(0, id);
            while (_recents.Count > 8)
                _recents.RemoveAt(_recents.Count - 1);
        }

        private void Build()
        {
            PhoneUi.CreateOverlayCanvas(gameObject, SortOrder);

            var dim = PhoneUi.CreateImage(transform, "Dimmer", PhoneUi.White(), new Color(0f, 0f, 0f, 0f));
            PhoneUi.Stretch(dim, 0f, 0f);
            dim.GetComponent<Image>().raycastTarget = true;
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(OnDimmerClick);

            _bezel = PhoneUi.CreateImage(transform, "Bezel", PhoneUi.Rounded(42), PhoneUi.Bezel);
            _bezelImg = _bezel.GetComponent<Image>();
            _bezel.anchorMin = _bezel.anchorMax = new Vector2(0.5f, 0.5f);
            _bezel.pivot = new Vector2(0.5f, 0.5f);
            _bezel.sizeDelta = new Vector2(PhoneWidth, PhoneHeight);
            _bezel.SetAsLastSibling();
            _bezelDrag = _bezel.gameObject.AddComponent<BezelDrag>();
            _bezelDrag.Menu = this;

            var screen = PhoneUi.CreateImage(_bezel, "Screen", PhoneUi.Rounded(34), Color.white);
            PhoneUi.Stretch(screen, 14f, 14f);
            var mask = screen.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            var wallpaper = PhoneUi.CreateImage(screen, "Wallpaper", PhoneUi.Wallpaper(), Color.white);
            PhoneUi.Stretch(wallpaper, 0f, 0f);
            _wallpaperImg = wallpaper.GetComponent<Image>();
            _wallpaperImg.raycastTarget = false;
            _wallpaperImg.type = Image.Type.Simple;

            BuildStatusBar(screen);
            BuildNavBar(screen);
            BuildPages(screen);
            BuildSideButtons();

            _veil = PhoneUi.CreateImage(screen, "Veil", PhoneUi.White(), new Color(0f, 0f, 0f, 0f)).GetComponent<Image>();
            PhoneUi.Stretch(_veil.rectTransform, 0f, 0f);
            _veil.raycastTarget = false;

            BuildShade(screen);
            EnsureShadeButtons();
            BuildPunchHole(_bezel);
            BuildToast(screen);
            BuildCallLayer(screen);
            RaiseChrome();
            ApplyChrome();
            ApplyPlacement();

            _built = true;
            RebuildHome();
        }

        private void BuildStatusBar(RectTransform screen)
        {
            var bar = PhoneUi.CreateImage(screen, "StatusBar", PhoneUi.White(), new Color(0f, 0f, 0f, 0f));
            _statusBar = bar;
            _statusImg = bar.GetComponent<Image>();
            PhoneUi.StretchTop(bar, 32f);
            var barImg = bar.GetComponent<Image>();
            barImg.raycastTarget = true;
            var barBtn = bar.gameObject.AddComponent<Button>();
            barBtn.transition = Selectable.Transition.None;
            barBtn.onClick.AddListener(ToggleShade);

            _statusTime = PhoneUi.CreateLabel(bar, "Time", "12:00", 13f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            _statusTime.rectTransform.anchorMin = new Vector2(0f, 0f);
            _statusTime.rectTransform.anchorMax = new Vector2(0.68f, 1f);
            _statusTime.rectTransform.offsetMin = new Vector2(16f, 0f);
            _statusTime.rectTransform.offsetMax = new Vector2(0f, 0f);
            _statusTime.overflowMode = TextOverflowModes.Ellipsis;

            _statusRight = PhoneUi.CreateLabel(bar, "Right", "LTE  84%", 13f, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
            _statusRight.rectTransform.anchorMin = new Vector2(0.68f, 0f);
            _statusRight.rectTransform.anchorMax = new Vector2(1f, 1f);
            _statusRight.rectTransform.offsetMin = Vector2.zero;
            _statusRight.rectTransform.offsetMax = new Vector2(-16f, 0f);
            _statusRight.color = PhoneUi.TextDim;
        }

        public static void RefreshMusicBar()
        {
            if (_instance != null)
                _instance.SyncMusicBar();
        }

        private void SyncMusicBar()
        {
            ApplyChromeInsets();
            if (_shadeRoot != null && _shadeRoot.activeSelf)
                RebuildShade();
        }

        private void BuildNavBar(RectTransform screen)
        {
            var bar = PhoneUi.CreateImage(screen, "NavBar", PhoneUi.Rounded(20), PhoneUi.Nav);
            _navImg = bar.GetComponent<Image>();
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(0f, 8f);
            var layout = PhoneUi.AddHorizontal(bar.gameObject, 6f);
            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fit = bar.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            FillNavKeys();
            BuildNavHandle(screen);
        }

        internal static void RegisterNavButton(PiPhoneNavButton button)
        {
            if (button == null || string.IsNullOrEmpty(button.Id))
                return;
            for (int i = 0; i < _navExtras.Count; i++)
            {
                if (_navExtras[i] != null && string.Equals(_navExtras[i].Id, button.Id, StringComparison.OrdinalIgnoreCase))
                {
                    _navExtras[i] = button;
                    if (_instance != null)
                        _instance.FillNavKeys();
                    return;
                }
            }
            _navExtras.Add(button);
            if (_instance != null)
                _instance.FillNavKeys();
        }

        private void FillNavKeys()
        {
            if (_navImg == null)
                return;
            _navFills.Clear();
            _navGlyphs.Clear();
            for (int i = _navImg.transform.childCount - 1; i >= 0; i--)
                UObject.DestroyImmediate(_navImg.transform.GetChild(i).gameObject);
            CreateNavKey(_navImg.rectTransform, PhoneIcons.Material("arrow_back"), "<", NavBack, true);
            CreateNavKey(_navImg.rectTransform, PhoneIcons.Material("home"), "○", NavHome, true);
            CreateNavKey(_navImg.rectTransform, PhoneIcons.Material("apps"), "▢", NavDrawer, true);
            _navRotate = CreateNavKey(_navImg.rectTransform, PhoneIcons.Material("screen_rotation"), "R", ToggleUserLandscape, true);
            var extras = new List<PiPhoneNavButton>(_navExtras);
            extras.Sort((a, b) =>
            {
                int ao = a != null ? a.SortOrder : 0;
                int bo = b != null ? b.SortOrder : 0;
                return ao.CompareTo(bo);
            });
            for (int i = 0; i < extras.Count; i++)
            {
                PiPhoneNavButton extra = extras[i];
                if (extra == null)
                    continue;
                if (extra.Available != null && !extra.Available())
                    continue;
                Sprite icon = extra.IconFn != null ? extra.IconFn() : extra.Icon;
                Action click = extra.OnClick;
                CreateNavKey(_navImg.rectTransform, icon, string.IsNullOrEmpty(extra.Glyph) ? "+" : extra.Glyph, () =>
                {
                    if (click != null)
                        click();
                }, extra.TintIcon);
            }
            PaintNavKeys();
            ApplyChromeInsets();
        }

        private void BuildNavHandle(RectTransform screen)
        {
            var handle = PhoneUi.CreateImage(screen, "NavHandle", PhoneUi.Rounded(12), PhoneUi.Nav);
            _navHandle = handle;
            handle.anchorMin = new Vector2(0.5f, 0f);
            handle.anchorMax = new Vector2(0.5f, 0f);
            handle.pivot = new Vector2(0.5f, 0f);
            handle.sizeDelta = new Vector2(28f, 16f);
            handle.anchoredPosition = Vector2.zero;
            var drag = handle.gameObject.AddComponent<NavHandleDrag>();
            drag.Menu = this;
            Sprite up = PhoneIcons.Material("expand_less");
            if (up != null)
            {
                var icon = PhoneUi.CreateImage(handle, "Glyph", up, Color.white);
                icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
                icon.pivot = new Vector2(0.5f, 0.5f);
                icon.sizeDelta = new Vector2(14f, 14f);
                icon.anchoredPosition = Vector2.zero;
                icon.GetComponent<Image>().raycastTarget = false;
                icon.GetComponent<Image>().preserveAspect = true;
                _navHandleLabel = null;
            }
            else
            {
                _navHandleLabel = PhoneUi.CreateLabel(handle, "Glyph", "^", 16f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Stretch(_navHandleLabel.rectTransform, 0f, 0f);
            }
            handle.gameObject.SetActive(false);
        }

        private void NavBack()
        {
            HandleBack(false);
        }

        private void NavHome()
        {
            CollapseNavPeek();
            ShowHome();
        }

        private void NavDrawer()
        {
            CollapseNavPeek();
            ShowDrawer();
        }

        private void ToggleNavPeek()
        {
            if (NavPinnedByDock())
                return;
            _navPeek = !_navPeek;
            ApplyChromeInsets();
        }

        private void CollapseNavPeek()
        {
            if (!_navPeek)
                return;
            _navPeek = false;
            ApplyChromeInsets();
        }

        private Button CreateNavKey(Transform parent, Sprite icon, string fallback, UnityAction action, bool tintIcon)
        {
            var btn = PhoneUi.CreateIconChip(parent, fallback, icon, action, false, new Vector2(40f, 32f));
            var le = btn.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.flexibleWidth = 0f;
                le.flexibleHeight = 0f;
                le.minWidth = 40f;
                le.preferredWidth = 40f;
                le.minHeight = 32f;
                le.preferredHeight = 32f;
            }
            Image fill = btn.GetComponent<Image>();
            if (fill != null)
                _navFills.Add(fill);
            Transform glyph = btn.transform.Find("I");
            if (glyph == null)
                glyph = btn.transform.Find("G");
            if (glyph != null)
            {
                var glyphImg = glyph.GetComponent<Graphic>();
                if (glyphImg != null)
                {
                    if (tintIcon)
                    {
                        var image = glyphImg as Image;
                        if (image != null)
                            _navGlyphs.Add(image);
                    }
                    else
                        glyphImg.color = Color.white;
                }
            }
            return btn;
        }

        private void MoveNavHandle(Vector2 local, bool save)
        {
            if (NavPinnedByDock())
                return;
            float w;
            float h;
            NavScreen(out w, out h);
            float halfW = w * 0.5f;
            float halfH = h * 0.5f;
            float top = halfH - 40f;
            float bottom = -halfH;
            float y = Mathf.Clamp(local.y, bottom + 10f, top);
            float x = Mathf.Clamp(local.x, -halfW + 10f, halfW - 10f);
            float db = (local.y - bottom) * (local.y - bottom);
            float dl = (local.x + halfW) * (local.x + halfW);
            float dr = (local.x - halfW) * (local.x - halfW);
            int edge = 0;
            float best = db;
            if (dl < best)
            {
                best = dl;
                edge = 1;
            }
            if (dr < best)
            {
                best = dr;
                edge = 2;
            }
            int current = PhoneTheme.NavEdge;
            float currentD = current == 1 ? dl : current == 2 ? dr : db;
            if (current >= 0 && current <= 2 && currentD <= best + 576f)
                edge = current;
            PhoneTheme.NavEdge = edge;
            PhoneTheme.NavHandleX = edge == 0 ? x : y;
            ApplyNavAxis();
            if (save)
                PhoneTheme.RememberNavPlace(PhoneTheme.NavEdge, PhoneTheme.NavHandleX);
        }

        private void NavScreen(out float width, out float height)
        {
            RectTransform parent = null;
            if (_navHandle != null)
                parent = _navHandle.parent as RectTransform;
            if (parent == null && _navImg != null)
                parent = _navImg.rectTransform.parent as RectTransform;
            width = parent != null ? parent.rect.width : (_landscape ? PhoneHeight : PhoneWidth);
            height = parent != null ? parent.rect.height : (_landscape ? PhoneWidth : PhoneHeight);
            if (width < 80f)
                width = _landscape ? PhoneHeight - 28f : PhoneWidth - 28f;
            if (height < 80f)
                height = _landscape ? PhoneWidth - 28f : PhoneHeight - 28f;
        }

        private static void AnchorEdge(RectTransform rt, int edge)
        {
            if (rt == null)
                return;
            if (edge == 1)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
            }
            else if (edge == 2)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);
            }
            else
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
            }
        }

        private void ApplyNavAxis()
        {
            if (_navImg == null)
                return;
            int n = 0;
            for (int i = 0; i < _navImg.transform.childCount; i++)
            {
                if (_navImg.transform.GetChild(i).gameObject.activeSelf)
                    n++;
            }
            float rowW = 16f + n * 40f + Mathf.Max(0, n - 1) * 6f;
            float stackH = 12f + n * 32f + Mathf.Max(0, n - 1) * 6f;
            float w;
            float h;
            NavScreen(out w, out h);
            int edge = _navDocked ? 0 : PhoneTheme.NavEdge;
            if (edge < 0 || edge > 2)
                edge = 0;
            bool side = edge != 0;
            float along = _navDocked && PhoneTheme.NavEdge != 0 ? 0f : PhoneTheme.NavHandleX;
            float halfW = w * 0.5f;
            bool stack = side || (n > 1 && (along - rowW * 0.5f < -halfW + 6f || along + rowW * 0.5f > halfW - 6f));
            float barH = stack ? stackH : 44f;
            float barW = stack ? 56f : rowW;
            if (side && stack && barH > h - 56f)
            {
                stack = false;
                barH = 44f;
                barW = rowW;
            }
            bool open = _navImg.gameObject.activeSelf;
            float extent = open ? (side ? barH * 0.5f : barW * 0.5f) : 14f;
            if (!_navDocked)
            {
                if (!side)
                {
                    float limit = halfW - extent - 6f;
                    if (limit < 0f)
                        limit = 0f;
                    along = Mathf.Clamp(along, -limit, limit);
                }
                else
                {
                    float halfH = h * 0.5f;
                    float min = -halfH + extent + 6f;
                    float max = halfH - 40f - extent;
                    along = min > max ? (min + max) * 0.5f : Mathf.Clamp(along, min, max);
                }
                PhoneTheme.NavHandleX = along;
            }
            _navBarHeight = barH;
            _navBarWidth = barW;
            bool hasStack = _navImg.GetComponent<VerticalLayoutGroup>() != null;
            bool hasRow = _navImg.GetComponent<HorizontalLayoutGroup>() != null;
            if (stack != _navStacked || (stack ? !hasStack : !hasRow))
            {
                _navStacked = stack;
                var row = _navImg.GetComponent<HorizontalLayoutGroup>();
                var column = _navImg.GetComponent<VerticalLayoutGroup>();
                if (stack)
                {
                    if (row != null)
                        UObject.DestroyImmediate(row);
                    column = _navImg.gameObject.AddComponent<VerticalLayoutGroup>();
                    column.spacing = 6f;
                    column.padding = new RectOffset(8, 8, 6, 6);
                    column.childAlignment = TextAnchor.MiddleCenter;
                    column.childControlWidth = true;
                    column.childControlHeight = true;
                    column.childForceExpandWidth = false;
                    column.childForceExpandHeight = false;
                }
                else
                {
                    if (column != null)
                        UObject.DestroyImmediate(column);
                    row = _navImg.gameObject.AddComponent<HorizontalLayoutGroup>();
                    row.spacing = 6f;
                    row.padding = new RectOffset(8, 8, 6, 6);
                    row.childAlignment = TextAnchor.MiddleCenter;
                    row.childControlWidth = true;
                    row.childControlHeight = true;
                    row.childForceExpandWidth = false;
                    row.childForceExpandHeight = false;
                }
            }
            var le = _navImg.GetComponent<LayoutElement>() ?? _navImg.gameObject.AddComponent<LayoutElement>();
            le.minWidth = barW;
            le.preferredWidth = barW;
            le.flexibleWidth = 0f;
            le.minHeight = barH;
            le.preferredHeight = barH;
            le.flexibleHeight = 0f;
            _navImg.rectTransform.sizeDelta = new Vector2(barW, barH);
            PlaceNavChrome();
        }

        private void PlaceNavChrome()
        {
            float portrait = Mathf.Clamp(PhoneTheme.PhoneScale, 0.55f, 1.35f);
            float current = _landscape
                ? Mathf.Clamp(PhoneTheme.PhoneScaleLand, 0.55f, 1.8f)
                : portrait;
            float s = current > 0.01f ? portrait / current : 1f;
            var scale = new Vector3(s, s, 1f);
            if (_navImg != null)
            {
                _navImg.rectTransform.localScale = scale;
                _navImg.rectTransform.localEulerAngles = Vector3.zero;
            }
            if (_navHandle != null)
            {
                _navHandle.localScale = scale;
                _navHandle.localEulerAngles = Vector3.zero;
            }
            if (_navHandle == null || _navImg == null)
                return;
            int edge = _navDocked ? 0 : PhoneTheme.NavEdge;
            if (edge < 0 || edge > 2)
                edge = 0;
            bool open = _navImg.gameObject.activeSelf;
            float along = _navDocked && PhoneTheme.NavEdge != 0 ? 0f : PhoneTheme.NavHandleX;
            float lift = _navDocked ? 92f : 8f;
            AnchorEdge(_navImg.rectTransform, edge);
            AnchorEdge(_navHandle, edge);
            bool side = edge != 0;
            _navHandle.sizeDelta = side ? new Vector2(16f, 28f) : new Vector2(28f, 16f);
            if (edge == 1)
            {
                _navImg.rectTransform.anchoredPosition = new Vector2(8f, along);
                float x = open ? 8f + _navBarWidth * s : 0f;
                _navHandle.anchoredPosition = new Vector2(x, along);
            }
            else if (edge == 2)
            {
                _navImg.rectTransform.anchoredPosition = new Vector2(-8f, along);
                float x = open ? -8f - _navBarWidth * s : 0f;
                _navHandle.anchoredPosition = new Vector2(x, along);
            }
            else
            {
                _navImg.rectTransform.anchoredPosition = new Vector2(along, lift);
                float y = open ? lift + _navBarHeight * s : 0f;
                _navHandle.anchoredPosition = new Vector2(along, y);
            }
            Transform glyph = _navHandle.Find("Glyph");
            if (glyph != null)
            {
                float spin = edge == 1 ? -90f : edge == 2 ? 90f : 0f;
                glyph.localEulerAngles = new Vector3(0f, 0f, spin);
            }
        }

        private void BuildPages(RectTransform screen)
        {
            var pages = PhoneUi.CreateImage(screen, "Pages", PhoneUi.White(), new Color(1f, 1f, 1f, 0f));
            PhoneUi.Stretch(pages, 0f, 0f);
            pages.offsetMin = Vector2.zero;
            pages.offsetMax = Vector2.zero;
            pages.GetComponent<Image>().raycastTarget = false;
            _pagesRt = pages;

            _homeRoot = new GameObject("Home", typeof(RectTransform));
            _homeRoot.transform.SetParent(pages, false);
            PhoneUi.Stretch(_homeRoot.GetComponent<RectTransform>(), 0f, 0f);
            PhoneUi.AddVertical(_homeRoot, 6f, new RectOffset(16, 16, 36, 8));

            var gridScroll = PhoneUi.CreateScrollView(_homeRoot.transform, out _homeGrid);
            var scrollLe = gridScroll.gameObject.AddComponent<LayoutElement>();
            scrollLe.flexibleHeight = 1f;
            scrollLe.minHeight = 180f;
            var grid = _homeGrid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(78f, 96f);
            grid.spacing = new Vector2(10f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.padding = new RectOffset(8, 8, 4, 8);
            PhoneUi.FitVertical(_homeGrid.gameObject);

            _dock = PhoneUi.CreateImage(_homeRoot.transform, "Dock", PhoneUi.White(), new Color(1f, 1f, 1f, 0f));
            _dockImg = _dock.GetComponent<Image>();
            _dockImg.raycastTarget = false;
            PhoneUi.Size(_dock.gameObject, 88f);
            PhoneUi.AddHorizontal(_dock.gameObject, 4f);
            var dockLayout = _dock.GetComponent<HorizontalLayoutGroup>();
            dockLayout.padding = new RectOffset(10, 10, 8, 8);
            dockLayout.childAlignment = TextAnchor.MiddleCenter;
            dockLayout.childForceExpandWidth = false;
            dockLayout.childControlWidth = false;

            _appRoot = new GameObject("App", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _appRoot.transform.SetParent(pages, false);
            PhoneUi.Stretch(_appRoot.GetComponent<RectTransform>(), 0f, 0f);
            var appBg = _appRoot.GetComponent<Image>();
            appBg.sprite = PhoneUi.White();
            appBg.color = PhoneUi.Screen;
            _appBg = appBg;
            _appRoot.SetActive(false);
            PhoneUi.AddVertical(_appRoot, 0f, new RectOffset(0, 0, 32, 0));

            var header = PhoneUi.CreateImage(_appRoot.transform, "Header", PhoneUi.White(), new Color(0.10f, 0.11f, 0.13f, 1f));
            _headerImg = header.GetComponent<Image>();
            PhoneUi.Size(header.gameObject, 44f);
            _appTitle = PhoneUi.CreateLabel(header, "Title", "App", 18f, FontStyles.Normal, TextAlignmentOptions.Center);
            PhoneUi.Stretch(_appTitle.rectTransform, 12f, 4f);

            var body = PhoneUi.CreateImage(_appRoot.transform, "Body", PhoneUi.White(), new Color(1f, 1f, 1f, 0f));
            var bodyLe = body.gameObject.AddComponent<LayoutElement>();
            bodyLe.flexibleHeight = 1f;
            bodyLe.minHeight = 120f;
            _appContent = body;
            PhoneUi.AddVertical(body.gameObject, 8f, new RectOffset(12, 12, 10, 10));

            _recentsRoot = new GameObject("Recents", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _recentsRoot.transform.SetParent(pages, false);
            PhoneUi.Stretch(_recentsRoot.GetComponent<RectTransform>(), 0f, 0f);
            var recentsBg = _recentsRoot.GetComponent<Image>();
            recentsBg.sprite = PhoneUi.White();
            recentsBg.color = new Color(0f, 0f, 0f, 0f);
            recentsBg.raycastTarget = true;
            _recentsRoot.SetActive(false);
            PhoneUi.AddVertical(_recentsRoot, 4f, new RectOffset(12, 12, 36, 8));
            var recentsTitle = PhoneUi.CreateLabel(_recentsRoot.transform, "Title", "All apps", 16f, FontStyles.Normal, TextAlignmentOptions.Center);
            PhoneUi.Size(recentsTitle.gameObject, 22f);

            var drawerScroll = PhoneUi.CreateScrollView(_recentsRoot.transform, out _drawerGrid);
            drawerScroll.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            var drawerGrid = _drawerGrid.gameObject.AddComponent<GridLayoutGroup>();
            drawerGrid.cellSize = new Vector2(78f, 96f);
            drawerGrid.spacing = new Vector2(10f, 8f);
            drawerGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            drawerGrid.constraintCount = 4;
            drawerGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
            drawerGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            drawerGrid.childAlignment = TextAnchor.UpperLeft;
            drawerGrid.padding = new RectOffset(8, 8, 4, 8);
            PhoneUi.FitVertical(_drawerGrid.gameObject);
        }

        private void BuildPunchHole(RectTransform bezel)
        {
            var hole = PhoneUi.CreateImage(bezel, "PunchHole", PhoneUi.Circle(), new Color(0.04f, 0.04f, 0.05f, 1f));
            _punchRt = hole;
            hole.GetComponent<Image>().raycastTarget = false;
            hole.SetAsLastSibling();
            PlacePunchHole();
        }

        private void BuildToast(RectTransform screen)
        {
            var toastRt = PhoneUi.CreateImage(screen, "Toast", PhoneUi.Rounded(18), new Color(0.10f, 0.11f, 0.13f, 0.96f));
            toastRt.anchorMin = new Vector2(0.05f, 1f);
            toastRt.anchorMax = new Vector2(0.95f, 1f);
            toastRt.pivot = new Vector2(0.5f, 1f);
            toastRt.sizeDelta = new Vector2(0f, 54f);
            toastRt.anchoredPosition = new Vector2(0f, -34f);
            _toastRoot = toastRt.gameObject;
            _toast = PhoneUi.CreateLabel(toastRt, "Text", string.Empty, 13f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            PhoneUi.Stretch(_toast.rectTransform, 12f, 6f);
#pragma warning disable CS0618
            _toast.enableWordWrapping = true;
#pragma warning restore CS0618
            var btn = toastRt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(ToggleShade);
            toastRt.gameObject.SetActive(false);
        }

        private void BuildCallLayer(RectTransform screen)
        {
            var layer = PhoneUi.CreateImage(screen, "CallLayer", PhoneUi.White(), PhoneUi.Screen);
            PhoneUi.Stretch(layer, 0f, 0f);
            _callLayer = layer.gameObject;
            PhoneUi.AddVertical(_callLayer, 10f, new RectOffset(20, 20, 40, 20));

            _callFace = new GameObject("Face", typeof(RectTransform));
            _callFace.transform.SetParent(_callLayer.transform, false);
            PhoneUi.Size(_callFace, 72f);
            PhoneUi.AddHorizontal(_callFace, 0f);
            var faceLayout = _callFace.GetComponent<HorizontalLayoutGroup>();
            faceLayout.childAlignment = TextAnchor.MiddleCenter;
            faceLayout.childForceExpandWidth = false;

            _callTitle = PhoneUi.CreateLabel(_callLayer.transform, "Name", "Call", 22f, FontStyles.Normal, TextAlignmentOptions.Center);
            PhoneUi.Size(_callTitle.gameObject, 36f);
            _callSub = PhoneUi.CreateLabel(_callLayer.transform, "Sub", string.Empty, 15f, FontStyles.Normal, TextAlignmentOptions.Center);
            _callSub.color = PhoneUi.TextDim;
            PhoneUi.Size(_callSub.gameObject, 28f);

            var videoPane = new GameObject("Video", typeof(RectTransform));
            videoPane.transform.SetParent(_callLayer.transform, false);
            var vle = videoPane.AddComponent<LayoutElement>();
            vle.flexibleHeight = 1f;
            vle.minHeight = 180f;
            var remoteGo = new GameObject("Remote", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            remoteGo.transform.SetParent(videoPane.transform, false);
            PhoneUi.Stretch(remoteGo.GetComponent<RectTransform>(), 0f, 0f);
            _callRemote = remoteGo.GetComponent<RawImage>();
            _callRemote.color = Color.white;
            _callRemote.enabled = false;
            var localGo = new GameObject("Local", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            localGo.transform.SetParent(videoPane.transform, false);
            var localRt = localGo.GetComponent<RectTransform>();
            localRt.anchorMin = new Vector2(1f, 0f);
            localRt.anchorMax = new Vector2(1f, 0f);
            localRt.pivot = new Vector2(1f, 0f);
            localRt.sizeDelta = new Vector2(88f, 120f);
            localRt.anchoredPosition = new Vector2(-8f, 8f);
            _callLocal = localGo.GetComponent<RawImage>();
            _callLocal.color = Color.white;
            _callLocal.enabled = false;

            _incomingBtns = new GameObject("Incoming", typeof(RectTransform));
            _incomingBtns.transform.SetParent(_callLayer.transform, false);
            PhoneUi.Size(_incomingBtns, 80f);
            PhoneUi.AddHorizontal(_incomingBtns, 24f);
            var inc = _incomingBtns.GetComponent<HorizontalLayoutGroup>();
            inc.childForceExpandWidth = false;
            inc.childControlWidth = false;
            Button decline = PhoneUi.CreateCircleButton(_incomingBtns.transform, "No", CallService.Reject, 64f, PhoneUi.HangRed);
            Button answer = PhoneUi.CreateCircleButton(_incomingBtns.transform, "Yes", CallService.Accept, 64f, PhoneUi.CallGreen);
            PhoneUi.SetCircleIcon(decline, PhoneIcons.Material("call_end"));
            PhoneUi.SetCircleIcon(answer, PhoneIcons.Material("call"));

            _activeBtns = new GameObject("Active", typeof(RectTransform));
            _activeBtns.transform.SetParent(_callLayer.transform, false);
            PhoneUi.Size(_activeBtns, 140f);
            PhoneUi.AddVertical(_activeBtns, 8f, new RectOffset(0, 0, 0, 0));
            var row = new GameObject("Row", typeof(RectTransform));
            row.transform.SetParent(_activeBtns.transform, false);
            PhoneUi.Size(row, 72f);
            PhoneUi.AddHorizontal(row, 16f);
            var rowL = row.GetComponent<HorizontalLayoutGroup>();
            rowL.childForceExpandWidth = false;
            rowL.childControlWidth = false;
            Button end = PhoneUi.CreateCircleButton(row.transform, "End", CallService.HangUp, 64f, PhoneUi.HangRed);
            PhoneUi.SetCircleIcon(end, PhoneIcons.Material("call_end"));
            var mute = PhoneUi.CreateCircleButton(row.transform, "Mute", CallService.ToggleMute, 64f, PhoneUi.SurfaceAlt);
            _muteBtn = mute;
            Button video = PhoneUi.MaterialChip(_activeBtns.transform, "videocam", "Video", CallVideo.Toggle, new Vector2(40f, 40f));
            PhoneUi.CreateButton(_activeBtns.transform, "Add scout", ShowAddToCall, new Vector2(220f, 40f));
            var vm = PhoneUi.CreateIconChip(_activeBtns.transform, "Voicemail", PhoneIcons.Material("voicemail"), CallService.LeaveVoicemailForCurrent, false, new Vector2(44f, 44f));
            _vmBtn = vm.gameObject;
            CallVideo.Bind(_callRemote, _callLocal, video);

            _callLayer.SetActive(false);

            var bar = PhoneUi.CreateImage(screen, "CallBar", PhoneUi.Rounded(14), PhoneUi.Nav);
            _callBar = bar.gameObject;
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(300f, 36f);
            bar.anchoredPosition = new Vector2(0f, -36f);
            var open = bar.gameObject.AddComponent<Button>();
            open.targetGraphic = bar.GetComponent<Image>();
            open.transition = Selectable.Transition.None;
            open.onClick.AddListener(() =>
            {
                _callScreen = true;
                SyncCallLayer();
            });
            _callBarLabel = PhoneUi.CreateLabel(bar, "Who", "On a call", 14f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            _callBarLabel.rectTransform.offsetMin = new Vector2(12f, 0f);
            _callBarLabel.rectTransform.offsetMax = new Vector2(-48f, 0f);
            PhoneUi.Stretch(_callBarLabel.rectTransform, 0f, 0f);
            _callBarLabel.rectTransform.offsetMin = new Vector2(12f, 0f);
            _callBarLabel.rectTransform.offsetMax = new Vector2(-48f, 0f);
            var hang = PhoneUi.CreateIconChip(bar, "End", PhoneIcons.Material("call_end"), CallService.HangUp, false, new Vector2(32f, 28f));
            var hangRt = hang.GetComponent<RectTransform>();
            hangRt.anchorMin = hangRt.anchorMax = new Vector2(1f, 0.5f);
            hangRt.pivot = new Vector2(1f, 0.5f);
            hangRt.anchoredPosition = new Vector2(-6f, 0f);
            _callBar.SetActive(false);
        }

        private void ShowAddToCall()
        {
            Photon.Realtime.Player[] others = BuiltinApps.OtherPlayers();
            int added = 0;
            for (int i = 0; i < others.Length; i++)
            {
                if (others[i] == null || CallService.Members.Contains(others[i].ActorNumber))
                    continue;
                CallService.AddPerson(others[i].ActorNumber);
                added++;
            }
            ShowToast(added > 0 ? "Added to the call." : "Nobody else to add.");
        }

        private void SyncCallLayer()
        {
            if (_callLayer == null)
                return;
            bool chasing = CallVideo.Chasing;
            bool incoming = !chasing && (CallService.State == CallService.Phase.Incoming || CallService.State == CallService.Phase.Dialing);
            if (!CallService.IsBusy && !chasing)
                _callScreen = true;
            else if (incoming || chasing)
                _callScreen = true;
            bool busy = CallService.IsBusy || chasing;
            bool showFull = busy && _callScreen;
            _callLayer.SetActive(showFull);
            if (_callBar != null)
            {
                bool showBar = CallService.IsOnCall && !showFull;
                _callBar.SetActive(showBar);
                if (showBar && _callBarLabel != null)
                    _callBarLabel.text = string.IsNullOrEmpty(CallService.RemoteName) ? "On a call" : CallService.RemoteName;
            }
            RaiseChrome();
            if (!showFull)
                return;
            if (_callTitle != null)
                _callTitle.text = CallVideo.Chasing ? "Scoutmaster" : CallService.RemoteName;
            PaintCallFace();
            if (_callSub != null)
            {
                if (CallVideo.Chasing)
                    _callSub.text = "He's coming for you";
                else if (CallService.State == CallService.Phase.Incoming)
                    _callSub.text = CallService.Group ? "Incoming group call" : "Incoming call";
                else if (CallService.State == CallService.Phase.Dialing)
                    _callSub.text = "Calling...";
                else
                    _callSub.text = CallService.Muted ? "Muted" : (CallService.Group ? "Group call" : "On a call");
            }
            if (_incomingBtns != null)
                _incomingBtns.SetActive(!CallVideo.Chasing && CallService.State == CallService.Phase.Incoming);
            if (_activeBtns != null)
                _activeBtns.SetActive(CallVideo.Chasing || CallService.State != CallService.Phase.Incoming);
            if (_muteBtn != null)
                PhoneUi.SetCircleIcon(_muteBtn, PhoneIcons.Material(CallService.Muted ? "mic_off" : "mic"));
            if (_vmBtn != null)
                _vmBtn.SetActive(CallService.State == CallService.Phase.Dialing);
            CallVideo.ApplyUi();
        }

        private void PaintCallFace()
        {
            if (_callFace == null)
                return;
            for (int i = _callFace.transform.childCount - 1; i >= 0; i--)
                UObject.Destroy(_callFace.transform.GetChild(i).gameObject);
            if (CallVideo.Chasing || string.IsNullOrEmpty(CallService.RemoteId))
                return;
            PhoneContacts.CreateAvatar(_callFace.transform, CallService.RemoteId, 64f);
        }

        private void ApplyChrome()
        {
            ApplyChrome(true);
        }

        private void ApplyChrome(bool reloadWallpaper)
        {
            if (_bezelImg != null)
                _bezelImg.color = PhoneUi.Bezel;
            if (reloadWallpaper)
                ApplyWallpaper();
            if (_veil != null)
                _veil.color = new Color(0f, 0f, 0f, Mathf.Clamp01(1f - PhoneTheme.ClampBright(PhoneTheme.Brightness)));
            ApplyPresentation();
            if (_navImg != null)
                _navImg.color = PhoneUi.Nav;
            PaintNavKeys();
            if (_statusImg != null)
                _statusImg.color = new Color(0f, 0f, 0f, 0f);
            SyncAppCover();
            if (_headerImg != null)
                _headerImg.color = PhoneUi.Nav;
            if (_dockImg != null)
                _dockImg.color = new Color(1f, 1f, 1f, 0f);
            if (_statusTime != null)
                _statusTime.color = PhoneUi.ClockText;
            if (_statusRight != null)
                _statusRight.color = PhoneUi.ClockText;
            if (_appTitle != null)
                _appTitle.color = PhoneUi.Text;
            if (_callLayer != null)
            {
                var img = _callLayer.GetComponent<Image>();
                if (img != null)
                    img.color = PhoneUi.Screen;
            }
        }

        private void ApplyWallpaper()
        {
            if (_wallpaperImg == null)
                return;
            _wpApplied = PhoneTheme.WallpaperFile ?? string.Empty;
            StopWallpaper();
            _wallpaperImg.color = Color.white;
            string path = PhoneStore.MediaPath(PhoneTheme.WallpaperFile);
            if (!string.IsNullOrEmpty(path) && File.Exists(path) && path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    PhoneGif.Clip clip = PhoneGif.Decode(File.ReadAllBytes(path), 60);
                    if (clip != null && clip.Frames != null && clip.Frames.Length > 1)
                    {
                        _wpClip = clip;
                        BuildWallpaperSprites(clip);
                        if (_wpSprites != null && _wpSprites.Length > 0)
                            _wallpaperImg.sprite = _wpSprites[0];
                        StartWallpaperPlay();
                        return;
                    }
                    if (clip != null && clip.Frames != null && clip.Frames.Length == 1)
                    {
                        SetWallpaperTex(clip.Frames[0]);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Plugin.LogError("GIF wallpaper failed: " + ex.Message);
                }
            }
            Texture2D tex = PhoneImages.LoadFile(path);
            if (tex != null)
            {
                SetWallpaperTex(tex);
                return;
            }
            _wallpaperImg.sprite = PhoneUi.Wallpaper();
            _wallpaperImg.type = Image.Type.Simple;
        }

        private void SetWallpaperTex(Texture2D tex)
        {
            if (tex == null || _wallpaperImg == null)
                return;
            if (_customWallpaper != null)
                UObject.Destroy(_customWallpaper);
            _customWallpaper = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            _customWallpaper.name = "PiP_CustomWp";
            _wallpaperImg.sprite = _customWallpaper;
            _wallpaperImg.type = Image.Type.Simple;
            _wallpaperImg.preserveAspect = false;
        }

        private void BuildWallpaperSprites(PhoneGif.Clip clip)
        {
            ClearWallpaperSprites();
            if (clip == null || clip.Frames == null)
                return;
            _wpSprites = new Sprite[clip.Frames.Length];
            for (int i = 0; i < clip.Frames.Length; i++)
            {
                Texture2D tex = clip.Frames[i];
                if (tex == null)
                    continue;
                _wpSprites[i] = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        private void StartWallpaperPlay()
        {
            if (_wallpaperImg == null || _wpSprites == null || _wpSprites.Length < 2)
                return;
            if (!isActiveAndEnabled)
                return;
            if (_wallpaperForcedPause || WallpaperCovered())
                return;
            if (_wpPlay != null)
                return;
            _wpPlay = StartCoroutine(PlayWallpaper());
        }

        private IEnumerator PlayWallpaper()
        {
            int i = 0;
            PhoneGif.Clip clip = _wpClip;
            while (_wallpaperImg != null && _wpSprites != null && _wpSprites.Length > 0)
            {
                if (_wpSprites[i] != null)
                    _wallpaperImg.sprite = _wpSprites[i];
                float wait = clip != null && clip.Delays != null && i < clip.Delays.Length ? clip.Delays[i] : 0.1f;
                if (wait < 0.04f)
                    wait = 0.04f;
                i++;
                if (i >= _wpSprites.Length)
                    i = 0;
                yield return new WaitForSecondsRealtime(wait);
            }
        }

        private void ClearWallpaperSprites()
        {
            if (_wpSprites == null)
                return;
            for (int i = 0; i < _wpSprites.Length; i++)
            {
                if (_wpSprites[i] != null)
                    UObject.Destroy(_wpSprites[i]);
            }
            _wpSprites = null;
        }

        private void StopWallpaper()
        {
            if (_wpPlay != null)
            {
                StopCoroutine(_wpPlay);
                _wpPlay = null;
            }
            ClearWallpaperSprites();
            if (_wpClip != null && _wpClip.Frames != null)
            {
                for (int i = 0; i < _wpClip.Frames.Length; i++)
                {
                    if (_wpClip.Frames[i] != null)
                        UObject.Destroy(_wpClip.Frames[i]);
                }
            }
            _wpClip = null;
        }

        private void RebuildHome()
        {
            if (_homeGrid == null || _dock == null)
                return;

            HideIconMenu();
            ScrollRect homeScroll = _homeGrid.GetComponentInParent<ScrollRect>();
            float homeY = homeScroll != null ? homeScroll.verticalNormalizedPosition : 1f;
            ClearChildren(_homeGrid);
            ClearChildren(_dock);
            PhoneStore.EnsureHomeDefaults();

            for (int i = 0; i < PhoneStore.HomeIds.Count; i++)
            {
                PiPhoneApp app;
                if (!PiPhoneApi.TryGetApp(PhoneStore.HomeIds[i], out app) || app == null || !PhoneStore.IsInstalled(app.Id))
                    continue;
                CreateAppIcon(_homeGrid, app, false, false);
            }

            int docked = 0;
            var dockSource = PhoneStore.DockIds.Count > 0 ? PhoneStore.DockIds : null;
            if (dockSource != null)
            {
                for (int i = 0; i < dockSource.Count && docked < 4; i++)
                {
                    PiPhoneApp app;
                    if (!PiPhoneApi.TryGetApp(dockSource[i], out app) || app == null || !PhoneStore.IsInstalled(app.Id))
                        continue;
                    CreateAppIcon(_dock, app, true, false);
                    docked++;
                }
            }
            else
            {
                var apps = PiPhoneApi.Apps;
                for (int i = 0; i < apps.Count && docked < 4; i++)
                {
                    if (apps[i] == null || !apps[i].ShowOnDock || !PhoneStore.IsInstalled(apps[i].Id))
                        continue;
                    CreateAppIcon(_dock, apps[i], true, false);
                    docked++;
                }
            }
            if (homeScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                homeScroll.verticalNormalizedPosition = homeY;
            }
            ApplyChromeInsets();
        }

        private void RebuildDrawer()
        {
            if (_drawerGrid == null)
                return;
            ScrollRect drawerScroll = _drawerGrid.GetComponentInParent<ScrollRect>();
            float drawerY = drawerScroll != null ? drawerScroll.verticalNormalizedPosition : 1f;
            ClearChildren(_drawerGrid);
            PhoneStore.EnsureHomeDefaults();
            var apps = PiPhoneApi.GetApps();
            for (int i = 0; i < apps.Length; i++)
            {
                PiPhoneApp app = apps[i];
                if (app == null || !PhoneStore.IsInstalled(app.Id))
                    continue;
                CreateAppIcon(_drawerGrid, app, false, true);
            }
            if (drawerScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                drawerScroll.verticalNormalizedPosition = drawerY;
            }
        }

        private void CreateAppIcon(Transform parent, PiPhoneApp app, bool dock, bool drawer)
        {
            string id = app.Id;
            var go = new GameObject("App_" + PhoneUi.Sanitize(app.Id), typeof(RectTransform));
            go.transform.SetParent(parent, false);
            PhoneUi.AddVertical(go, 4f, new RectOffset(0, 0, 4, 0));
            var v = go.GetComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.UpperCenter;
            v.childForceExpandWidth = false;
            v.childControlWidth = false;

            var icon = PhoneIcons.CreateView(go.transform, app, 56f, false);
            var iconImg = icon.GetComponent<Image>();
            var btn = icon.gameObject.AddComponent<Button>();
            btn.targetGraphic = iconImg;
            var hold = icon.gameObject.AddComponent<AppIconHold>();
            hold.Menu = this;
            hold.Id = id;
            hold.Dock = dock;
            hold.Drawer = drawer;

            if (!dock)
            {
                var name = PhoneUi.CreateLabel(go.transform, "Name", PhoneLang.AppName(app), 12f, FontStyles.Normal, TextAlignmentOptions.Center);
                name.color = PhoneUi.Text;
                PhoneUi.Size(name.gameObject, 18f, 72f);
                name.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        internal void LaunchApp(string id)
        {
            HideIconMenu();
            OpenAppInternal(id);
        }

        internal void AddToHomeOrDock(string id)
        {
            if (!PhoneStore.IsInstalled(id))
            {
                ShowToast("Install it from Apps first.");
                return;
            }
            if (!PhoneStore.IsOnHome(id))
            {
                PhoneStore.ToggleHome(id);
                RebuildHome();
                ShowToast("Added to home screen. Hold again to dock it.");
                return;
            }
            if (PhoneStore.AddDock(id))
            {
                RebuildHome();
                ShowToast("Added to the dock.");
                return;
            }
            ShowToast(PhoneStore.IsOnDock(id) ? "Already on the dock." : "Dock is full (4 apps).");
        }

        internal void ShowIconMenu(string id, bool dock)
        {
            HideIconMenu();
            if (_homeRoot == null || string.IsNullOrEmpty(id))
                return;
            _iconMenu = new GameObject("IconMenu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _iconMenu.transform.SetParent(_homeRoot.transform, false);
            var img = _iconMenu.GetComponent<Image>();
            img.sprite = PhoneUi.Rounded(16);
            img.color = PhoneUi.Surface;
            PhoneUi.Size(_iconMenu, 44f);
            PhoneUi.AddHorizontal(_iconMenu, 6f);
            var layout = _iconMenu.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.childForceExpandWidth = false;
            PhoneUi.CreateIconChip(_iconMenu.transform, "–", PhoneShade.RemoveIcon(), () =>
            {
                if (dock)
                    PhoneStore.RemoveDock(id);
                else if (PhoneStore.IsOnHome(id))
                    PhoneStore.ToggleHome(id);
                RebuildHome();
            }, false, new Vector2(36f, 36f));
            if (PiPhoneApi.CanUninstall(id))
            {
                PhoneUi.CreateIconChip(_iconMenu.transform, "X", PhoneShade.UninstallIcon(), () =>
                {
                    PiPhoneApi.Uninstall(id);
                    HideIconMenu();
                    RebuildHome();
                    if (_recentsRoot != null && _recentsRoot.activeSelf)
                        RebuildDrawer();
                }, false, new Vector2(36f, 36f));
            }
            PhoneUi.CreateIconChip(_iconMenu.transform, "<", PhoneIcons.Material("chevron_left"), () =>
            {
                if (dock)
                    PhoneStore.MoveDock(id, -1);
                else
                    PhoneStore.MoveHome(id, -1);
                RebuildHome();
                ShowIconMenu(id, dock);
            }, false, new Vector2(36f, 34f));
            PhoneUi.CreateIconChip(_iconMenu.transform, ">", PhoneIcons.Material("chevron_right"), () =>
            {
                if (dock)
                    PhoneStore.MoveDock(id, 1);
                else
                    PhoneStore.MoveHome(id, 1);
                RebuildHome();
                ShowIconMenu(id, dock);
            }, false, new Vector2(36f, 34f));
            if (!dock && !PhoneStore.IsOnDock(id))
            {
                PhoneUi.CreateButton(_iconMenu.transform, "Dock", () =>
                {
                    if (!PhoneStore.AddDock(id))
                        ShowToast("Dock is full (4 apps).");
                    RebuildHome();
                }, new Vector2(64f, 34f));
            }
            PhoneUi.MaterialChip(_iconMenu.transform, "check", "Done", HideIconMenu, new Vector2(36f, 32f));
            PhoneUi.ApplyAllFonts(_iconMenu.transform);
        }

        private void HideIconMenu()
        {
            if (_iconMenu == null)
                return;
            UObject.Destroy(_iconMenu);
            _iconMenu = null;
        }

        private sealed class AppIconHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
        {
            internal PhoneMenu Menu;
            internal string Id;
            internal bool Dock;
            internal bool Drawer;
            private float _downAt;
            private bool _pressed;
            private bool _held;

            public void OnPointerDown(PointerEventData eventData)
            {
                _pressed = true;
                _held = false;
                _downAt = Time.unscaledTime;
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                _pressed = false;
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                if (_held || Menu == null || string.IsNullOrEmpty(Id))
                    return;
                Menu.LaunchApp(Id);
            }

            private void Update()
            {
                if (!_pressed || _held || Menu == null)
                    return;
                if (Time.unscaledTime - _downAt < 0.45f)
                    return;
                _held = true;
                _pressed = false;
                if (Drawer)
                    Menu.AddToHomeOrDock(Id);
                else
                    Menu.ShowIconMenu(Id, Dock);
            }
        }

        private static void ClearChildren(Transform t)
        {
            if (t == null)
                return;
            for (int i = t.childCount - 1; i >= 0; i--)
                UObject.Destroy(t.GetChild(i).gameObject);
        }

        private void Update()
        {
            if (!_visible)
                return;
            if (Time.unscaledTime >= _nextStatus)
            {
                _nextStatus = Time.unscaledTime + 1f;
                RefreshStatus();
            }
            if (_toastUntil > 0f && Time.unscaledTime >= _toastUntil)
            {
                if (_toastRoot != null)
                    _toastRoot.SetActive(false);
                else if (_toast != null)
                    _toast.gameObject.SetActive(false);
                _toastUntil = 0f;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Plugin.CapturingHotkey || Plugin.CaptureEndedFrame == Time.frameCount)
                    return;
                HandleBack();
            }
        }

        private void LateUpdate()
        {
            if (!_visible)
                return;
            ApplyPlayMode();
            ApplyEmoteYield();
        }

        public static void SyncPlayThrough()
        {
            if (_instance == null || !_instance._visible)
                return;
            _instance.ApplyPlayMode();
        }

        private void ApplyPlayMode()
        {
            PiPhonePlayThrough mode = EffectivePlayMode();
            bool move = mode != PiPhonePlayThrough.Off;
            bool cursor = mode == PiPhonePlayThrough.Off || mode == PiPhonePlayThrough.WalkAndCursor;
            bool freezeLook = mode == PiPhonePlayThrough.Off;
            ApplyInputBlock(!move);
            if (cursor)
            {
                if (freezeLook)
                {
                    if (!_hasFrozenLook)
                        CaptureLook();
                    HoldLook();
                }
                else
                {
                    _hasFrozenLook = false;
                }
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                _hasFrozenLook = false;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void ApplyEmoteYield()
        {
            bool wheel = EmoteWheelOpen();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            if (wheel)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        private static bool EmoteWheelOpen()
        {
            try
            {
                GUIManager gui = GUIManager.instance;
                if (gui != null && gui.emoteWheel != null && gui.emoteWheel.activeSelf)
                    return true;
                Character local = Character.localCharacter;
                if (local != null && local.data != null && local.data.usingEmoteWheel)
                    return true;
            }
            catch
            {
            }
            return false;
        }

        public static void SetCameraFill(bool fill)
        {
            SetImmersive(fill);
        }

        public static void SetImmersive(bool immersive)
        {
            if (_instance == null)
                return;
            _instance._immersive = immersive;
            if (_instance._headerImg != null)
                _instance._headerImg.gameObject.SetActive(!immersive);
            if (_instance._appContent != null)
            {
                var layout = _instance._appContent.GetComponent<VerticalLayoutGroup>();
                if (layout != null)
                    layout.padding = immersive ? new RectOffset(0, 0, 0, 0) : new RectOffset(12, 12, 10, 10);
            }
        }

        public static void SetLandscape(bool landscape)
        {
            if (_instance == null)
                return;
            if (_instance._hasFrozenLook)
                _instance.HoldLook();
            _instance._landscape = landscape;
            _instance.ApplyPresentation();
        }

        public static void SetFullscreen(bool fullscreen)
        {
            if (_instance == null)
                return;
            if (_instance._hasFrozenLook)
                _instance.HoldLook();
            _instance._fullscreen = fullscreen;
            if (fullscreen)
                _instance._landscape = true;
            _instance.ApplyPresentation();
        }

        public static void ToggleUserLandscape()
        {
            SetUserLandscape(!IsLandscape);
        }

        public static void SetUserLandscape(bool landscape)
        {
            EnsureCreated();
            if (!_instance._visible)
                _instance.OpenInternal();
            if (_instance._hasFrozenLook)
                _instance.HoldLook();
            _instance._userPinned = true;
            _instance._userLandscape = landscape;
            _instance._landscape = landscape;
            _instance.ApplyPresentation();
            if (_instance._hasFrozenLook)
                _instance.HoldLook();
            if (_instance._shadeRoot != null && _instance._shadeRoot.activeSelf)
                _instance.RebuildShade();
        }

        public static void SetWallpaperPaused(bool paused)
        {
            if (_instance == null)
                return;
            _instance._wallpaperForcedPause = paused;
            _instance.SyncBackgroundWork();
        }

        public static void ApplyPlacement()
        {
            if (_instance == null)
                return;
            _instance.ApplyPresentation();
        }

        public static void RefreshShade()
        {
            if (_instance == null || _instance._shadeRoot == null || !_instance._shadeRoot.activeSelf)
                return;
            _instance.RebuildShade();
        }

        private static void ApplyAppPresentation(PiPhoneApp app)
        {
            if (app == null || _instance == null)
                return;
            if (_instance._userPinned)
            {
                _instance._landscape = _instance._userLandscape;
                _instance._fullscreen = false;
            }
            else
            {
                _instance._landscape = app.Landscape || app.Fullscreen;
                _instance._fullscreen = app.Fullscreen;
            }
            SetImmersive(app.Immersive);
            _instance.ApplyPresentation();
            _instance.SyncAppCover();
        }

        private void ResetAppPresentation()
        {
            SetImmersive(false);
            if (_userPinned)
            {
                _landscape = _userLandscape;
                _fullscreen = false;
            }
            else
            {
                _landscape = false;
                _fullscreen = false;
            }
            ApplyPresentation();
        }

        private void ApplyPresentation()
        {
            if (_bezel == null)
                return;
            if (_hasFrozenLook)
                HoldLook();
            _bezel.localEulerAngles = Vector3.zero;
            _bezel.sizeDelta = _landscape
                ? new Vector2(PhoneHeight, PhoneWidth)
                : new Vector2(PhoneWidth, PhoneHeight);
            float scale = _landscape
                ? Mathf.Clamp(PhoneTheme.PhoneScaleLand, 0.55f, 1.8f)
                : Mathf.Clamp(PhoneTheme.PhoneScale, 0.55f, 1.35f);
            _bezel.localScale = new Vector3(scale, scale, 1f);
            _bezel.anchoredPosition = _landscape
                ? new Vector2(PhoneTheme.PhonePosLandX, PhoneTheme.PhonePosLandY)
                : new Vector2(PhoneTheme.PhonePosX, PhoneTheme.PhonePosY);
            PlaceSideKeys();
            if (_bezelDrag != null)
                _bezelDrag.enabled = !PhoneTheme.PositionLocked;
            ApplyChromeInsets();
            if (_hasFrozenLook)
                HoldLook();
            RaiseOrientationIfChanged();
        }

        private void ApplyChromeInsets()
        {
            bool dockOnScreen = NavPinnedByDock();
            _navDocked = dockOnScreen;
            bool showNav = PhoneTheme.NavBarOn && (dockOnScreen || _navPeek);
            if (_statusBar != null)
                _statusBar.gameObject.SetActive(true);
            if (_statusImg != null)
                _statusImg.color = new Color(0f, 0f, 0f, 0f);
            if (_navRotate != null)
                _navRotate.gameObject.SetActive(PhoneTheme.NavRotateButton);
            if (_navImg != null)
                _navImg.gameObject.SetActive(showNav);
            if (_navHandle != null)
                _navHandle.gameObject.SetActive(PhoneTheme.NavBarOn && !dockOnScreen);
            ApplyNavAxis();
            if (_navHandleLabel != null)
                _navHandleLabel.text = _navPeek ? "v" : "^";
            if (_navHandle != null)
            {
                Image glyph = null;
                Transform g = _navHandle.Find("Glyph");
                if (g != null)
                    glyph = g.GetComponent<Image>();
                if (glyph != null)
                    glyph.sprite = PhoneIcons.Material(_navPeek ? "expand_more" : "expand_less") ?? glyph.sprite;
            }
            if (_dock != null)
            {
                bool showDock = !_landscape && !PhoneTheme.HideDock;
                _dock.gameObject.SetActive(showDock);
                if (showDock)
                    PhoneUi.Size(_dock.gameObject, 88f);
            }
            if (_pagesRt != null)
            {
                _pagesRt.offsetMin = Vector2.zero;
                _pagesRt.offsetMax = Vector2.zero;
            }
            if (_callLayer != null)
            {
                var rt = _callLayer.transform as RectTransform;
                if (rt != null)
                {
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }
            }
            if (_shadeRoot != null)
            {
                var shadeRt = _shadeRoot.transform as RectTransform;
                if (shadeRt != null)
                {
                    PhoneUi.StretchTop(shadeRt, _landscape ? 280f : 420f);
                    shadeRt.anchoredPosition = Vector2.zero;
                }
            }
            ApplyGridLayout();
            RaiseChrome();
        }

        private bool NavPinnedByDock()
        {
            return !_landscape
                && !PhoneTheme.HideDock
                && _homeRoot != null
                && _homeRoot.activeSelf;
        }

        private void PaintNavKeys()
        {
            for (int i = 0; i < _navFills.Count; i++)
            {
                if (_navFills[i] != null)
                    _navFills[i].color = PhoneTheme.NavButtonColor;
            }
            for (int i = 0; i < _navGlyphs.Count; i++)
            {
                if (_navGlyphs[i] != null)
                    _navGlyphs[i].color = PhoneTheme.NavIconColor;
            }
        }

        private void RaiseChrome()
        {
            if (_callLayer != null)
                _callLayer.transform.SetAsLastSibling();
            if (_navImg != null)
                _navImg.transform.SetAsLastSibling();
            if (_navHandle != null)
                _navHandle.SetAsLastSibling();
            if (_callBar != null)
                _callBar.transform.SetAsLastSibling();
            if (_shadeRoot != null)
                _shadeRoot.transform.SetAsLastSibling();
            if (_statusBar != null)
                _statusBar.SetAsLastSibling();
            if (_veil != null)
                _veil.transform.SetAsLastSibling();
            if (_toastRoot != null)
                _toastRoot.transform.SetAsLastSibling();
        }

        private void SyncAppCover()
        {
            if (_appBg == null)
                return;
            bool cover = true;
            PiPhoneApp app;
            if (!string.IsNullOrEmpty(_openAppId) && PiPhoneApi.TryGetApp(_openAppId, out app) && app != null)
                cover = app.CoversWallpaper;
            _appBg.color = cover ? PhoneUi.Screen : new Color(0f, 0f, 0f, 0f);
        }

        private void ApplyGridLayout()
        {
            const float cellW = 72f;
            const float cellH = 92f;
            float width = _landscape ? PhoneHeight - 64f : PhoneWidth - 64f;
            int cols = _landscape ? Mathf.Max(6, Mathf.FloorToInt((width - 8f) / (cellW + 6f))) : 4;
            ApplyGrid(_homeGrid, cols, cellW, cellH);
            ApplyGrid(_drawerGrid, cols, cellW, cellH);
        }

        private static void ApplyGrid(RectTransform gridRt, int cols, float cellW, float cellH)
        {
            if (gridRt == null)
                return;
            var grid = gridRt.GetComponent<GridLayoutGroup>();
            if (grid == null)
                return;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.constraintCount = Mathf.Max(2, cols);
            grid.cellSize = new Vector2(cellW, cellH);
            grid.spacing = new Vector2(6f, 6f);
            float used = cols * cellW + (cols - 1) * 6f;
            float width = cols >= 6 ? PhoneHeight - 64f : PhoneWidth - 64f;
            int pad = Mathf.Max(4, Mathf.FloorToInt((width - used) * 0.5f));
            grid.padding = new RectOffset(pad, pad, 4, 8);
            grid.childAlignment = TextAnchor.UpperLeft;
        }

        private void RaiseOrientationIfChanged()
        {
            if (_landscape == _lastLand && _fullscreen == _lastFull && _immersive == _lastImm)
                return;
            _lastLand = _landscape;
            _lastFull = _fullscreen;
            _lastImm = _immersive;
            if (_raisingOrient)
                return;
            _raisingOrient = true;
            try
            {
                SyncBackgroundWork();
                PiPhoneApp open;
                if (!string.IsNullOrEmpty(_openAppId) && PiPhoneApi.TryGetApp(_openAppId, out open) && open != null && open.OnOrientation != null)
                {
                    try { open.OnOrientation(); }
                    catch (Exception ex) { Plugin.LogError("App '" + _openAppId + "' OnOrientation: " + ex.Message); }
                }
                PiPhoneApi.RaiseOrientation();
                ApplyGridLayout();
            }
            finally
            {
                _raisingOrient = false;
            }
        }

        private void SyncBackgroundWork()
        {
            if (_wallpaperForcedPause || WallpaperCovered())
                PauseWallpaperLoop();
            else
                StartWallpaperPlay();
        }

        private bool WallpaperCovered()
        {
            if (!_visible)
                return true;
            if (_callLayer != null && _callLayer.activeSelf)
                return true;
            if (_appRoot == null || !_appRoot.activeSelf)
                return false;
            PiPhoneApp app;
            if (!string.IsNullOrEmpty(_openAppId) && PiPhoneApi.TryGetApp(_openAppId, out app) && app != null)
                return app.CoversWallpaper;
            return true;
        }

        private void PauseWallpaperLoop()
        {
            if (_wpPlay == null)
                return;
            StopCoroutine(_wpPlay);
            _wpPlay = null;
        }

        internal void NudgePosition(Vector2 canvasDelta)
        {
            if (_bezel == null || PhoneTheme.PositionLocked)
                return;
            Vector2 pos = _bezel.anchoredPosition + canvasDelta;
            _bezel.anchoredPosition = pos;
            if (_landscape)
                PhoneTheme.SetPhonePosLand(pos.x, pos.y);
            else
                PhoneTheme.SetPhonePos(pos.x, pos.y);
        }

        private void ApplyPlayThroughCursor(bool overPhone)
        {
            ApplyInputBlock(overPhone);
            Cursor.lockState = overPhone ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = overPhone;
        }

        private bool PointerOverPhone()
        {
            if (_bezel == null)
                return false;
            Canvas canvas = GetComponent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(_bezel, Input.mousePosition, cam);
        }

        private void PlaceSideKeys()
        {
            if (_bezel == null)
                return;
            PlacePunchHole();
            float halfW = _bezel.sizeDelta.x * 0.5f;
            // Clockwise from portrait: right edge becomes the bottom, left edge becomes the top.
            float side = PhoneWidth * 0.5f + 5f;
            if (_landscape)
            {
                PlaceSideKey(_volUpRt, new Vector2(32f, 7f), new Vector2(280f, -side));
                PlaceSideKey(_volDnRt, new Vector2(32f, 7f), new Vector2(236f, -side));
                PlaceSideKey(_ringerRt, new Vector2(44f, 7f), new Vector2(258f, side));
            }
            else
            {
                PlaceSideKey(_volUpRt, new Vector2(7f, 32f), new Vector2(halfW + 5f, 280f));
                PlaceSideKey(_volDnRt, new Vector2(7f, 32f), new Vector2(halfW + 5f, 236f));
                PlaceSideKey(_ringerRt, new Vector2(7f, 44f), new Vector2(-halfW - 5f, 258f));
            }
        }

        private void PlacePunchHole()
        {
            if (_punchRt == null)
                return;
            _punchRt.sizeDelta = new Vector2(18f, 18f);
            if (_landscape)
            {
                _punchRt.anchorMin = _punchRt.anchorMax = new Vector2(1f, 0.5f);
                _punchRt.pivot = new Vector2(1f, 0.5f);
                _punchRt.anchoredPosition = new Vector2(-18f, 0f);
            }
            else
            {
                _punchRt.anchorMin = _punchRt.anchorMax = new Vector2(0.5f, 1f);
                _punchRt.pivot = new Vector2(0.5f, 1f);
                _punchRt.anchoredPosition = new Vector2(0f, -18f);
            }
        }

        private static void PlaceSideKey(RectTransform rt, Vector2 size, Vector2 pos)
        {
            if (rt == null)
                return;
            rt.gameObject.SetActive(true);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        private void BuildSideButtons()
        {
            if (_bezel == null)
                return;
            _volUpRt = MakeSideKey(_bezel, "VolUp", new Vector2(PhoneWidth * 0.5f + 5f, 280f), new Vector2(7f, 32f), () => NudgeVolume(0.1f)).transform as RectTransform;
            _volDnRt = MakeSideKey(_bezel, "VolDn", new Vector2(PhoneWidth * 0.5f + 5f, 236f), new Vector2(7f, 32f), () => NudgeVolume(-0.1f)).transform as RectTransform;
            _ringerRt = MakeSideKey(_bezel, "Ringer", new Vector2(-PhoneWidth * 0.5f - 5f, 258f), new Vector2(7f, 44f), () =>
            {
                PhoneTheme.CycleRinger();
                PhoneNotify.Quiet("Sound", PhoneTheme.RingerLabel());
            }).transform as RectTransform;
        }

        private static Button MakeSideKey(Transform parent, string name, Vector2 pos, Vector2 size, UnityAction click)
        {
            var rt = PhoneUi.CreateImage(parent, name, PhoneUi.Rounded(4), new Color(0.16f, 0.17f, 0.19f, 1f));
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = rt.GetComponent<Image>();
            btn.colors = PhoneUi.TintColors();
            btn.onClick.AddListener(click);
            return btn;
        }

        private static void NudgeVolume(float delta)
        {
            if (MusicPlayer.HasTrack)
            {
                PhoneTheme.SetMusicVolume(PhoneTheme.MusicVolume + delta);
                PhoneNotify.Quiet("Music", Mathf.RoundToInt(PhoneTheme.MusicVolume * 100f) + "%");
                return;
            }
            PhoneTheme.SetRingVolume(PhoneTheme.RingVolume + delta);
            PhoneNotify.Quiet("Volume", Mathf.RoundToInt(PhoneTheme.RingVolume * 100f) + "%");
        }

        private void BuildShade(RectTransform screen)
        {
            var shade = PhoneUi.CreateImage(screen, "Shade", PhoneUi.White(), new Color(0.08f, 0.09f, 0.11f, 0.96f));
            PhoneUi.StretchTop(shade, 420f);
            shade.anchoredPosition = Vector2.zero;
            shade.GetComponent<Image>().raycastTarget = true;
            _shadeRoot = shade.gameObject;
            _shadeRoot.SetActive(false);
            PhoneUi.AddVertical(_shadeRoot, 8f, new RectOffset(16, 16, 40, 12));
        }

        private void ToggleShade()
        {
            if (_shadeRoot == null)
                return;
            bool open = !_shadeRoot.activeSelf;
            _shadeRoot.SetActive(open);
            if (open)
                RebuildShade();
        }

        private void RebuildShade()
        {
            if (_shadeRoot == null)
                return;
            EnsureShadeButtons();
            for (int i = _shadeRoot.transform.childCount - 1; i >= 0; i--)
                UObject.Destroy(_shadeRoot.transform.GetChild(i).gameObject);

            PhoneStore.MarkNoticesSeen();
            var title = PhoneUi.CreateLabel(_shadeRoot.transform, "T", "Quick settings", 16f, FontStyles.Normal, TextAlignmentOptions.Center);
            PhoneUi.Size(title.gameObject, 22f);

            if (PhoneShade.SizeOpen)
            {
                if (_landscape)
                    PhoneUi.CreateSliderRow(_shadeRoot.transform, "Size", 0.55f, 1.8f, PhoneTheme.PhoneScaleLand, v => PhoneTheme.SetPhoneScaleLand(v));
                else
                    PhoneUi.CreateSliderRow(_shadeRoot.transform, "Size", 0.55f, 1.35f, PhoneTheme.PhoneScale, v => PhoneTheme.SetPhoneScale(v));
            }
            PhoneUi.CreateSliderRow(_shadeRoot.transform, "Bright", PhoneTheme.MinBrightness, 1f, PhoneTheme.Brightness, v => PhoneTheme.SetBrightness(v));
            PhoneUi.CreateSliderRow(_shadeRoot.transform, "Ringer", 0f, 1f, PhoneTheme.RingVolume, v => PhoneTheme.SetRingVolume(v));
            if (PhoneStore.IsInstalled(BuiltinApps.SoundsId))
            {
                PhoneUi.CreateSliderRow(_shadeRoot.transform, "Music", 0f, 1f, PhoneTheme.MusicVolume, v => PhoneTheme.SetMusicVolume(v));
                var musicRow = new GameObject("MusicBtns", typeof(RectTransform));
                musicRow.transform.SetParent(_shadeRoot.transform, false);
                PhoneUi.Size(musicRow, PhoneShade.MediaChip);
                PhoneUi.AddHorizontal(musicRow, 6f);
                var musicLayout = musicRow.GetComponent<HorizontalLayoutGroup>();
                musicLayout.childForceExpandWidth = false;
                PhoneUi.CreateIconChip(musicRow.transform, MusicPlayer.Playing ? "||" : ">", PhoneShade.PlayIcon(MusicPlayer.Playing), () =>
                {
                    MusicPlayer.Toggle();
                    RebuildShade();
                }, false, new Vector2(PhoneShade.MediaChip, PhoneShade.MediaChip));
                PhoneUi.CreateIconChip(musicRow.transform, ">|", PhoneShade.NextIcon(), () =>
                {
                    MusicPlayer.Next();
                    RebuildShade();
                }, false, new Vector2(PhoneShade.MediaChip, PhoneShade.MediaChip));
                var track = PhoneUi.CreateLabel(musicRow.transform, "Track", MusicPlayer.HasTrack ? (MusicPlayer.CurrentName ?? "Now playing") : "No track", 13f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                track.overflowMode = TextOverflowModes.Ellipsis;
                var trackLe = track.gameObject.AddComponent<LayoutElement>();
                trackLe.flexibleWidth = 1f;
                trackLe.minWidth = 80f;
            }

            PhoneShade.DrawTiles(_shadeRoot.transform);

            int shown = 0;
            for (int i = 0; i < PhoneStore.Notices.Count && shown < 8; i++)
            {
                NoticeItem n = PhoneStore.Notices[i];
                if (n == null)
                    continue;
                NoticeItem captured = n;
                var row = new GameObject("N" + i, typeof(RectTransform));
                row.transform.SetParent(_shadeRoot.transform, false);
                PhoneUi.Size(row, 26f);
                PhoneUi.AddHorizontal(row, 6f);
                string line = n.Title + (string.IsNullOrEmpty(n.Body) ? string.Empty : " — " + n.Body);
                var lab = PhoneUi.CreateLabel(row.transform, "T", line, 13f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                lab.color = PhoneUi.TextDim;
                lab.overflowMode = TextOverflowModes.Ellipsis;
                lab.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                PhoneUi.CreateIconChip(row.transform, "X", PhoneIcons.Material("delete"), () =>
                {
                    PhoneStore.DeleteNotice(captured.Id);
                    RebuildShade();
                }, false, new Vector2(28f, 24f));
                shown++;
            }
            if (shown == 0)
            {
                var empty = PhoneUi.CreateLabel(_shadeRoot.transform, "Empty", "No notifications yet.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                empty.color = PhoneUi.TextDim;
                PhoneUi.Size(empty.gameObject, 22f);
            }
        }

        private void EnsureShadeButtons()
        {
            if (_shadeReady)
                return;
            _shadeReady = true;
            PhoneShade.Register(new PiPhoneShadeButton
            {
                Id = PhoneShade.LockId,
                Label = "Lock position",
                SortOrder = 10,
                IconFn = () => PhoneShade.LockIcon(PhoneTheme.PositionLocked),
                IsActive = () => PhoneTheme.PositionLocked,
                OnClick = () =>
                {
                    PhoneTheme.SetPositionLocked(!PhoneTheme.PositionLocked);
                    if (_bezelDrag != null)
                        _bezelDrag.enabled = !PhoneTheme.PositionLocked;
                    RebuildShade();
                }
            });
            PhoneShade.Register(new PiPhoneShadeButton
            {
                Id = PhoneShade.SizeId,
                Label = "Size",
                SortOrder = 20,
                IconFn = () => PhoneShade.SizeIcon(),
                IsActive = () => PhoneShade.SizeOpen,
                OnClick = () =>
                {
                    PhoneShade.SizeOpen = !PhoneShade.SizeOpen;
                    RebuildShade();
                }
            });
            PhoneShade.Register(new PiPhoneShadeButton
            {
                Id = PhoneShade.DndId,
                Label = "Do not disturb",
                SortOrder = 30,
                IconFn = () => PhoneShade.DndIcon(),
                IsActive = () => PhoneTheme.DoNotDisturb,
                OnClick = () =>
                {
                    PhoneTheme.SetDoNotDisturb(!PhoneTheme.DoNotDisturb);
                    RebuildShade();
                }
            });
            PhoneShade.Register(new PiPhoneShadeButton
            {
                Id = PhoneShade.RingerId,
                Label = "Ringer",
                SortOrder = 40,
                IconFn = () => PhoneShade.RingerIcon(PhoneTheme.RingerMode),
                OnClick = () =>
                {
                    PhoneTheme.CycleRinger();
                    RebuildShade();
                }
            });
            PhoneShade.Register(new PiPhoneShadeButton
            {
                Id = PhoneShade.RotateId,
                Label = "Rotate",
                SortOrder = 50,
                IconFn = () => PhoneShade.RotateIcon(),
                IsActive = () => _landscape,
                OnClick = ToggleUserLandscape
            });
            PhoneShade.Register(new PiPhoneShadeButton
            {
                Id = PhoneShade.NavId,
                Label = "Navigation",
                SortOrder = 55,
                CanHide = false,
                IconFn = () => PhoneIcons.Material("menu"),
                IsActive = () => PhoneTheme.NavBarOn,
                OnClick = () => PhoneTheme.SetNavBar(!PhoneTheme.NavBarOn)
            });
            PhoneShade.Register(new PiPhoneShadeButton
            {
                Id = PhoneShade.HomeId,
                Label = "Home",
                SortOrder = 60,
                IconFn = () => PhoneShade.HomeIcon(),
                Available = () => !string.IsNullOrEmpty(_openAppId),
                OnClick = () =>
                {
                    ShowHome();
                    RebuildShade();
                }
            });
        }

        private static string CarrierLabel(PiPhoneCarrier carrier)
        {
            if (carrier == PiPhoneCarrier.None)
                return "No SIM";
            if (carrier == PiPhoneCarrier.Edge)
                return "E";
            if (carrier == PiPhoneCarrier.ThreeG)
                return "3G";
            if (carrier == PiPhoneCarrier.FiveG)
                return "5G";
            return "LTE";
        }

        private void RefreshStatus()
        {
            if (_statusTime != null)
                _statusTime.text = PhoneTheme.FormatStatusClock();
            if (_statusRight != null)
            {
                int n = PiPhoneApi.SignalBars;
                string bars = n >= 4 ? "▂▄▆█" : n == 3 ? "▂▄▆_" : n == 2 ? "▂▄__" : n == 1 ? "▂___" : "____";
                string net = CarrierLabel(PiPhoneApi.Carrier);
                if (PhoneTheme.RingerMode == 1)
                    net = "Vib";
                else if (PhoneTheme.RingerMode == 2)
                    net = "Sil";
                int battery = PiPhoneApi.BatteryPercent;
                string charge = PiPhoneApi.BatteryCharging ? "+" : "";
                int unseen = PhoneStore.UnseenNoticeCount();
                _statusRight.text = (unseen > 0 ? unseen + "  " : "") + bars + " " + net + "  " + battery + charge + "%";
            }
        }

        private void CaptureLook()
        {
            Character local = Character.localCharacter;
            if (local == null || local.data == null)
                return;
            _frozenLook = local.data.lookValues;
            _hasFrozenLook = true;
        }

        private void HoldLook()
        {
            if (!_hasFrozenLook)
                return;
            Character local = Character.localCharacter;
            if (local == null || local.data == null)
                return;
            local.data.lookValues = _frozenLook;
        }

        private static void ApplyInputBlock(bool block)
        {
            GUIManager gui = GUIManager.instance;
            if (gui == null)
                return;
            gui.windowBlockingInput = block;
            gui.windowShowingCursor = block;
        }

        internal static void InjectPauseButton(PauseMenuMainPage page)
        {
            if (page == null || !Plugin.GetShowPauseMenuButton())
                return;
            try
            {
                Button donor = PhoneUi.GetPauseMenuDonor(page);
                if (donor == null)
                    return;
                Transform parent = donor.transform.parent;
                Transform existing = parent.Find(PauseChipName);
                GameObject buttonGo = existing != null ? existing.gameObject : null;
                if (buttonGo == null)
                {
                    buttonGo = Instantiate(donor.gameObject, parent, false);
                    buttonGo.name = PauseChipName;
                    foreach (var loc in buttonGo.GetComponentsInChildren<LocalizedText>(true))
                        Destroy(loc);
                    var tmp = buttonGo.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (tmp != null)
                        tmp.text = "PiPhone";
                    var rt = buttonGo.GetComponent<RectTransform>();
                    var donorRt = donor.GetComponent<RectTransform>();
                    if (rt != null && donorRt != null)
                        rt.anchoredPosition = donorRt.anchoredPosition + new Vector2(0f, -56f);
                }
                var button = buttonGo.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(Toggle);
                }
                buttonGo.SetActive(Plugin.GetShowPauseMenuButton());
            }
            catch (Exception ex)
            {
                Plugin.LogError("Pause button inject failed: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(GUIManager), nameof(GUIManager.UpdateWindowStatus))]
        private static class Patch_UpdateWindowStatus
        {
            private static bool _logged;

            private static void Postfix(GUIManager __instance)
            {
                try
                {
                    if (__instance == null || !IsOpen)
                        return;
                    __instance.windowBlockingInput = !WantsPlayThrough;
                    __instance.windowShowingCursor = WantsPlayCursor;
                }
                catch (Exception ex)
                {
                    if (_logged)
                        return;
                    _logged = true;
                    Plugin.LogError("Window status patch failed: " + ex.Message);
                }
            }
        }

        [HarmonyPatch(typeof(CharacterInput), nameof(CharacterInput.Sample))]
        private static class Patch_SamplePtt
        {
            private static bool _logged;

            private static void Postfix(CharacterInput __instance, bool playerMovementActive)
            {
                try
                {
                    if (playerMovementActive || !IsOpen || __instance == null)
                        return;
                    if (CharacterInput.push_to_talk == null)
                        return;
                    __instance.pushToTalkPressed = CharacterInput.push_to_talk.IsPressed();
                }
                catch (Exception ex)
                {
                    if (_logged)
                        return;
                    _logged = true;
                    Plugin.LogError("PTT sample patch failed: " + ex.Message);
                }
            }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.CanDoInput))]
        private static class Patch_CanDoInput
        {
            private static bool _logged;

            private static bool Prefix(ref bool __result)
            {
                try
                {
                    if (IsOpen && !WantsPlayThrough)
                    {
                        __result = false;
                        return false;
                    }
                }
                catch
                {
                }
                return true;
            }

            private static void Postfix(ref bool __result)
            {
                try
                {
                    if (IsOpen && !WantsPlayThrough)
                        __result = false;
                }
                catch (Exception ex)
                {
                    if (_logged)
                        return;
                    _logged = true;
                    Plugin.LogError("Input patch failed: " + ex.Message);
                }
            }
        }

        [HarmonyPatch(typeof(CursorHandler), "Update")]
        private static class Patch_CursorHandler
        {
            private static bool _logged;

            private static void Postfix()
            {
                try
                {
                    if (!IsOpen)
                        return;
                    if (EmoteWheelOpen())
                    {
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                        return;
                    }
                    if (WantsPlayCursor)
                    {
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                        return;
                    }
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                catch (Exception ex)
                {
                    if (_logged)
                        return;
                    _logged = true;
                    Plugin.LogError("Cursor patch failed: " + ex.Message);
                }
            }
        }

        private sealed class NavHandleDrag : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerClickHandler
        {
            internal PhoneMenu Menu;
            private Vector2 _start;
            private bool _dragged;

            public void OnPointerDown(PointerEventData eventData)
            {
                _dragged = false;
                if (eventData != null)
                    _start = eventData.position;
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (Menu == null || eventData == null)
                    return;
                if (Vector2.Distance(_start, eventData.position) > 8f)
                    _dragged = true;
                RectTransform parent = transform.parent as RectTransform;
                if (parent == null)
                    return;
                Canvas canvas = Menu.GetComponent<Canvas>();
                Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                Vector2 local;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, cam, out local))
                    return;
                Menu.MoveNavHandle(local, false);
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                if (Menu == null)
                    return;
                if (_dragged)
                {
                    PhoneTheme.RememberNavPlace(PhoneTheme.NavEdge, PhoneTheme.NavHandleX);
                    _dragged = false;
                    return;
                }
                Menu.ToggleNavPeek();
            }
        }

        private sealed class BezelDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
        {
            internal PhoneMenu Menu;

            public void OnBeginDrag(PointerEventData eventData)
            {
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (Menu == null || eventData == null || !enabled)
                    return;
                Canvas canvas = Menu.GetComponent<Canvas>();
                float scale = canvas != null ? canvas.scaleFactor : 1f;
                if (scale < 0.01f)
                    scale = 1f;
                Menu.NudgePosition(eventData.delta / scale);
            }
        }

        [HarmonyPatch(typeof(PauseMenuMainPage), "Start")]
        private static class Patch_PauseStart
        {
            private static void Postfix(PauseMenuMainPage __instance)
            {
                InjectPauseButton(__instance);
            }
        }

        [HarmonyPatch(typeof(PauseMenuMainPage), "OnEnable")]
        private static class Patch_PauseEnable
        {
            private static void Postfix(PauseMenuMainPage __instance)
            {
                InjectPauseButton(__instance);
            }
        }
    }
}
