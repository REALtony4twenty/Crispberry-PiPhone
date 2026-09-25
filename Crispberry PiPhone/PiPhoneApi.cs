using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// Public entry point for other mods that want to add apps to the in-game phone.
    ///
    /// Hard dependency:
    ///   [BepInDependency(PiPhoneApi.PluginGuid)]
    ///   PiPhoneApi.RegisterApp(new PiPhoneApp {
    ///       Id = "mymod.snake",
    ///       DisplayName = "Snake",
    ///       IconGlyph = "S",
    ///       IconBackground = new Color(0.2f, 0.65f, 0.35f),
    ///       OnOpen = host => { /* build uGUI under host.Content */ }
    ///   });
    ///   // Custom icon (PNG/JPG). Decoded then clamped to 128px:
    ///   app.SetIcon(File.ReadAllBytes("icon.png"));
    ///   // or app.SetIconFile(path); or app.IconSprite = PiPhoneApi.CreateIcon(tex);
    ///
    /// Soft dependency:
    ///   [BepInDependency(PiPhoneApi.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    ///   if (PiPhoneApi.IsReady) PiPhoneApi.RegisterApp(...);
    ///
    /// Build your app UI under <see cref="IPiPhoneHost.Content"/> with Unity uGUI
    /// (or <see cref="PhoneUi"/> helpers so it matches the Android chrome).
    ///
    /// Apps (the store app, id pip.store): registered apps appear there first and
    /// are not installed until the player taps Install (or you set
    /// <see cref="PiPhoneApp.Preinstalled"/>). Third-party mods should leave
    /// Preinstalled false. Home / All apps only list installed apps. Players can
    /// uninstall from Apps (except Sticky apps).
    ///
    /// Home screen: <see cref="PiPhoneApp.ShowOnHome"/> (default true) places the app
    /// on the home grid when it is installed, including a reinstall. Players can
    /// remove it later by holding the icon. Use <see cref="AddToHome"/> if you need
    /// to pin it yourself. Press-and-hold in All apps also adds it. Do not add your
    /// own "home screen settings" page for that.
    ///
    /// Photos: use <see cref="PhoneUi.FitContained"/> so stills and video keep their
    /// aspect ratio (letterboxed, never stretched).
    ///
    /// Notifications: <see cref="Notify"/> posts a banner (and a HUD strip if the phone
    /// is closed). Set <see cref="PiPhoneApp.PostsNotices"/> false for games. Drop WAV/OGG
    /// files in <see cref="AlertsFolder"/> for custom ringtones/text/app alerts. Players
    /// can always reset an app or contact back to the Settings defaults.
    ///
    /// Startup and power: <see cref="SetStartup"/> runs once the first time the phone
    /// opens each launch. <see cref="SetPowerOn"/> / <see cref="SetPowerOff"/> cover
    /// later power cycles. <see cref="PiPhoneApp.BuildLoading"/> plus
    /// <see cref="PiPhoneApp.LoadingSeconds"/> cover one app opening. Each stage is a
    /// rectangle and a duration. Parent pictures, GIFs, video, and audio yourself.
    /// <see cref="FinishStage"/> ends the wait early. <see cref="PhoneUi.CreateHorizontalScroll"/>
    /// is a left-to-right flick gallery that stays inside the phone width.
    ///
    /// Open with data: <see cref="OpenApp(string, object)"/> passes a payload (a thread id,
    /// an item, anything). Read it from <see cref="IPiPhoneHost.Payload"/> inside <see cref="PiPhoneApp.OnOpen"/>.
    /// <see cref="PiPhoneApp.Tick"/> runs every frame even while the phone is closed.
    /// <see cref="SetStatusIcon"/> puts a small icon just left of the battery. Use your plugin GUID in the id.
    ///
    /// Hover text: <see cref="PiPhoneApp.Tooltip"/> on the home, dock, and All apps icon.
    /// Empty falls back to <see cref="PiPhoneApp.Description"/>. <see cref="PhoneUi.SetTooltip"/>
    /// sets hover text on any button or icon. Shade chips use
    /// <see cref="PiPhoneShadeButton.Tooltip"/> (or <see cref="PiPhoneShadeButton.Label"/>).
    /// Nav icons use <see cref="PiPhoneNavButton.Tooltip"/>.
    ///
    /// Presentation: set <see cref="PiPhoneApp.Landscape"/> (or <see cref="PiPhoneApp.Fullscreen"/>,
    /// kept as an alias) so the phone turns on its side. The bezel keeps the same
    /// 420×860 shape, swapped to 860×420, and uses the landscape size slider — it does
    /// not stretch to fill the monitor. <see cref="SetPhoneScale"/>, <see cref="SetPhonePos"/>,
    /// and <see cref="SetPhoneOrientation"/> remember the player's size, position, and
    /// orientation on the first call. <see cref="RestorePhonePlacement"/> puts that
    /// snapshot back. Those overrides are not written into the player's saved settings.
    /// <see cref="PiPhoneApp.Immersive"/> hides the
    /// in-app title bar. Players can also rotate any time (shade Wide/Tall or the
    /// landscape keybind); that pins landscape until they rotate back or close the
    /// phone. Status bar and nav bar float over the app. The nav bar does not reserve
    /// a strip. A draggable <c>^</c> handle shows and hides Back, Home, All apps, and
    /// Rotate. With the dock on, in portrait, the nav stays above the dock. The dock
    /// itself stays hidden in landscape.
    /// Closing the phone always returns to portrait. Subscribe to
    /// <see cref="OrientationChanged"/> or set <see cref="PiPhoneApp.OnOrientation"/>
    /// and rebuild under <see cref="IPiPhoneHost.Content"/> using <see cref="IsLandscape"/>
    /// / <see cref="PhoneUi.SplitIfLandscape"/> / <see cref="PhoneUi.ApplyMediaGrid"/>.
    /// Animated wallpaper keeps playing on Home and All apps. It pauses only when an
    /// open app covers it (<see cref="PiPhoneApp.CoversWallpaper"/>, default true) or
    /// you call <see cref="SetWallpaperPaused"/>. Set
    /// <see cref="PiPhoneApp.ApplyPhoneFonts"/> false if your UI already has its own look.
    /// Return true from <see cref="PiPhoneApp.OnBack"/> to eat Back/Escape (popups) before
    /// the phone goes home.
    ///
    /// Keybinds: <see cref="RegisterKeybind"/> adds a row in Settings → Controls.
    /// Use your plugin GUID as the id prefix. Players recapture keys there.
    /// Poll with <see cref="KeyDown"/> or set <see cref="PiPhoneKeybind.OnPressed"/>.
    ///
    /// Shade toolbar: <see cref="RegisterShadeButton"/> adds a compact icon chip to
    /// Quick settings (pull the status bar). Players can hide any extra chip in
    /// Settings → Customize → Toolbar. Use your plugin GUID as the id prefix.
    ///
    /// Nav bar: <see cref="RegisterNavButton"/> adds an icon to the right of Rotate.
    /// Back, Home, All apps, and Rotate stay first. Use your plugin GUID as the id prefix.
    /// If that row would run off the side of the screen, the icons wrap onto another row.
    /// On the side edges, a column that would leave the screen gains another column.
    /// Quick settings has a Navigation chip that hides and shows the bar.
    /// Material icons follow the filled or outline choice in Look. Full-color pictures
    /// (emoji, emote scouts) stay untinted.
    ///
    /// Dialer numbers: <see cref="RegisterNumber"/> lets a mod handle a digit string
    /// (like PiPhone's 911 rescue). Return true from <see cref="PiPhoneNumber.OnCall"/>
    /// if you handled it.
    ///
    /// Screen cast: <see cref="RegisterCastDevice"/> adds a screen the cast chip can use.
    /// Built-in screens are the airport flight boards under Map. One phone owns a
    /// screen at a time. Other players in the room see that phone on the screen.
    /// The picture keeps the phone's aspect unless the open app sets
    /// <see cref="PiPhoneApp.CastAspectWidth"/> or calls <see cref="SetCastAspect"/>.
    ///
    /// Networking: texts/calls use Photon RaiseEvent byte 185 with magic
    /// <see cref="PluginGuid"/>. Pick a different byte if you raise your own events.
    ///
    /// Scouts: <see cref="GetScouts"/> / <see cref="TryGetScout"/> snapshot a
    /// character's life state (conscious, passed out, fully passed out, dead, ghost),
    /// death timer, fog/water, who is carrying whom, and world/voice position.
    /// <see cref="PiPhoneScout.Id"/> matches Messages thread ids.
    ///
    /// Play-through: <see cref="SetPlayThrough"/> / <see cref="TogglePlayThrough"/>
    /// lets the player walk with the phone open. Built-in apps leave this on.
    /// Other mods may set <see cref="PiPhoneApp.AllowPlayThrough"/> false.
    ///
    /// Voice filters: <see cref="TryGetVoiceFilter"/> / <see cref="SetVoiceFilter"/>
    /// hold the vanilla low-pass, high-pass, reverb, and echo on that remote scout
    /// (listener-side only). <see cref="ClearVoiceFilter"/> gives PEAK the voice back.
    /// An active call with that scout uses PiPhone's call tone until hangup.
    /// Presets: <see cref="PiPhoneVoiceFilter.Dry"/>, <see cref="PiPhoneVoiceFilter.Realm"/>.
    ///
    /// Contacts: keyed by Photon UserId (Steam id on PEAK). See
    /// <see cref="SetContactName"/> / <see cref="SetContactPhoto"/> /
    /// <see cref="TryGetContact"/>. Incoming calls and texts use the custom name
    /// when set; the live nick stays on <see cref="PiPhoneContact.RealName"/>.
    ///
    /// Look: there are no built-in theme packs. Set each color and radius
    /// yourself, or write a themes app that calls <see cref="SetLook"/> /
    /// <see cref="SetScreenColor"/> / <see cref="SetButtonRadius"/> etc.
    /// Players can reset Look / Buttons / Case / Clock from Settings.
    ///
    /// Language: built-in English, Spanish, and French cover game chrome and
    /// app names. Other mods add packs with <see cref="RegisterLanguage"/>
    /// (or drop key=value .txt files in the phone's lang folder). Look up
    /// strings with <see cref="T(string, string)"/>. App titles use
    /// <c>app.{id}</c>, e.g. <c>app.mymod.snake</c>.
    ///
    /// Service / battery: <see cref="SetBattery"/> / <see cref="SetCarrier"/> write
    /// the status-bar meters. Nothing drains or changes them unless a mod (or a
    /// later PiPhone update) calls those setters.
    ///
    /// UI helpers: <see cref="PhoneUi"/> is public so third-party apps match the chrome.
    /// Icons do not have to come from Google. <see cref="PiPhoneApp.SetIcon"/>,
    /// <see cref="CreateIcon(byte[])"/>, and <see cref="CreateIconButton(Transform, Sprite, UnityAction, Vector2)"/>
    /// take any PNG, JPG, or <see cref="Sprite"/> you made. The phone supplies the
    /// button shape, size, and icon mask. <see cref="PhoneIcons.Material"/> is optional
    /// and only the Google Material set shipped with this mod (Apache-2.0).
    /// Pass <c>tintIcon: false</c> to <see cref="PhoneUi.CreateIconChip"/> when your
    /// picture already has its own colors.
    /// </summary>
    public static class PiPhoneApi
    {
        public const string PluginGuid = Plugin.PluginGuid;
        public const string OsName = Plugin.OsName;

        /// <summary>e.g. "Crispberry OS 0.14.0".</summary>
        public static string OsVersion
        {
            get { return Plugin.OsVersionLabel; }
        }

        internal static readonly List<PiPhoneApp> Apps = new List<PiPhoneApp>(16);
        internal static readonly List<PiPhoneStatusSlot> StatusIcons = new List<PiPhoneStatusSlot>(4);
        private static readonly HashSet<string> TickFailures = new HashSet<string>();

        public static event Action AppsChanged;

        /// <summary>Fired after the phone switches portrait / landscape. Rebuild your UI from <see cref="IsLandscape"/>.</summary>
        public static event Action OrientationChanged;

        public static bool IsReady
        {
            get { return Plugin.Instance != null; }
        }

        public static bool IsOpen
        {
            get { return PhoneMenu.IsOpen; }
        }

        /// <summary>
        /// False while passed out or dead. Ghosts can use the phone.
        /// </summary>
        public static bool CanUsePhone
        {
            get { return PhoneMenu.CanUsePhone(); }
        }

        /// <summary>True when the player can walk/look with the phone still open.</summary>
        public static bool IsPlayThrough
        {
            get { return PhoneMenu.IsPlayThrough; }
        }

        public static PiPhonePlayThrough PlayThroughMode
        {
            get { return PhoneMenu.PlayThroughMode; }
        }

        /// <summary>False if the current app set <see cref="PiPhoneApp.AllowPlayThrough"/> to false.</summary>
        public static bool PlayThroughAllowed
        {
            get { return PhoneMenu.PlayThroughAllowed(); }
        }

        public static void SetPlayThrough(bool on)
        {
            PhoneMenu.SetPlayThrough(on);
        }

        public static void SetPlayThrough(PiPhonePlayThrough mode)
        {
            PhoneMenu.SetPlayThrough(mode);
        }

        public static void TogglePlayThrough()
        {
            PhoneMenu.TogglePlayThrough();
        }

        /// <summary>Id of the app currently on screen, or null on the home / All apps pages.</summary>
        public static string CurrentAppId
        {
            get { return PhoneMenu.CurrentAppId; }
        }

        public static void Open()
        {
            PhoneMenu.Open();
        }

        public static void Close()
        {
            PhoneMenu.Close();
        }

        public static void Toggle()
        {
            PhoneMenu.Toggle();
        }

        public static void GoHome()
        {
            PhoneMenu.GoHome();
        }

        public static void GoBack()
        {
            PhoneMenu.GoBack();
        }

        public static bool OpenApp(string id)
        {
            return OpenApp(id, null);
        }

        /// <summary>
        /// Open an installed app and hand it <paramref name="payload"/>.
        /// The app reads it from <see cref="IPiPhoneHost.Payload"/> in <see cref="PiPhoneApp.OnOpen"/>.
        /// A normal open from the home screen passes null.
        /// </summary>
        public static bool OpenApp(string id, object payload)
        {
            return PhoneMenu.OpenApp(id, payload);
        }

        /// <summary>
        /// Show a small icon just left of the battery. Same id replaces the icon.
        /// A null <paramref name="icon"/> clears it. The sprite is not tinted.
        /// </summary>
        public static void SetStatusIcon(string id, Sprite icon, Action onClick = null)
        {
            if (string.IsNullOrEmpty(id))
                return;
            if (icon == null)
            {
                ClearStatusIcon(id);
                return;
            }
            for (int i = 0; i < StatusIcons.Count; i++)
            {
                if (StatusIcons[i] != null && StatusIcons[i].Id == id)
                {
                    StatusIcons[i].Icon = icon;
                    StatusIcons[i].OnClick = onClick;
                    PhoneMenu.RefreshStatusIcons();
                    return;
                }
            }
            StatusIcons.Add(new PiPhoneStatusSlot { Id = id, Icon = icon, OnClick = onClick });
            PhoneMenu.RefreshStatusIcons();
        }

        /// <summary>Remove a status icon added with <see cref="SetStatusIcon"/>.</summary>
        public static void ClearStatusIcon(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            for (int i = StatusIcons.Count - 1; i >= 0; i--)
            {
                if (StatusIcons[i] != null && StatusIcons[i].Id == id)
                    StatusIcons.RemoveAt(i);
            }
            PhoneMenu.RefreshStatusIcons();
        }

        internal static void TickApps()
        {
            for (int i = 0; i < Apps.Count; i++)
            {
                PiPhoneApp app = Apps[i];
                if (app == null || app.Tick == null || string.IsNullOrEmpty(app.Id))
                    continue;
                if (TickFailures.Contains(app.Id))
                    continue;
                try
                {
                    app.Tick();
                }
                catch (Exception ex)
                {
                    TickFailures.Add(app.Id);
                    Plugin.LogError("App '" + app.Id + "' tick failed: " + ex.Message);
                }
            }
        }

        public static bool IsLandscape
        {
            get { return PhoneMenu.IsLandscape; }
        }

        public static bool IsFullscreen
        {
            get { return PhoneMenu.IsFullscreen; }
        }

        public static bool IsImmersive
        {
            get { return PhoneMenu.IsImmersive; }
        }

        /// <summary>Switch the bezel to landscape (same phone shape, landscape size). Does not pin the player's rotate toggle.</summary>
        public static void SetLandscape(bool landscape)
        {
            PhoneMenu.SetLandscape(landscape);
        }

        /// <summary>Same as <see cref="SetLandscape"/> for older mods. The phone keeps its shape and does not fill the monitor.</summary>
        public static void SetFullscreen(bool fullscreen)
        {
            PhoneMenu.SetFullscreen(fullscreen);
        }

        /// <summary>
        /// True after a mod has moved, resized, or rotated the phone.
        /// <see cref="OriginalPhonePlacement"/> is the player's placement from before that change.
        /// </summary>
        public static bool HasPhonePlacementOverride
        {
            get { return _placementHeld; }
        }

        /// <summary>
        /// The player's size, screen position, and orientation.
        /// While <see cref="HasPhonePlacementOverride"/> is set, this is the snapshot from before the override.
        /// </summary>
        public static PiPhonePlacement OriginalPhonePlacement
        {
            get { return _placementHeld ? CopyPlacement(_originalPlacement) : CapturePhonePlacement(); }
        }

        /// <summary>Live size, position, and orientation.</summary>
        public static PiPhonePlacement CurrentPhonePlacement
        {
            get { return CapturePhonePlacement(); }
        }

        /// <summary>Portrait size. The first call remembers the player's placement. Does not write their saved settings.</summary>
        public static void SetPhoneScale(float value)
        {
            RememberPhonePlacement();
            PhoneTheme.SetPhoneScale(value);
        }

        /// <summary>Landscape size. The first call remembers the player's placement. Does not write their saved settings.</summary>
        public static void SetPhoneScaleLand(float value)
        {
            RememberPhonePlacement();
            PhoneTheme.SetPhoneScaleLand(value);
        }

        /// <summary>Portrait position, in phone pixels from center. The first call remembers the player's placement.</summary>
        public static void SetPhonePos(float x, float y)
        {
            RememberPhonePlacement();
            PhoneTheme.SetPhonePos(x, y);
            PhoneMenu.ApplyPlacement();
        }

        /// <summary>Landscape position, in phone pixels from center. The first call remembers the player's placement.</summary>
        public static void SetPhonePosLand(float x, float y)
        {
            RememberPhonePlacement();
            PhoneTheme.SetPhonePosLand(x, y);
            PhoneMenu.ApplyPlacement();
        }

        /// <summary>
        /// Turn the phone tall or wide without saving that as the player's rotate choice.
        /// The first call remembers the player's placement, including orientation.
        /// While that override is active, closing the phone and opening apps leave this
        /// orientation, size, and position in place. <see cref="RestorePhonePlacement"/> puts them back.
        /// </summary>
        public static void SetPhoneOrientation(bool landscape)
        {
            RememberPhonePlacement();
            PhoneMenu.SetLandscape(landscape);
        }

        /// <summary>Put size, position, and orientation back to <see cref="OriginalPhonePlacement"/> and forget the snapshot.</summary>
        public static void RestorePhonePlacement()
        {
            if (!_placementHeld)
                return;
            PiPhonePlacement original = _originalPlacement;
            _placementHeld = false;
            _originalPlacement = null;
            PhoneTheme.HoldSavedPlacement = false;
            PhoneTheme.PhoneScale = Mathf.Clamp(original.Scale, 0.55f, 1.35f);
            PhoneTheme.PhoneScaleLand = Mathf.Clamp(original.ScaleLand, 0.55f, 1.8f);
            PhoneTheme.PhonePosX = original.X;
            PhoneTheme.PhonePosY = original.Y;
            PhoneTheme.PhonePosLandX = original.LandX;
            PhoneTheme.PhonePosLandY = original.LandY;
            PhoneTheme.Save();
            if (original.OrientationPinned)
                PhoneMenu.SetUserLandscape(original.Landscape);
            else
                PhoneMenu.SetLandscape(original.Landscape);
            PhoneMenu.ApplyPlacement();
        }

        private static bool _placementHeld;
        private static PiPhonePlacement _originalPlacement;

        private static void RememberPhonePlacement()
        {
            if (_placementHeld)
                return;
            _originalPlacement = CapturePhonePlacement();
            _placementHeld = true;
            PhoneTheme.HoldSavedPlacement = true;
        }

        private static PiPhonePlacement CapturePhonePlacement()
        {
            return new PiPhonePlacement
            {
                Landscape = PhoneMenu.IsLandscape,
                OrientationPinned = PhoneMenu.OrientationPinned,
                Scale = PhoneTheme.PhoneScale,
                ScaleLand = PhoneTheme.PhoneScaleLand,
                X = PhoneTheme.PhonePosX,
                Y = PhoneTheme.PhonePosY,
                LandX = PhoneTheme.PhonePosLandX,
                LandY = PhoneTheme.PhonePosLandY
            };
        }

        private static PiPhonePlacement CopyPlacement(PiPhonePlacement source)
        {
            if (source == null)
                return CapturePhonePlacement();
            return new PiPhonePlacement
            {
                Landscape = source.Landscape,
                OrientationPinned = source.OrientationPinned,
                Scale = source.Scale,
                ScaleLand = source.ScaleLand,
                X = source.X,
                Y = source.Y,
                LandX = source.LandX,
                LandY = source.LandY
            };
        }

        /// <summary>
        /// First time the phone opens each launch, this page covers the screen while the UI builds.
        /// </summary>
        public static void SetStartup(PiPhoneStage stage)
        {
            StartupStage = stage;
        }

        /// <summary>Played when the phone is turned on after <see cref="PowerOff"/>.</summary>
        public static void SetPowerOn(PiPhoneStage stage)
        {
            PowerOnStage = stage;
        }

        /// <summary>Played when <see cref="PowerOff"/> runs while the phone is open.</summary>
        public static void SetPowerOff(PiPhoneStage stage)
        {
            PowerOffStage = stage;
        }

        public static bool Powered = false;

        internal static PiPhoneStage StartupStage;
        internal static PiPhoneStage PowerOnStage;
        internal static PiPhoneStage PowerOffStage;

        /// <summary>Skip the rest of the current startup, power, or app loading wait.</summary>
        public static void FinishStage()
        {
            PhoneMenu.FinishStage();
        }

        /// <summary>Turn the phone off. The next open stays dark until the side button is held.</summary>
        public static void PowerOff()
        {
            PhoneMenu.PowerOff();
        }

        /// <summary>Turn the phone on. A side-button hold does this while the screen is dark. Plays HELLO unless <see cref="SetPowerOn"/> replaced it.</summary>
        public static void PowerOn()
        {
            PhoneMenu.PowerOn();
        }

        /// <summary>
        /// Add a screen the cast chip can target. <see cref="PiPhoneCastDevice.Find"/>
        /// should return that object's transform in the current scene, or null.
        /// The same id replaces an older entry.
        /// </summary>
        public static void RegisterCastDevice(PiPhoneCastDevice device)
        {
            PhoneCast.Register(device);
        }

        /// <summary>Remove a screen added with <see cref="RegisterCastDevice"/>.</summary>
        public static void UnregisterCastDevice(string id)
        {
            PhoneCast.Unregister(id);
        }

        /// <summary>
        /// Shape of the cast picture, as width and height (16 and 9, or 4 and 3).
        /// The phone image stays unstretched inside that shape. Zero either value
        /// to use the phone's own shape again. Also cleared when the open app closes.
        /// </summary>
        public static void SetCastAspect(float width, float height)
        {
            PhoneCast.SetAspect(width, height);
        }

        /// <summary>Cast uses the phone's aspect again, or the open app's <see cref="PiPhoneApp.CastAspectWidth"/> if that is set.</summary>
        public static void ClearCastAspect()
        {
            PhoneCast.ClearAspect();
        }

        /// <summary>
        /// Pause or resume the GIF wallpaper while your app is open. Cleared when the
        /// app closes. Prefer <see cref="PiPhoneApp.CoversWallpaper"/> when the app
        /// already covers the wallpaper.
        /// </summary>
        public static void SetWallpaperPaused(bool paused)
        {
            PhoneMenu.SetWallpaperPaused(paused);
        }

        /// <summary>
        /// Register or replace a keybind. Shows up in Settings → Controls.
        /// Id should be unique, e.g. "mymod.jump". Empty <see cref="PiPhoneKeybind.AppId"/>
        /// plus <see cref="PiPhoneKeybind.Global"/> means it works with the phone closed.
        /// </summary>
        public static void RegisterKeybind(PiPhoneKeybind bind)
        {
            PhoneKeys.Add(bind);
        }

        /// <summary>
        /// Add or replace a Quick settings chip. Id should be unique, e.g. "mymod.torch".
        /// Players can hide it in Settings → Customize → Toolbar.
        /// </summary>
        public static void RegisterShadeButton(PiPhoneShadeButton button)
        {
            PhoneShade.Register(button);
            PhoneMenu.RefreshShade();
        }

        /// <summary>
        /// Add or replace a nav-bar button, placed to the right of Rotate.
        /// Id should be unique, e.g. "mymod.map".
        /// </summary>
        public static void RegisterNavButton(PiPhoneNavButton button)
        {
            PhoneMenu.RegisterNavButton(button);
        }

        /// <summary>
        /// Handle a keypad number. Digits only, e.g. "911". Your
        /// <see cref="PiPhoneNumber.OnCall"/> should return true if you handled it.
        /// </summary>
        public static void RegisterNumber(PiPhoneNumber number)
        {
            PhoneNumbers.Register(number);
        }

        public static bool UnregisterNumber(string number)
        {
            return PhoneNumbers.Unregister(number);
        }

        public static int BatteryPercent = 100;
        public static bool BatteryCharging;
        public static PiPhoneCarrier Carrier = PiPhoneCarrier.Lte;
        public static int SignalBars = 4;

        /// <summary>Status-bar battery. Unused by PiPhone itself until a later realism pass.</summary>
        public static void SetBattery(int percent, bool charging)
        {
            if (percent < 0)
                percent = 0;
            if (percent > 100)
                percent = 100;
            BatteryPercent = percent;
            BatteryCharging = charging;
        }

        /// <summary>Status-bar carrier / bars. Unused by PiPhone itself until a later realism pass.</summary>
        public static void SetCarrier(PiPhoneCarrier carrier, int bars)
        {
            Carrier = carrier;
            if (bars < 0)
                bars = 0;
            if (bars > 4)
                bars = 4;
            SignalBars = bars;
        }

        /// <summary>Photon UserId / Steam id on PEAK. Creates the contact if needed.</summary>
        public static void SetContactName(string id, string customName)
        {
            PhoneContacts.SetCustomName(id, customName);
        }

        /// <summary>PNG/JPG profile photo for that Steam / Photon UserId.</summary>
        public static bool SetContactPhoto(string id, byte[] pngOrJpg)
        {
            return PhoneContacts.SetPhoto(id, pngOrJpg);
        }

        public static bool TryGetContact(string id, out PiPhoneContact contact)
        {
            contact = PhoneContacts.Get(id);
            return contact != null;
        }

        /// <summary>Custom name if set, otherwise the last seen nick / fallback.</summary>
        public static string ContactDisplayName(string id, string fallback)
        {
            return PhoneContacts.Display(id, fallback);
        }

        public static PiPhoneContact[] GetContacts()
        {
            return PhoneContacts.All();
        }

        public static PiPhoneLook GetLook()
        {
            return new PiPhoneLook
            {
                Screen = PhoneTheme.ScreenColor,
                Surface = PhoneTheme.SurfaceColor,
                Nav = PhoneTheme.NavColor,
                NavButton = PhoneTheme.NavButtonColor,
                NavIcon = PhoneTheme.NavIconColor,
                Text = PhoneTheme.TextColor,
                Accent = PhoneTheme.AccentColor,
                Icon = PhoneTheme.IconColor,
                Case = PhoneTheme.CaseColor,
                Clock = PhoneTheme.ClockColor,
                ButtonFill = PhoneTheme.ButtonFillColor,
                ButtonText = PhoneTheme.ButtonFontColor,
                FontScale = PhoneTheme.FontScale,
                ButtonRadius = PhoneTheme.ButtonRadius,
                IconRadius = PhoneTheme.IconRadius
            };
        }

        /// <summary>Apply a full look snapshot. Use this from a third-party themes app.</summary>
        public static void SetLook(PiPhoneLook look)
        {
            if (look == null)
                return;
            PhoneTheme.ScreenColor = look.Screen;
            PhoneTheme.SurfaceColor = look.Surface;
            PhoneTheme.NavColor = look.Nav;
            PhoneTheme.NavButtonColor = look.NavButton;
            PhoneTheme.NavIconColor = look.NavIcon;
            PhoneTheme.TextColor = look.Text;
            PhoneTheme.AccentColor = look.Accent;
            PhoneTheme.IconColor = look.Icon;
            PhoneTheme.CaseColor = look.Case;
            PhoneTheme.ClockColor = look.Clock;
            PhoneTheme.ButtonFillColor = look.ButtonFill;
            PhoneTheme.ButtonFontColor = look.ButtonText;
            PhoneTheme.FontScale = Mathf.Clamp(look.FontScale, 0.7f, 1.6f);
            PhoneTheme.ButtonRadius = look.ButtonRadius < 0 ? 0 : (look.ButtonRadius > 28 ? 28 : look.ButtonRadius);
            PhoneTheme.IconRadius = look.IconRadius < 0 ? 0 : (look.IconRadius > 28 ? 28 : look.IconRadius);
            PhoneTheme.Commit();
        }

        public static void ResetLook() { PhoneTheme.ResetLook(); }
        public static void ResetButtons() { PhoneTheme.ResetButtons(); }
        public static void ResetCase() { PhoneTheme.ResetCase(); }
        public static void ResetClockColor() { PhoneTheme.ResetClockColor(); }

        /// <summary>Add or replace a language pack. File packs in the lang folder also load at boot.</summary>
        public static void RegisterLanguage(PiPhoneLanguage pack)
        {
            PhoneLang.Register(pack);
        }

        public static bool SetLanguage(string code)
        {
            return PhoneLang.Set(code);
        }

        public static void UseSystemLanguage()
        {
            PhoneLang.UseSystem();
        }

        public static string GetLanguage()
        {
            return PhoneLang.Code;
        }

        public static string T(string key, string fallback)
        {
            return PhoneLang.T(key, fallback);
        }

        public static PiPhoneLanguage[] GetLanguages()
        {
            return PhoneLang.All();
        }

        public static void SetScreenColor(Color color) { PhoneTheme.SetScreenColor(color); }
        public static void SetSurfaceColor(Color color) { PhoneTheme.SetSurfaceColor(color); }
        public static void SetNavColor(Color color) { PhoneTheme.SetNavColor(color); }
        public static void SetNavButtonColor(Color color) { PhoneTheme.SetNavButtonColor(color); }
        public static void SetNavIconColor(Color color) { PhoneTheme.SetNavIconColor(color); }
        public static void SetTextColor(Color color) { PhoneTheme.SetTextColor(color); }
        public static void SetAccentColor(Color color) { PhoneTheme.SetAccentColor(color); }
        public static void SetIconColor(Color color) { PhoneTheme.SetIconColor(color); }
        public static void SetFontScale(float scale) { PhoneTheme.SetFontScale(scale); }
        public static void SetButtonRadius(int radius) { PhoneTheme.SetButtonRadius(radius); }
        public static void SetIconRadius(int radius) { PhoneTheme.SetIconRadius(radius); }

        public static bool UnregisterShadeButton(string id)
        {
            bool removed = PhoneShade.Unregister(id);
            if (removed)
                PhoneMenu.RefreshShade();
            return removed;
        }

        public static PiPhoneShadeButton[] GetShadeButtons()
        {
            return PhoneShade.All();
        }

        public static bool ShadeButtonVisible(string id)
        {
            PiPhoneShadeButton[] all = PhoneShade.All();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && string.Equals(all[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return PhoneShade.Visible(all[i]);
            }
            return false;
        }

        public static void SetShadeButtonVisible(string id, bool visible)
        {
            PhoneTheme.SetShadeButtonOn(id, visible);
            PhoneMenu.RefreshShade();
        }

        public static bool UnregisterKeybind(string id)
        {
            return PhoneKeys.Remove(id);
        }

        /// <summary>True on the frame the player pressed this bind and it is allowed to fire.</summary>
        public static bool KeyDown(string id)
        {
            return PhoneKeys.Down(id);
        }

        public static bool KeyHeld(string id)
        {
            return PhoneKeys.Held(id);
        }

        public static string FormatKey(string id)
        {
            return PhoneKeys.Format(id);
        }

        public static PiPhoneKeybind[] GetKeybinds()
        {
            return PhoneKeys.All();
        }

        /// <summary>Start listening for a key. Esc cancels. <paramref name="onDone"/> runs after capture or cancel.</summary>
        public static void CaptureKeybind(string id, Action onDone)
        {
            PhoneKeys.BeginCapture(id, onDone);
        }

        public static void ClearKeybind(string id)
        {
            PhoneKeys.Clear(id);
        }

        /// <summary>Hide the in-app title bar and content padding so the app can use the full body.</summary>
        public static void SetImmersive(bool immersive)
        {
            PhoneMenu.SetImmersive(immersive);
        }

        /// <summary>Player rotate: landscape when true, portrait when false. Survives app switches until the phone closes.</summary>
        public static void SetUserLandscape(bool landscape)
        {
            PhoneMenu.SetUserLandscape(landscape);
        }

        /// <summary>Flip between portrait and landscape (same phone shape).</summary>
        public static void ToggleLandscape()
        {
            PhoneMenu.ToggleUserLandscape();
        }

        /// <summary>BepInEx folder <c>CrispberryPiPhone/alerts</c>. Drop short WAV/OGG clips here; they show up in MakeNoti.</summary>
        public static string AlertsFolder
        {
            get { return PhoneStore.AlertsDir; }
        }

        /// <summary>Post a notification for your app. No-op if the player muted it or <see cref="PiPhoneApp.PostsNotices"/> is false.</summary>
        public static void Notify(string appId, string title, string body)
        {
            PhoneNotify.Post(appId, title, body, true, true);
        }

        /// <summary>Copy a sound file into the alerts library (max 20s). Returns the new id, or null.</summary>
        public static string ImportAlert(string path)
        {
            SoundItem item = PhoneStore.AddSound(path, true, 20f);
            return item != null ? item.Id : null;
        }

        /// <summary>Set this app's alert sound. Pass null or empty to restore the Settings default.</summary>
        public static void SetAppAlertSound(string appId, string soundId)
        {
            PhoneTones.SetAppTone(appId, soundId);
        }

        public static string GetAppAlertSound(string appId)
        {
            return PhoneTones.AppTone(appId);
        }

        public static void Toast(string message)
        {
            PhoneMenu.Toast(message);
        }

        /// <summary>Local scout, or null before the character exists.</summary>
        public static PiPhoneScout LocalScout()
        {
            return ScoutQuery.Local();
        }

        public static bool IsLocalDead
        {
            get { return ScoutQuery.LocalIsDead(); }
        }

        public static bool IsLocalGhost
        {
            get { return ScoutQuery.LocalIsGhost(); }
        }

        /// <summary>Everyone in the room who has a character, including you.</summary>
        public static PiPhoneScout[] GetScouts()
        {
            return ScoutQuery.All(false);
        }

        /// <summary>Room scouts except the local player.</summary>
        public static PiPhoneScout[] GetOtherScouts()
        {
            return ScoutQuery.All(true);
        }

        public static PiPhoneScout[] GetDeadScouts()
        {
            return ScoutQuery.Where(s => s.IsDead);
        }

        public static PiPhoneScout[] GetGhostScouts()
        {
            return ScoutQuery.Where(s => s.IsGhost);
        }

        /// <summary>Passed out and still alive. Excludes ghosts and dead bodies.</summary>
        public static PiPhoneScout[] GetPassedOutScouts()
        {
            return ScoutQuery.Where(s => s.IsPassedOut && !s.IsDead && !s.IsGhost);
        }

        public static PiPhoneScout[] GetCarriedScouts()
        {
            return ScoutQuery.Where(s => s.IsCarried);
        }

        public static bool TryGetScout(string id, out PiPhoneScout scout)
        {
            return ScoutQuery.TryById(id, out scout);
        }

        public static bool TryGetScout(int actorNumber, out PiPhoneScout scout)
        {
            return ScoutQuery.TryByActor(actorNumber, out scout);
        }

        public static bool IsScoutDead(string id)
        {
            PiPhoneScout scout;
            return TryGetScout(id, out scout) && scout.IsDead;
        }

        public static bool IsScoutGhost(string id)
        {
            PiPhoneScout scout;
            return TryGetScout(id, out scout) && scout.IsGhost;
        }

        public static bool IsScoutDead(int actorNumber)
        {
            PiPhoneScout scout;
            return TryGetScout(actorNumber, out scout) && scout.IsDead;
        }

        public static bool IsScoutGhost(int actorNumber)
        {
            PiPhoneScout scout;
            return TryGetScout(actorNumber, out scout) && scout.IsGhost;
        }

        /// <summary>Read the current listener-side voice FX on that scout.</summary>
        public static bool TryGetVoiceFilter(string id, out PiPhoneVoiceFilter filter)
        {
            return VoiceFx.TryGet(id, out filter);
        }

        public static bool TryGetVoiceFilter(int actorNumber, out PiPhoneVoiceFilter filter)
        {
            return VoiceFx.TryGet(actorNumber, out filter);
        }

        /// <summary>
        /// Hold vanilla voice FX on a remote scout until <see cref="ClearVoiceFilter"/>.
        /// Does not change what they transmit. An active call with them still uses
        /// PiPhone's call tone (dry or realm) until hangup.
        /// </summary>
        public static bool SetVoiceFilter(string id, PiPhoneVoiceFilter filter)
        {
            return VoiceFx.SetHold(id, filter);
        }

        public static bool SetVoiceFilter(int actorNumber, PiPhoneVoiceFilter filter)
        {
            return VoiceFx.SetHold(actorNumber, filter);
        }

        public static bool ClearVoiceFilter(string id)
        {
            return VoiceFx.ClearHold(id);
        }

        public static bool ClearVoiceFilter(int actorNumber)
        {
            return VoiceFx.ClearHold(actorNumber);
        }

        public static void ClearAllVoiceFilters()
        {
            VoiceFx.ClearAllHolds();
        }

        public static bool VoiceFilterHeld(string id)
        {
            return VoiceFx.IsHeld(id);
        }

        /// <summary>
        /// Register or replace an app. Safe to call from your plugin Awake after this
        /// plugin has loaded (hard-depend), or whenever <see cref="IsReady"/> is true.
        /// </summary>
        public static void RegisterApp(PiPhoneApp app)
        {
            if (app == null || string.IsNullOrEmpty(app.Id))
                throw new ArgumentException("PiPhone app needs a non-empty Id.", "app");

            int existing = IndexOf(app.Id);
            if (existing >= 0)
                Apps[existing] = app;
            else
                Apps.Add(app);

            if (app.IconSprite != null)
                app.IconSprite = PhoneIcons.ClampSprite(app.IconSprite);
            if (!string.IsNullOrEmpty(app.BundledAlertPath) && File.Exists(app.BundledAlertPath))
            {
                SoundItem imported = PhoneStore.AddSound(app.BundledAlertPath, true, 20f);
                if (imported != null && string.IsNullOrEmpty(PhoneTones.AppTone(app.Id)))
                    PhoneTones.SetAppTone(app.Id, imported.Id);
            }

            if (PhoneStore.DefaultsReady && app.Preinstalled && !PhoneStore.IsInstalled(app.Id))
                PhoneStore.Install(app.Id);

            SortApps();
            RaiseAppsChanged();
            PhoneMenu.OnAppsChanged();
        }

        public const int MaxIconSize = PhoneIcons.MaxSize;
        public const int MaxIconBytes = PhoneIcons.MaxBytes;

        /// <summary>Decode and clamp a texture to a cached 128px icon sprite. Source can be destroyed afterward.</summary>
        public static Sprite CreateIcon(Texture2D texture)
        {
            return PhoneIcons.FromTexture(texture, false);
        }

        /// <summary>Decode PNG/JPG bytes, clamp to 128px, and cache. Sources over <see cref="MaxIconBytes"/> are skipped (logged).</summary>
        public static Sprite CreateIcon(byte[] pngOrJpg)
        {
            return PhoneIcons.FromBytes(pngOrJpg);
        }

        /// <summary>Load a PNG/JPG from disk, clamp to 128px, and cache. Files over <see cref="MaxIconBytes"/> are skipped (logged).</summary>
        public static Sprite CreateIconFromFile(string path)
        {
            return PhoneIcons.FromFile(path);
        }

        /// <summary>
        /// A phone button that shows your own picture. Any sprite works; it does not
        /// have to be a Google icon. Colors in the picture are kept.
        /// </summary>
        public static Button CreateIconButton(Transform parent, Sprite icon, UnityAction onClick, Vector2 size)
        {
            return PhoneUi.CreateIconChip(parent, string.Empty, icon, onClick, false, size, false);
        }

        /// <summary>Same as <see cref="CreateIconButton(Transform, Sprite, UnityAction, Vector2)"/>, decoding a PNG or JPG first.</summary>
        public static Button CreateIconButton(Transform parent, byte[] pngOrJpg, UnityAction onClick, Vector2 size)
        {
            return CreateIconButton(parent, CreateIcon(pngOrJpg), onClick, size);
        }

        public static bool UnregisterApp(string id)
        {
            int index = IndexOf(id);
            if (index < 0)
                return false;
            Apps.RemoveAt(index);
            RaiseAppsChanged();
            PhoneMenu.OnAppsChanged();
            return true;
        }

        public static bool TryGetApp(string id, out PiPhoneApp app)
        {
            int index = IndexOf(id);
            if (index < 0)
            {
                app = null;
                return false;
            }
            app = Apps[index];
            return true;
        }

        public static PiPhoneApp[] GetApps()
        {
            return Apps.ToArray();
        }

        /// <summary>Installed apps currently on the phone (home / All apps).</summary>
        public static PiPhoneApp[] GetInstalledApps()
        {
            var list = new List<PiPhoneApp>();
            for (int i = 0; i < Apps.Count; i++)
            {
                if (Apps[i] != null && PhoneStore.IsInstalled(Apps[i].Id))
                    list.Add(Apps[i]);
            }
            return list.ToArray();
        }

        private static readonly List<PiPhoneCategory> Categories = new List<PiPhoneCategory>();

        /// <summary>
        /// Add or rename a store section. Apps set <see cref="PiPhoneApp.Category"/> to <paramref name="id"/>.
        /// </summary>
        public static void RegisterCategory(string id, string displayName, int sortOrder)
        {
            if (string.IsNullOrEmpty(id))
                return;
            for (int i = 0; i < Categories.Count; i++)
            {
                if (Categories[i] != null && string.Equals(Categories[i].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    Categories[i].DisplayName = string.IsNullOrEmpty(displayName) ? id : displayName;
                    Categories[i].SortOrder = sortOrder;
                    return;
                }
            }
            Categories.Add(new PiPhoneCategory
            {
                Id = id,
                DisplayName = string.IsNullOrEmpty(displayName) ? id : displayName,
                SortOrder = sortOrder
            });
        }

        /// <summary>Store sections, sorted by <see cref="PiPhoneCategory.SortOrder"/>.</summary>
        public static PiPhoneCategory[] GetCategories()
        {
            var list = new List<PiPhoneCategory>(Categories);
            list.Sort((a, b) =>
            {
                int ao = a != null ? a.SortOrder : 0;
                int bo = b != null ? b.SortOrder : 0;
                int cmp = ao.CompareTo(bo);
                if (cmp != 0)
                    return cmp;
                return string.Compare(a != null ? a.DisplayName : string.Empty, b != null ? b.DisplayName : string.Empty, StringComparison.OrdinalIgnoreCase);
            });
            return list.ToArray();
        }

        /// <summary>Apps listed in the Apps store, including ones already installed.</summary>
        public static PiPhoneApp[] GetStoreApps()
        {
            var list = new List<PiPhoneApp>();
            for (int i = 0; i < Apps.Count; i++)
            {
                PiPhoneApp app = Apps[i];
                if (app == null || app.ListedInStore == false)
                    continue;
                list.Add(app);
            }
            return list.ToArray();
        }

        public static bool IsInstalled(string id)
        {
            return PhoneStore.IsInstalled(id);
        }

        public static bool CanUninstall(string id)
        {
            PiPhoneApp app;
            if (!TryGetApp(id, out app) || app == null || app.Sticky)
                return false;
            return PhoneStore.IsInstalled(id);
        }

        /// <summary>Install a registered app onto the phone. No-op if unknown or already installed. Honors <see cref="PiPhoneApp.ShowOnHome"/>.</summary>
        public static bool Install(string id)
        {
            PiPhoneApp app;
            if (!TryGetApp(id, out app) || app == null)
                return false;
            if (!PhoneStore.Install(id))
                return false;
            RaiseAppsChanged();
            PhoneMenu.OnAppsChanged();
            return true;
        }

        /// <summary>True if this installed app is on the home grid.</summary>
        public static bool IsOnHome(string id)
        {
            return PhoneStore.IsOnHome(id);
        }

        /// <summary>Pin an installed app to the home grid. No-op if unknown, not installed, or already there.</summary>
        public static bool AddToHome(string id)
        {
            if (!PhoneStore.AddToHome(id))
                return false;
            RaiseAppsChanged();
            PhoneMenu.OnAppsChanged();
            return true;
        }

        /// <summary>Remove an app from the home grid. It stays installed (All apps).</summary>
        public static bool RemoveFromHome(string id)
        {
            if (!PhoneStore.RemoveFromHome(id))
                return false;
            RaiseAppsChanged();
            PhoneMenu.OnAppsChanged();
            return true;
        }

        /// <summary>Remove an app from the phone. It stays listed in Apps unless unregistered.</summary>
        public static bool Uninstall(string id)
        {
            if (!CanUninstall(id))
                return false;
            if (CurrentAppId == id)
                PhoneMenu.GoHome();
            if (!PhoneStore.Uninstall(id))
                return false;
            RaiseAppsChanged();
            PhoneMenu.OnAppsChanged();
            return true;
        }

        internal static int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id))
                return -1;
            for (int i = 0; i < Apps.Count; i++)
            {
                if (Apps[i] != null && string.Equals(Apps[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static void SortApps()
        {
            Apps.Sort(CompareApps);
        }

        private static int CompareApps(PiPhoneApp a, PiPhoneApp b)
        {
            int dock = (b != null && b.ShowOnDock ? 1 : 0) - (a != null && a.ShowOnDock ? 1 : 0);
            if (dock != 0)
                return dock;
            int order = (a != null ? a.SortOrder : 0).CompareTo(b != null ? b.SortOrder : 0);
            if (order != 0)
                return order;
            string an = a != null ? a.DisplayName : string.Empty;
            string bn = b != null ? b.DisplayName : string.Empty;
            return string.Compare(an, bn, StringComparison.OrdinalIgnoreCase);
        }

        private static void RaiseAppsChanged()
        {
            Action handler = AppsChanged;
            if (handler != null)
                handler();
        }

        internal static void RaiseOrientation()
        {
            Action handler = OrientationChanged;
            if (handler != null)
                handler();
        }
    }

    /// <summary>
    /// How the player moves while the phone is open.
    /// Off = cursor only. Walk = look and move, cursor hidden.
    /// WalkAndCursor = look and move with the cursor out so they can tap the phone.
    /// </summary>
    public enum PiPhonePlayThrough
    {
        Off,
        Walk,
        WalkAndCursor
    }

    /// <summary>Status-bar carrier. Default LTE. Nothing changes this unless a mod calls <see cref="PiPhoneApi.SetCarrier"/>.</summary>
    public enum PiPhoneCarrier
    {
        None,
        Edge,
        ThreeG,
        Lte,
        FiveG
    }

    /// <summary>
    /// Saved contact. <see cref="Id"/> is Photon UserId (Steam id on PEAK).
    /// <see cref="CustomName"/> is what the player typed; <see cref="RealName"/> is the last seen nick.
    /// </summary>
    public sealed class PiPhoneContact
    {
        public string Id;
        public string CustomName;
        public string RealName;
        public string PhotoFile;
        public bool BlockCalls;
        public bool BlockTexts;
    }

    /// <summary>A section in the Apps store. Register with <see cref="PiPhoneApi.RegisterCategory"/>.</summary>
    public sealed class PiPhoneCategory
    {
        public string Id;
        public string DisplayName;
        public int SortOrder;
    }

    /// <summary>Independent chrome colors and corner radii. No preset packs.</summary>
    public sealed class PiPhoneLook
    {
        public Color Screen = new Color(0.08f, 0.10f, 0.13f, 1f);
        public Color Surface = new Color(0.16f, 0.18f, 0.21f, 0.96f);
        public Color Nav = new Color(0.10f, 0.11f, 0.13f, 1f);
        public Color NavButton = new Color(0.20f, 0.22f, 0.26f, 1f);
        public Color NavIcon = new Color(0.96f, 0.97f, 0.98f, 1f);
        public Color Text = new Color(0.96f, 0.97f, 0.98f, 1f);
        public Color Accent = new Color(0.24f, 0.86f, 0.52f, 1f);
        public Color Icon = Color.white;
        public Color Case = new Color(0.07f, 0.07f, 0.08f, 1f);
        public Color Clock = new Color(0.96f, 0.97f, 0.98f, 1f);
        public Color ButtonFill = new Color(0.20f, 0.22f, 0.26f, 1f);
        public Color ButtonText = new Color(0.96f, 0.97f, 0.98f, 1f);
        public float FontScale = 1f;
        public int ButtonRadius = 18;
        public int IconRadius = 20;
    }

    /// <summary>
    /// One icon in the status row, just left of the battery.
    /// </summary>
    public sealed class PiPhoneStatusSlot
    {
        public string Id;
        public Sprite Icon;
        public Action OnClick;
    }

    /// <summary>
    /// A timed full-phone page. Put pictures, GIFs, video, or audio under the rectangle
    /// passed to <see cref="Build"/>. The phone does not decode that media.
    /// </summary>
    public sealed class PiPhoneStage
    {
        /// <summary>How long the page stays up. <see cref="PiPhoneApi.FinishStage"/> can end it sooner.</summary>
        public float Seconds = 1.2f;

        /// <summary>Optional. Parent your own UI to this rectangle.</summary>
        public Action<RectTransform> Build;
    }

    /// <summary>
    /// A world screen the phone can cast onto. Airport flight boards are already registered.
    /// </summary>
    public sealed class PiPhoneCastDevice
    {
        /// <summary>Unique id. Use your plugin GUID as the prefix.</summary>
        public string Id;

        /// <summary>Name shown in logs.</summary>
        public string Name;

        /// <summary>Return the live transform, or null when this scene does not have it.</summary>
        public Func<Transform> Find;
    }

    /// <summary>
    /// The player's phone size, screen position, and orientation.
    /// <see cref="PiPhoneApi.OriginalPhonePlacement"/> is the copy taken before a mod moves the phone.
    /// </summary>
    public sealed class PiPhonePlacement
    {
        /// <summary>True when the phone is landscape.</summary>
        public bool Landscape;

        /// <summary>True when the player pinned portrait or landscape with the rotate control.</summary>
        public bool OrientationPinned;

        /// <summary>Portrait size. 1 is the default. Range 0.55–1.35.</summary>
        public float Scale;

        /// <summary>Landscape size. 1.2 is the default. Range 0.55–1.8.</summary>
        public float ScaleLand;

        /// <summary>Portrait position, pixels from the center of the screen.</summary>
        public float X;
        public float Y;

        /// <summary>Landscape position, pixels from the center of the screen.</summary>
        public float LandX;
        public float LandY;
    }

    /// <summary>
    /// Descriptor other mods fill in and pass to <see cref="PiPhoneApi.RegisterApp"/>.
    /// </summary>
    public sealed class PiPhoneApp
    {
        /// <summary>Stable unique id, e.g. "mymod.snake".</summary>
        public string Id;

        /// <summary>Home-screen and All-apps label.</summary>
        public string DisplayName;

        /// <summary>Single character drawn on the generated icon when <see cref="IconSprite"/> is null.</summary>
        public string IconGlyph;

        public Color IconBackground = new Color(0.22f, 0.45f, 0.72f, 1f);
        public Color IconForeground = Color.white;

        /// <summary>
        /// Optional custom icon from any source (your PNG, a texture, a sprite).
        /// You do not need the Google icon set. The phone masks this to the player's
        /// icon shape. Letter glyphs are only used when this is null.
        /// </summary>
        public Sprite IconSprite;

        /// <summary>Assign a PNG/JPG icon. Decoded, clamped to 128px, and cached.</summary>
        public bool SetIcon(byte[] pngOrJpg)
        {
            Sprite sprite = PhoneIcons.FromBytes(pngOrJpg);
            if (sprite == null)
                return false;
            IconSprite = sprite;
            return true;
        }

        /// <summary>Assign a texture icon. Copied and clamped to 128px; you may destroy <paramref name="texture"/> after.</summary>
        public void SetIcon(Texture2D texture)
        {
            IconSprite = PhoneIcons.FromTexture(texture, false);
        }

        /// <summary>Load a PNG/JPG from disk as the app icon. Decoded then clamped to 128px.</summary>
        public bool SetIconFile(string path)
        {
            Sprite sprite = PhoneIcons.FromFile(path);
            if (sprite == null)
                return false;
            IconSprite = sprite;
            return true;
        }
        public int SortOrder;

        /// <summary>If true and the player has no saved dock yet, this app is offered a dock slot.</summary>
        public bool ShowOnDock;

        /// <summary>
        /// If true (default), <see cref="PiPhoneApi.Install"/> also pins this app on the
        /// home grid (including a reinstall). Players can remove it later by holding the icon.
        /// </summary>
        public bool ShowOnHome = true;

        /// <summary>
        /// If true, the app is installed the first time this player has no installed-apps
        /// list yet. Leave false for third-party mods so they appear in Apps first.
        /// </summary>
        public bool Preinstalled;

        /// <summary>If false, the app is hidden from the Apps listing.</summary>
        public bool ListedInStore = true;

        /// <summary>
        /// Store section id from <see cref="PiPhoneApi.RegisterCategory"/>,
        /// for example "games" or "communication". Empty means All only.
        /// </summary>
        public string Category;

        /// <summary>Store page text. Built-in apps use this without screenshots.</summary>
        public string Description;

        /// <summary>
        /// Hover text on the home, dock, and All apps icon.
        /// Empty uses <see cref="Description"/>.
        /// </summary>
        public string Tooltip;

        private readonly List<Sprite> _screenshots = new List<Sprite>();

        /// <summary>Pictures shown on the store page, in the order they were added. Clamped to 480px.</summary>
        public Sprite[] Screenshots
        {
            get { return _screenshots.ToArray(); }
        }

        /// <summary>Add a store screenshot. PNG or JPG, clamped to 480px. At most 8.</summary>
        public bool AddScreenshot(byte[] pngOrJpg)
        {
            return AddScreenshot(PhoneIcons.FromBytes(pngOrJpg, true, 480));
        }

        /// <summary>Add a store screenshot from a texture. Copied and clamped to 480px.</summary>
        public bool AddScreenshot(Texture2D texture)
        {
            return AddScreenshot(PhoneIcons.Screenshot(texture));
        }

        /// <summary>Add a store screenshot sprite. Clamped to 480px. At most 8.</summary>
        public bool AddScreenshot(Sprite sprite)
        {
            if (sprite == null || _screenshots.Count >= 8)
                return false;
            Sprite ready = PhoneIcons.ClampSprite(sprite, 480);
            if (ready == null)
                return false;
            _screenshots.Add(ready);
            return true;
        }

        /// <summary>If true, the app cannot be uninstalled (the Apps store itself).</summary>
        public bool Sticky;

        /// <summary>If false, this app never posts shade/HUD notifications (games should leave this false).</summary>
        public bool PostsNotices = true;

        /// <summary>
        /// If true (default), the player can walk/look with the phone open (Walk around key).
        /// Built-in apps leave this on. Set false only if your app must keep the cursor.
        /// </summary>
        public bool AllowPlayThrough = true;

        /// <summary>Optional path to a short alert clip copied into <see cref="PiPhoneApi.AlertsFolder"/> on register.</summary>
        public string BundledAlertPath;

        /// <summary>If true, open in landscape (same phone shape, landscape size). The player can still rotate any time.</summary>
        public bool Landscape;

        /// <summary>Alias of <see cref="Landscape"/> for older mods. Does not stretch the phone to fill the monitor.</summary>
        public bool Fullscreen;

        /// <summary>
        /// Cast picture width, in ratio units (16 in 16:9). Zero uses the phone's shape.
        /// Pair with <see cref="CastAspectHeight"/>. <see cref="PiPhoneApi.SetCastAspect"/>
        /// overrides this while the app is open.
        /// </summary>
        public float CastAspectWidth;

        /// <summary>Cast picture height, in ratio units (9 in 16:9). Zero uses the phone's shape.</summary>
        public float CastAspectHeight;

        /// <summary>
        /// If true (default), this app covers the wallpaper so the GIF can pause.
        /// Set false for transparent or overlay apps that should keep the wallpaper playing.
        /// </summary>
        public bool CoversWallpaper = true;

        /// <summary>If true, hide the in-app title bar so your UI can use the full body.</summary>
        public bool Immersive;

        /// <summary>
        /// If true (default), the phone applies its TMP font to your content after
        /// <see cref="OnOpen"/>. Set false if your app already has its own look.
        /// </summary>
        public bool ApplyPhoneFonts = true;

        /// <summary>Seconds to keep <see cref="BuildLoading"/> up while <see cref="OnOpen"/> runs behind it.</summary>
        public float LoadingSeconds;

        /// <summary>Optional loading page. Fill the rectangle with any media. Pair with <see cref="LoadingSeconds"/>.</summary>
        public Action<RectTransform> BuildLoading;

        /// <summary>
        /// Called after the phone clears <see cref="IPiPhoneHost.Content"/>.
        /// Parent your UI to that RectTransform. Use <see cref="PhoneUi"/> so buttons,
        /// labels, and media match the phone chrome. For images, call
        /// <see cref="PhoneUi.FitContained"/> instead of stretching.
        /// </summary>
        public Action<IPiPhoneHost> OnOpen;

        /// <summary>Called when leaving the app (home, back, or phone close).</summary>
        public Action OnClose;

        /// <summary>
        /// Optional. Return true if your app handled Back / Escape (e.g. closed a popup).
        /// Returning false lets the phone go home.
        /// </summary>
        public Func<bool> OnBack;

        /// <summary>Called when the player (or your app) switches landscape
        /// while this app is open. Rebuild under <see cref="IPiPhoneHost.Content"/>
        /// with <see cref="PhoneUi.SplitIfLandscape"/> / <see cref="PhoneUi.ApplyMediaGrid"/>.
        /// Skip mid-game rebuilds that would reset progress.
        /// </summary>
        public Action OnOrientation;

        /// <summary>
        /// Called every frame, including while the phone is closed and this app is not on screen.
        /// Keep it cheap. A thrown tick is logged once and then skipped until the game restarts.
        /// </summary>
        public Action Tick;
    }

    /// <summary>
    /// A player-configurable key. Register with <see cref="PiPhoneApi.RegisterKeybind"/>.
    /// </summary>
    public sealed class PiPhoneKeybind
    {
        /// <summary>Stable id, e.g. "mymod.jump".</summary>
        public string Id;

        /// <summary>Settings row label.</summary>
        public string Label;

        /// <summary>Settings section, e.g. "Camera" or your app name.</summary>
        public string Group = "Apps";

        /// <summary>If set, the bind only fires while this app is on screen (unless <see cref="AlsoApps"/> also matches).</summary>
        public string AppId;

        /// <summary>Extra app ids that may use this bind (shared game controls).</summary>
        public string[] AlsoApps;

        public KeyCode DefaultKey;
        public KeyCode DefaultModifier;
        public KeyCode Key;
        public KeyCode Modifier;

        /// <summary>If true, Settings capture also stores Ctrl/Alt/Shift with the key.</summary>
        public bool WithModifier;

        /// <summary>Fires even when the phone is closed (open-phone, answer, music).</summary>
        public bool Global;

        /// <summary>Fires while the phone is open and no app filter is set.</summary>
        public bool PhoneOpen;

        /// <summary>If true, Settings will not allow this bind to be cleared. Open-phone uses this.</summary>
        public bool Required;

        public int SortOrder;

        /// <summary>Optional. Invoked by the phone when the bind fires. You can also poll <see cref="PiPhoneApi.KeyDown"/>.</summary>
        public Action OnPressed;
    }

    /// <summary>
    /// A nav-bar icon. Register with <see cref="PiPhoneApi.RegisterNavButton"/>.
    /// Built-in Back, Home, All apps, and Rotate stay to the left of these.
    /// </summary>
    public sealed class PiPhoneNavButton
    {
        /// <summary>Stable id, e.g. "mymod.map".</summary>
        public string Id;

        /// <summary>Fallback letter if no icon is set.</summary>
        public string Glyph;

        /// <summary>Optional icon. White-on-transparent sprites tint with the nav icon color.</summary>
        public Sprite Icon;

        /// <summary>Optional live icon. Preferred over <see cref="Icon"/>.</summary>
        public Func<Sprite> IconFn;

        /// <summary>Set false for a full-color picture so the phone does not tint it.</summary>
        public bool TintIcon = true;

        public int SortOrder = 100;

        public Action OnClick;

        /// <summary>Hover text. Empty uses <see cref="Glyph"/>.</summary>
        public string Tooltip;

        /// <summary>If set and this returns false, the button is left off the bar.</summary>
        public Func<bool> Available;
    }

    /// <summary>
    /// A compact Quick settings chip. Register with <see cref="PiPhoneApi.RegisterShadeButton"/>.
    /// Players can hide it in Settings → Customize → Toolbar.
    /// </summary>
    public sealed class PiPhoneShadeButton
    {
        /// <summary>Stable id, e.g. "mymod.torch".</summary>
        public string Id;

        /// <summary>Settings row label. Also the hover text unless <see cref="Tooltip"/> is set.</summary>
        public string Label;

        /// <summary>Hover text. Empty uses <see cref="Label"/>.</summary>
        public string Tooltip;

        /// <summary>Fallback letter if <see cref="Icon"/> / <see cref="IconFn"/> is null.</summary>
        public string Glyph;

        /// <summary>Optional 32px icon. White-on-transparent sprites tint with the theme.</summary>
        public Sprite Icon;

        /// <summary>If true, the chip is a compact text label (ringer) instead of an icon.</summary>
        public bool UseText;

        public int SortOrder = 100;

        /// <summary>If false, the chip starts hidden until the player turns it on in Settings.</summary>
        public bool DefaultVisible = true;

        public Action OnClick;

        /// <summary>If set, the chip is highlighted while this returns true.</summary>
        public Func<bool> IsActive;

        /// <summary>If set and returns false, the chip is omitted (e.g. Home only while an app is open).</summary>
        public Func<bool> Available;

        /// <summary>When false, Settings cannot hide this chip. The navigation toggle stays available.</summary>
        public bool CanHide = true;

        /// <summary>Optional live glyph, e.g. a lock that changes when toggled.</summary>
        public Func<string> GlyphFn;

        /// <summary>Optional live icon. Preferred over <see cref="GlyphFn"/>.</summary>
        public Func<Sprite> IconFn;
    }

    /// <summary>
    /// Handle handed to an app while it is on screen.
    /// </summary>
    public interface IPiPhoneHost
    {
        RectTransform Content { get; }
        void SetTitle(string title);
        void GoHome();
        void GoBack();
        void ClosePhone();
        void ShowToast(string message);
        Coroutine StartHostCoroutine(IEnumerator routine);
        bool IsMasterClient { get; }
        Button CreateButton(Transform parent, string label, UnityAction onClick, Vector2 size);
        bool IsLandscape { get; }
        bool IsFullscreen { get; }
        void SetLandscape(bool landscape);
        void SetFullscreen(bool fullscreen);
        void SetImmersive(bool immersive);
        void ToggleLandscape();
        void SetPlayThrough(bool on);
        void SetPlayThrough(PiPhonePlayThrough mode);
        void TogglePlayThrough();
        bool IsPlayThrough { get; }
        PiPhonePlayThrough PlayThroughMode { get; }
        void SetWallpaperPaused(bool paused);

        /// <summary>
        /// Object passed to <see cref="PiPhoneApi.OpenApp(string, object)"/> for this open.
        /// Null when the player opened the app from the home screen.
        /// </summary>
        object Payload { get; }
    }
}
