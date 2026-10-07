using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    internal static class SettingsApp
    {
        private static Session _live;

        internal static void Register()
        {
            PiPhoneApi.RegisterApp(new PiPhoneApp
            {
                Id = BuiltinApps.SettingsId,
                DisplayName = "Settings",
                IconGlyph = "S",
                IconBackground = PhoneUi.SettingsIcon,
                SortOrder = 90,
                ShowOnDock = true,
                OnOpen = host => { _live = new Session(host); _live.ShowHome(); },
                OnClose = () =>
                {
                    Session live = _live;
                    _live = null;
                    if (live != null)
                        live.EndDisplayPreview();
                },
                OnOrientation = () => { if (_live != null) _live.Relayout(); }
            });
        }

        internal static bool TryGoBack()
        {
            if (_live == null)
                return false;
            return _live.GoBack();
        }

        private sealed class Session
        {
            private readonly IPiPhoneHost _host;
            private string _page = "home";
            private string _uiFileKey;
            private string _toneKind = "notify";
            private string _toneAppId;
            private string _toneContactId;
            private bool _toneContactRing = true;
            private int _lookIndex;
            private int _buttonIndex;
            private int _toggleIndex;
            private TextMeshProUGUI _chooserName;
            private bool _displayHold;
            private bool _savedLandscape;
            private bool _cornerApps;

            public Session(IPiPhoneHost host)
            {
                _host = host;
            }

            public bool GoBack()
            {
                if (_page == "home")
                    return false;
                if (_page == "tones")
                {
                    if (!string.IsNullOrEmpty(_toneContactId))
                    {
                        ShowNotifications();
                        return true;
                    }
                    ShowNotifications();
                    return true;
                }
                if (_page == "dock" || _page == "nav" || _page == "wallpaper")
                {
                    ShowHomeScreen();
                    return true;
                }
                if (_page == "display")
                {
                    _page = "customize";
                    EndDisplayPreview();
                    ShowCustomize();
                    return true;
                }
                if (_page == "notifications" || _page == "homescreen")
                {
                    ShowCustomize();
                    return true;
                }
                if (_page == "uifile")
                {
                    ShowSounds();
                    return true;
                }
                if (_page == "sounds")
                {
                    ShowCustomize();
                    return true;
                }
                if (_page == "customize" || _page == "controls")
                {
                    ShowHome();
                    return true;
                }
                ShowCustomize();
                return true;
            }

            public void Relayout()
            {
                switch (_page)
                {
                    case "customize": ShowCustomize(); break;
                    case "notifications": ShowNotifications(); break;
                    case "homescreen": ShowHomeScreen(); break;
                    case "dock": ShowDock(); break;
                    case "nav": ShowNavigation(); break;
                    case "wallpaper": ShowWallpaper(); break;
                    case "theme":
                    case "look": ShowLook(); break;
                    case "clock": ShowClock(); break;
                    case "buttons": ShowButtons(); break;
                    case "language": ShowLanguage(); break;
                    case "toolbar": ShowToolbar(); break;
                    case "display": ShowDisplay(); break;
                    case "controls": ShowControls(); break;
                    case "sounds": ShowSounds(); break;
                    case "uifile": ShowUiFiles(_uiFileKey); break;
                    case "tones": ShowNotifications(); break;
                    default: ShowHome(); break;
                }
            }

            public void ShowHome()
            {
                _page = "home";
                RectTransform content = BeginPage("Settings", null);
                OsRow(content);
                Row(content, "You are", _host.IsMasterClient ? "Expedition leader" : "Scout");
                Link(content, "keyboard", "Controls", "Keys, calls, and the pause menu", ShowControls);
                Link(content, "palette", "Personalize", "Style, display, home screen, alerts, and sounds", ShowCustomize);
                var keys = PhoneUi.CreateLabel(content, "OpenKey", "Open phone  " + Plugin.FormatHotkey(), 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                keys.color = PhoneUi.TextDim;
                PhoneUi.Wrap(keys);
                PhoneUi.Size(keys.gameObject, 28f);
            }

            private float _uiPreviewAt;

            private void ShowSounds()
            {
                _page = "sounds";
                PhoneSounds.StopPreview();
                RectTransform content = BeginPage("Sounds", ShowCustomize);
                var hint = PhoneUi.CreateLabel(content, "Hint", "Each sound can be turned off, turned down, or pointed at any other phone sound. The same clips can be alerts, ringtones, and button sounds.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 48f);
                AddUiCue(content, "click", "Button");
                AddUiCue(content, "toggle-off", "Toggle off");
                AddUiCue(content, "toggle-on", "Toggle on");
                AddUiCue(content, "back-btn", "Back");
                AddUiCue(content, "hover", "App hover");
                AddUiCue(content, "shutter", "Camera");
                AddUiCue(content, "rec-start", "Record start");
                AddUiCue(content, "rec-stop", "Record stop");
                AddUiCue(content, "tick", "Slider");
                AddUiCue(content, "trash", "Trash");
                AddUiCue(content, "vibrate", "Vibrate");
                AddUiCue(content, "type", "Typing");
                AddUiCue(content, "back", "Backspace");
            }

            private void AddUiCue(RectTransform content, string key, string label)
            {
                var name = PhoneUi.CreateLabel(content, "Cue", label, 16f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                PhoneUi.Size(name.gameObject, 24f);
                IconToggle(content, "instant_mix", label, PhoneTheme.UiCueOn(key), () =>
                {
                    PhoneTheme.SetUiCueOn(key, !PhoneTheme.UiCueOn(key));
                    if (PhoneTheme.UiCueOn(key))
                        PhoneSfx.PreviewUi(key);
                    ShowSounds();
                });
                PhoneUi.CreateSliderRow(content, "Volume", 0f, 1f, PhoneTheme.UiCueVolume(key), v =>
                {
                    PhoneTheme.SetUiCueVolume(key, v);
                    if (Time.unscaledTime < _uiPreviewAt)
                        return;
                    _uiPreviewAt = Time.unscaledTime + 0.14f;
                    PhoneSfx.PreviewUi(key);
                }, null, "instant_mix");
                PhoneUi.CreateButton(content, "Sound  " + PhoneTheme.UiCueFileName(key), () => ShowUiFiles(key), new Vector2(280f, 40f));
            }

            private void ShowUiFiles(string key)
            {
                _page = "uifile";
                _uiFileKey = key;
                PhoneSounds.StopPreview();
                string title = PhoneSfx.ClipLabel(key);
                if (string.IsNullOrEmpty(title))
                    title = "Sound";
                RectTransform content = BeginPage(title, ShowSounds);
                string current = PhoneTheme.UiCueFile(key);
                SoundChoice(content, "Default", string.IsNullOrEmpty(current), () =>
                {
                    PhoneTheme.SetUiCueFile(key, string.Empty);
                    ShowSounds();
                }, () => PhoneSfx.PreviewFile(key, key));
                PhoneSfx.Cue[] clips = PhoneSfx.Library();
                for (int i = 0; i < clips.Length; i++)
                {
                    string clipKey = clips[i].Key;
                    if (clipKey == key)
                        continue;
                    string clipLabel = clips[i].Label;
                    SoundChoice(content, clipLabel, current == clipKey, () =>
                    {
                        PhoneTheme.SetUiCueFile(key, clipKey);
                        ShowSounds();
                    }, () => PhoneSfx.PreviewFile(key, clipKey));
                }
                System.Collections.Generic.List<SoundItem> tones = PhoneStore.AlertTones();
                for (int i = 0; i < tones.Count; i++)
                {
                    SoundItem item = tones[i];
                    if (item == null)
                        continue;
                    string id = item.Id;
                    string path = PhoneStore.SoundPath(item.File);
                    string itemName = item.Name;
                    SoundChoice(content, itemName, current == id, () =>
                    {
                        PhoneSounds.StopPreview();
                        PhoneTheme.SetUiCueFile(key, id);
                        ShowSounds();
                    }, () =>
                    {
                        if (key == "vibrate")
                        {
                            PhoneSfx.PreviewFile(key, id);
                            return;
                        }
                        PhoneSounds.TogglePreview(path);
                        ShowUiFiles(key);
                    });
                }
            }

            private static void SoundChoice(RectTransform content, string label, bool current, UnityEngine.Events.UnityAction pick, UnityEngine.Events.UnityAction play)
            {
                var row = new GameObject("Pick", typeof(RectTransform));
                row.transform.SetParent(content, false);
                PhoneUi.Size(row, 40f);
                PhoneUi.AddHorizontal(row, 6f);
                var btn = PhoneUi.CreateButton(row.transform, PhoneTones.Mark(label, current), pick, new Vector2(220f, 36f));
                var le = btn.GetComponent<LayoutElement>();
                if (le != null)
                    le.flexibleWidth = 1f;
                PhoneUi.MaterialChip(row.transform, "play", "Play", play, new Vector2(40f, 36f));
            }

            private void ShowHomeScreen()
            {
                _page = "homescreen";
                RectTransform content = BeginPage("Home screen", ShowCustomize);
                Link(content, "shelf_auto_hide", "Dock", "Choose the apps on the home dock", ShowDock);
                Link(content, "mobile_3", "Navigation", "The bar along the edge of the screen", ShowNavigation);
                Link(content, "wallpaper", "Wallpaper", "A photo or GIF behind the home icons", ShowWallpaper);
            }

            public void ShowCustomize()
            {
                _page = "customize";
                RectTransform content = BeginPage("Personalize", ShowHome);
                Link(content, "palette", "Style", "Colors, case, and icon style", ShowLook);
                Link(content, "language", PhoneLang.T("language", "Language"), "Words on the phone", ShowLanguage);
                Link(content, "nest_clock_farsight_digital", "Clock", "Time on the status bar", ShowClock);
                Link(content, "capture", PhoneLang.T("buttons", "Buttons"), "Side keys and the ringer", ShowButtons);
                Link(content, "toolbar", "Toolbar", "The row under the screen", ShowToolbar);
                Link(content, "display_settings", "Display", "Size, position, and brightness", ShowDisplay);
                Link(content, "home", "Home screen", "Dock, navigation, and wallpaper", ShowHomeScreen);
                Link(content, "instant_mix", "Sounds", "Typing and backspace", ShowSounds);
                Link(content, "notifications", "Notifications", "Alerts and ringtones", ShowNotifications);
            }

            private void ShowNotifications()
            {
                _page = "notifications";
                PhoneSounds.StopPreview();
                RectTransform content = BeginPage("Notifications", ShowCustomize);
                IconToggle(content, "music_cast", "Fade music on alerts", PhoneTones.DuckMusic, () =>
                {
                    PhoneTones.SetDuckMusic(!PhoneTones.DuckMusic);
                    ShowNotifications();
                });

                var hint = PhoneUi.CreateLabel(content, "Hint", "These are the defaults until you set a per-person sound. Phone is the ringtone, Messages is the text tone. Drop files in the alerts folder. Each row can go back to Default.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 48f);

                var grid = MakeGrid(content, 76f, 72f);
                PiPhoneApp[] apps = PiPhoneApi.GetApps();
                for (int i = 0; i < apps.Length; i++)
                {
                    PiPhoneApp app = apps[i];
                    if (app == null || !app.PostsNotices)
                        continue;
                    string id = app.Id;
                    var cell = new GameObject("N", typeof(RectTransform));
                    cell.transform.SetParent(grid, false);
                    var v = PhoneUi.AddVertical(cell, 4f, new RectOffset(0, 0, 0, 0));
                    v.childAlignment = TextAnchor.UpperCenter;
                    v.childForceExpandWidth = false;
                    v.childForceExpandHeight = false;
                    var icon = PhoneIcons.CreateView(cell.transform, app, 48f, false);
                    PhoneUi.SetClickable(icon.gameObject, true);
                    var open = icon.gameObject.AddComponent<Button>();
                    open.targetGraphic = icon.GetComponent<Image>();
                    string appId = id;
                    PhoneSfx.BindPress(open, () => ShowTones("app", appId, null, true));
                    PhoneUi.SetTooltip(icon.gameObject, PhoneLang.AppName(app) + "\n" + PhoneTheme.ToneName(PhoneTones.ResolveApp(id)));
                    bool on = PhoneTheme.AppNoticesOn(id);
                    var tog = PhoneUi.CreateToggleChip(cell.transform, on, null);
                    PhoneUi.SetTooltip(tog.gameObject, PhoneLang.AppName(app) + (on ? " alerts on" : " alerts off"));
                    tog.onClick.AddListener(() =>
                    {
                        bool next = !PhoneTheme.AppNoticesOn(appId);
                        PhoneTheme.SetAppNotices(appId, next);
                        Sprite sprite = PhoneIcons.Material(next ? "toggle_on" : "toggle_off");
                        Transform art = tog.transform.Find("I");
                        var artImg = art != null ? art.GetComponent<Image>() : null;
                        if (artImg != null)
                            artImg.sprite = sprite;
                        PhoneUi.TintToggle(tog, next);
                        PhoneUi.SetTooltip(tog.gameObject, PhoneLang.AppName(app) + (next ? " alerts on" : " alerts off"));
                    });
                }
            }

            private void ShowTones(string kind, string appId, string contactId, bool contactRing)
            {
                _page = "tones";
                _toneKind = kind ?? "notify";
                _toneAppId = appId;
                _toneContactId = contactId;
                _toneContactRing = contactRing;
                RectTransform content = BeginPage("Choose sound", ShowNotifications);
                string stored;
                string effective;
                ToneSelection(out stored, out effective);
                var usingLine = PhoneUi.CreateLabel(content, "Using", "Using " + PhoneTheme.ToneName(effective), 14f, FontStyles.Normal, TextAlignmentOptions.Center);
                usingLine.color = PhoneUi.TextDim;
                PhoneUi.Size(usingLine.gameObject, 24f);
                PhoneUi.CreateButton(content, PhoneTones.Mark("Default", string.IsNullOrEmpty(stored)), () =>
                {
                    ApplyTone(string.Empty);
                    ShowNotifications();
                }, new Vector2(220f, 40f));
                System.Collections.Generic.List<SoundItem> tones = PhoneStore.AlertTones();
                if (tones.Count == 0)
                {
                    var empty = PhoneUi.CreateLabel(content, "Empty", "Add alert sounds in MakeNoti, or drop files in the alerts folder.", 14f, FontStyles.Normal, TextAlignmentOptions.Center);
                    empty.color = PhoneUi.TextDim;
                    PhoneUi.Wrap(empty);
                    PhoneUi.Size(empty.gameObject, 40f);
                    return;
                }
                PhoneTones.FillPicker(content, () => ShowTones(_toneKind, _toneAppId, _toneContactId, _toneContactRing), captured =>
                {
                    ApplyTone(captured.Id);
                    ShowNotifications();
                }, stored, ToneChannel());
            }

            private PhoneAudioChannel ToneChannel()
            {
                bool ring;
                if (!string.IsNullOrEmpty(_toneContactId))
                    ring = _toneContactRing;
                else if (!string.IsNullOrEmpty(_toneAppId))
                    ring = _toneAppId == BuiltinApps.PhoneId;
                else
                    ring = _toneKind == "ringtone";
                return ring ? PhoneAudioChannel.Ringtone : PhoneAudioChannel.Notification;
            }

            private void ToneSelection(out string stored, out string effective)
            {
                stored = string.Empty;
                effective = string.Empty;
                if (!string.IsNullOrEmpty(_toneContactId))
                {
                    if (_toneContactRing)
                    {
                        stored = PhoneTones.ContactRing(_toneContactId);
                        effective = PhoneTones.ResolveRing(_toneContactId);
                    }
                    else
                    {
                        stored = PhoneTones.ContactText(_toneContactId);
                        effective = PhoneTones.ResolveText(_toneContactId);
                    }
                    return;
                }
                if (!string.IsNullOrEmpty(_toneAppId))
                {
                    stored = PhoneTones.AppTone(_toneAppId);
                    effective = PhoneTones.ResolveApp(_toneAppId);
                    return;
                }
                if (_toneKind == "ringtone")
                    stored = PhoneTheme.RingtoneId;
                else if (_toneKind == "text")
                    stored = PhoneTheme.TextToneId;
                else
                    stored = PhoneTheme.NotifyToneId;
                effective = stored;
                if (_toneKind == "text" && string.IsNullOrEmpty(effective))
                    effective = PhoneTheme.NotifyToneId;
            }

            private void ApplyTone(string soundId)
            {
                if (!string.IsNullOrEmpty(_toneContactId))
                {
                    if (_toneContactRing)
                        PhoneTones.SetContactRing(_toneContactId, soundId);
                    else
                        PhoneTones.SetContactText(_toneContactId, soundId);
                    return;
                }
                if (!string.IsNullOrEmpty(_toneAppId))
                {
                    PhoneTones.SetAppTone(_toneAppId, soundId);
                    return;
                }
                PhoneTheme.SetTone(_toneKind, soundId);
            }

            private void ShowDock()
            {
                _page = "dock";
                RectTransform content = BeginPage("Dock", ShowHomeScreen);
                IconToggle(content, "shelf_auto_hide", PhoneTheme.HideDock ? "Show dock" : "Hide dock", !PhoneTheme.HideDock, () =>
                {
                    PhoneTheme.SetHideDock(!PhoneTheme.HideDock);
                    ShowDock();
                });
                var hint = PhoneUi.CreateLabel(content, "Hint", "Pick up to four apps for the home dock. You can also hold an app on the home screen.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 40f);
                var grid = MakeGrid(content, 76f, 72f);
                PiPhoneApp[] apps = PiPhoneApi.GetInstalledApps();
                for (int i = 0; i < apps.Length; i++)
                {
                    PiPhoneApp app = apps[i];
                    if (app == null)
                        continue;
                    string id = app.Id;
                    var cell = new GameObject("D", typeof(RectTransform));
                    cell.transform.SetParent(grid, false);
                    var v = PhoneUi.AddVertical(cell, 4f, new RectOffset(0, 0, 0, 0));
                    v.childAlignment = TextAnchor.UpperCenter;
                    v.childForceExpandWidth = false;
                    v.childForceExpandHeight = false;
                    var icon = PhoneIcons.CreateView(cell.transform, app, 48f, false);
                    PhoneUi.SetTooltip(icon.gameObject, PhoneLang.AppName(app));
                    bool on = PhoneStore.IsOnDock(id);
                    var tog = PhoneUi.CreateToggleChip(cell.transform, on, null);
                    PhoneUi.SetTooltip(tog.gameObject, PhoneLang.AppName(app) + (on ? " on the dock" : " off the dock"));
                    string appId = id;
                    tog.onClick.AddListener(() =>
                    {
                        bool next = !PhoneStore.IsOnDock(appId);
                        if (!next)
                            PhoneStore.RemoveDock(appId);
                        else if (!PhoneStore.AddDock(appId))
                        {
                            _host.ShowToast("Dock is full (4 apps).");
                            return;
                        }
                        PhoneMenu.OnAppsChanged();
                        Transform art = tog.transform.Find("I");
                        var artImg = art != null ? art.GetComponent<Image>() : null;
                        if (artImg != null)
                            artImg.sprite = PhoneIcons.Material(next ? "toggle_on" : "toggle_off");
                        PhoneUi.TintToggle(tog, next);
                        PhoneUi.SetTooltip(tog.gameObject, PhoneLang.AppName(app) + (next ? " on the dock" : " off the dock"));
                    });
                }
            }

            private void ShowNavigation()
            {
                _page = "nav";
                RectTransform content = BeginPage("Navigation", ShowHomeScreen);
                IconToggle(content, "screen_rotation", "Rotate button", PhoneTheme.NavRotateButton, () =>
                {
                    PhoneTheme.SetNavRotateButton(!PhoneTheme.NavRotateButton);
                    ShowNavigation();
                });
                var hint = PhoneUi.CreateLabel(content, "Hint", "Back, home, and recent apps stay on the bar. The rotate button turns the phone sideways.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 48f);
            }

            private void ShowWallpaper()
            {
                _page = "wallpaper";
                RectTransform content = BeginPage("Wallpaper", ShowHomeScreen);
                PhoneUi.CreateButton(content, "Default background", () =>
                {
                    PhoneTheme.SetWallpaper(string.Empty);
                    _host.ShowToast("Default background restored.");
                    ShowWallpaper();
                }, new Vector2(240f, 40f));
                var hint = PhoneUi.CreateLabel(content, "Hint", "Pick a photo or GIF already on this phone. New pictures come from the camera and downloads.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 40f);
                int shown = 0;
                for (int i = 0; i < PhoneStore.Photos.Count; i++)
                {
                    PhotoItem photo = PhoneStore.Photos[i];
                    if (photo == null || photo.Video || string.IsNullOrEmpty(photo.File))
                        continue;
                    string rel = "photos/" + photo.File;
                    AddWallChoice(content, photo.File, rel);
                    shown++;
                }
                for (int i = 0; i < PhoneStore.Downloads.Count; i++)
                {
                    DownloadItem item = PhoneStore.Downloads[i];
                    if (item == null || string.IsNullOrEmpty(item.File) || !WallFile(item.File))
                        continue;
                    string rel = "downloads/" + item.File;
                    AddWallChoice(content, item.File, rel);
                    shown++;
                }
                if (shown == 0)
                {
                    var empty = PhoneUi.CreateLabel(content, "Empty", "No photos or GIFs on this phone yet.", 14f, FontStyles.Normal, TextAlignmentOptions.Center);
                    empty.color = PhoneUi.TextDim;
                    PhoneUi.Size(empty.gameObject, 28f);
                }
            }

            private void AddWallChoice(Transform parent, string label, string relative)
            {
                bool current = string.Equals(PhoneTheme.WallpaperFile, relative, StringComparison.OrdinalIgnoreCase);
                string title = (current ? "Using  " : string.Empty) + label;
                PhoneUi.CreateButton(parent, title, () =>
                {
                    PhoneTheme.SetWallpaper(relative);
                    _host.ShowToast("Wallpaper set.");
                    ShowWallpaper();
                }, new Vector2(280f, 40f));
            }

            private static bool WallFile(string file)
            {
                if (string.IsNullOrEmpty(file))
                    return false;
                string lower = file.ToLowerInvariant();
                return lower.EndsWith(".png") || lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") || lower.EndsWith(".gif") || lower.EndsWith(".webp");
            }

            private void ShowLook()
            {
                _page = "look";
                ColorChoice[] choices = LookChoices();
                if (_lookIndex < 0 || _lookIndex >= choices.Length)
                    _lookIndex = 0;
                ScrollPage("Style", content =>
                {
                    var hint = PhoneUi.CreateLabel(content, "Hint", "Pick what to color, then use the wheel. Brightness and opacity apply to that one color.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                    hint.color = PhoneUi.TextDim;
                    PhoneUi.Wrap(hint);
                    PhoneUi.Size(hint.gameObject, 40f);
                    AddColorChooser(content, choices, () => _lookIndex, i => _lookIndex = i, () =>
                    {
                        PhoneTheme.ResetLook();
                        _lookIndex = 0;
                        ShowLook();
                    });
                    PhoneUi.CreateSliderRow(content, "Font size", 0.7f, 1.4f, PhoneTheme.FontScale, v => PhoneTheme.SetFontScale(v), FontPercent, "format_size");
                    var iconStyle = PhoneUi.CreateIconChip(content, "Icons", PhoneIcons.Material("palette"), () =>
                    {
                        PhoneTheme.SetFilledIcons(!PhoneTheme.FilledIcons);
                        ShowLook();
                    }, false, new Vector2(40f, 36f));
                    PhoneUi.SetTooltip(iconStyle.gameObject, PhoneTheme.FilledIcons ? "Filled icons" : "Outline icons");
                });
            }

            private static ColorChoice[] LookChoices()
            {
                return new[]
                {
                    new ColorChoice("Screen", () => PhoneTheme.ScreenColor, PhoneTheme.SetScreenColor, () => PhoneTheme.SetScreenColor(PhoneTheme.DefaultScreen)),
                    new ColorChoice("Cards", () => PhoneTheme.SurfaceColor, PhoneTheme.SetSurfaceColor, () => PhoneTheme.SetSurfaceColor(PhoneTheme.DefaultSurface)),
                    new ColorChoice("Case", () => PhoneTheme.CaseColor, PhoneTheme.SetCaseColor, PhoneTheme.ResetCase),
                    new ColorChoice("Nav bar", () => PhoneTheme.NavColor, PhoneTheme.SetNavColor, () => PhoneTheme.SetNavColor(PhoneTheme.DefaultNav)),
                    new ColorChoice("Nav buttons", () => PhoneTheme.NavButtonColor, PhoneTheme.SetNavButtonColor, () => PhoneTheme.SetNavButtonColor(PhoneTheme.DefaultNavButton)),
                    new ColorChoice("Nav icons", () => PhoneTheme.NavIconColor, PhoneTheme.SetNavIconColor, () => PhoneTheme.SetNavIconColor(PhoneTheme.DefaultNavIcon)),
                    new ColorChoice("Text", () => PhoneTheme.TextColor, PhoneTheme.SetTextColor, () => PhoneTheme.SetTextColor(PhoneTheme.DefaultText)),
                    new ColorChoice("Clock", () => PhoneTheme.ClockColor, PhoneTheme.SetClockColor, PhoneTheme.ResetClockColor),
                    new ColorChoice("Battery", () => PhoneTheme.BatteryColor, PhoneTheme.SetBatteryColor, PhoneTheme.ResetBatteryColor),
                    new ColorChoice("Signal", () => PhoneTheme.SignalColor, PhoneTheme.SetSignalColor, PhoneTheme.ResetSignalColor),
                    new ColorChoice("Accent", () => PhoneTheme.AccentColor, PhoneTheme.SetAccentColor, () => PhoneTheme.SetAccentColor(PhoneTheme.DefaultAccent)),
                    new ColorChoice("App icon glyphs", () => PhoneTheme.IconColor, PhoneTheme.SetIconColor, () => PhoneTheme.SetIconColor(PhoneTheme.DefaultIcon))
                };
            }

            private void ShowLanguage()
            {
                _page = "language";
                ScrollPage(PhoneLang.T("language", "Language"), content =>
                {
                    var hint = PhoneUi.CreateLabel(content, "Hint", PhoneLang.T("lang_hint", "Other mods can add packs with PiPhoneApi.RegisterLanguage, or drop key=value files in the lang folder."), 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                    hint.color = PhoneUi.TextDim;
                    PhoneUi.Wrap(hint);
                    PhoneUi.Size(hint.gameObject, 56f);
                    bool usingSystem = string.IsNullOrEmpty(PhoneTheme.Language);
                    PhoneUi.CreateButton(content, PhoneLang.T("system_language", "Use system language") + (usingSystem ? "  ✓" : string.Empty), () =>
                    {
                        PhoneLang.UseSystem();
                        ShowLanguage();
                    }, new Vector2(280f, 40f));
                    PiPhoneLanguage[] packs = PhoneLang.All();
                    for (int i = 0; i < packs.Length; i++)
                    {
                        PiPhoneLanguage pack = packs[i];
                        if (pack == null || string.IsNullOrEmpty(pack.Code))
                            continue;
                        string code = pack.Code;
                        string label = string.IsNullOrEmpty(pack.Name) ? code : pack.Name;
                        bool on = !usingSystem && string.Equals(PhoneLang.Code, code, System.StringComparison.OrdinalIgnoreCase);
                        PhoneUi.CreateButton(content, on ? label + "  ✓" : label, () =>
                        {
                            PhoneLang.Set(code);
                            ShowLanguage();
                        }, new Vector2(280f, 40f));
                    }
                });
            }

            private void ShowClock()
            {
                _page = "clock";
                ScrollPage("Clock", content =>
                {
                    Toggle(content, "24-hour clock", PhoneTheme.Clock24Hour, () => PhoneTheme.SetClock24Hour(true));
                    Toggle(content, "AM/PM clock", !PhoneTheme.Clock24Hour, () => PhoneTheme.SetClock24Hour(false));
                    Toggle(content, "Real-world time", !PhoneTheme.UsePeakTime, () => PhoneTheme.SetPeakTime(false));
                    Toggle(content, "PEAK time", PhoneTheme.UsePeakTime, () => PhoneTheme.SetPeakTime(true));
                    Toggle(content, "Hide date", PhoneTheme.HideDate, () => PhoneTheme.SetHideDate(!PhoneTheme.HideDate));
                    var hint = PhoneUi.CreateLabel(content, "Hint", "PEAK time follows the in-game day/night cycle and never shows a date. Clock color is in Style.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                    hint.color = PhoneUi.TextDim;
                    PhoneUi.Wrap(hint);
                    PhoneUi.Size(hint.gameObject, 56f);
                });
            }

            private void ShowButtons()
            {
                _page = "buttons";
                ColorChoice[] choices = ButtonChoices();
                if (_buttonIndex < 0 || _buttonIndex >= choices.Length)
                    _buttonIndex = 0;
                ScrollPage("Buttons", content =>
                {
                    AddColorChooser(content, choices, () => _buttonIndex, i => _buttonIndex = i, () =>
                    {
                        PhoneTheme.ResetButtons();
                        _buttonIndex = 0;
                        ShowButtons();
                    });
                    CornerChooser(content);
                    var shapeHint = PhoneUi.CreateLabel(content, "SH", "0 is a square. Switch between Buttons and Apps.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                    shapeHint.color = PhoneUi.TextDim;
                    PhoneUi.Wrap(shapeHint);
                    PhoneUi.Size(shapeHint.gameObject, 36f);
                    ColorChoice[] toggles = ToggleChoices();
                    if (_toggleIndex < 0 || _toggleIndex >= toggles.Length)
                        _toggleIndex = 0;
                    var toggleHint = PhoneUi.CreateLabel(content, "TH", "Toggle colors", 14f, FontStyles.Bold, TextAlignmentOptions.Center);
                    PhoneUi.Size(toggleHint.gameObject, 22f);
                    AddColorChooser(content, toggles, () => _toggleIndex, i => _toggleIndex = i, () =>
                    {
                        PhoneTheme.SetToggleOnColor(PhoneTheme.DefaultToggleOn);
                        PhoneTheme.SetToggleOffColor(PhoneTheme.DefaultToggleOff);
                        _toggleIndex = 0;
                        ShowButtons();
                    });
                });
            }

            private static ColorChoice[] ToggleChoices()
            {
                return new[]
                {
                    new ColorChoice("On", () => PhoneTheme.ToggleOnColor, PhoneTheme.SetToggleOnColor, () => PhoneTheme.SetToggleOnColor(PhoneTheme.DefaultToggleOn)),
                    new ColorChoice("Off", () => PhoneTheme.ToggleOffColor, PhoneTheme.SetToggleOffColor, () => PhoneTheme.SetToggleOffColor(PhoneTheme.DefaultToggleOff))
                };
            }

            private static ColorChoice[] ButtonChoices()
            {
                return new[]
                {
                    new ColorChoice("Button color", () => PhoneTheme.ButtonFillColor, PhoneTheme.SetButtonFill, () => PhoneTheme.SetButtonFill(PhoneTheme.DefaultButtonFill)),
                    new ColorChoice("Button font", () => PhoneTheme.ButtonFontColor, PhoneTheme.SetButtonFont, () => PhoneTheme.SetButtonFont(PhoneTheme.DefaultButtonFont))
                };
            }

            private PhoneColorPicker AddColorChooser(Transform parent, ColorChoice[] choices, Func<int> getIndex, Action<int> setIndex, Action resetAll)
            {
                int index = getIndex();
                if (choices == null || choices.Length == 0)
                    return null;
                if (index < 0 || index >= choices.Length)
                    index = 0;
                TextMeshProUGUI name = null;
                PhoneColorPicker picker = null;
                if (choices.Length > 1)
                {
                    var row = new GameObject("Chooser", typeof(RectTransform));
                    row.transform.SetParent(parent, false);
                    PhoneUi.Size(row, 40f);
                    var layout = PhoneUi.AddHorizontal(row, 8f);
                    layout.childForceExpandWidth = false;
                    layout.childAlignment = TextAnchor.MiddleCenter;
                    var prev = PhoneUi.CreateIconChip(row.transform, "Previous", PhoneIcons.Material("chevron_left"), () => Step(-1), false, new Vector2(32f, 32f));
                    PhoneUi.SetTooltip(prev.gameObject, "Previous");
                    name = PhoneUi.CreateLabel(row.transform, "Name", choices[index].Name, 15f, FontStyles.Normal, TextAlignmentOptions.Center);
                    PhoneUi.Size(name.gameObject, 32f, 160f);
                    _chooserName = name;
                    ResetChip(row.transform, "refresh", "Default", () =>
                    {
                        int current = getIndex();
                        if (current < 0 || current >= choices.Length || choices[current].Reset == null)
                            return;
                        choices[current].Reset();
                        if (name != null)
                            name.text = choices[current].Name;
                        if (picker != null)
                            picker.SetColor(choices[current].Get(), false);
                    });
                    var next = PhoneUi.CreateIconChip(row.transform, "Next", PhoneIcons.Material("chevron_right"), () => Step(1), false, new Vector2(32f, 32f));
                    PhoneUi.SetTooltip(next.gameObject, "Next");
                    if (resetAll != null)
                        ResetChip(row.transform, "reset_settings", "Default all", () => resetAll());
                }
                picker = PhoneColorPicker.Create(parent, choices[index].Get(), c =>
                {
                    int current = getIndex();
                    if (current < 0 || current >= choices.Length)
                        return;
                    choices[current].Set(c);
                });
                return picker;

                void Step(int delta)
                {
                    if (picker == null || choices.Length < 2)
                        return;
                    int next = getIndex() + delta;
                    if (next < 0)
                        next = choices.Length - 1;
                    if (next >= choices.Length)
                        next = 0;
                    setIndex(next);
                    if (name != null)
                        name.text = choices[next].Name;
                    picker.SetColor(choices[next].Get(), false);
                }
            }

            private sealed class ColorChoice
            {
                public readonly string Name;
                public readonly Func<Color> Get;
                public readonly Action<Color> Set;
                public readonly Action Reset;

                public ColorChoice(string name, Func<Color> get, Action<Color> set, Action reset)
                {
                    Name = name;
                    Get = get;
                    Set = set;
                    Reset = reset;
                }
            }

            private void ShowToolbar()
            {
                _page = "toolbar";
                Clear();
                _host.SetTitle("Toolbar");
                PhoneUi.MaterialChip(_host.Content, "arrow_back", "Back", ShowCustomize, new Vector2(36f, 32f));
                ScrollRect scroll = PhoneUi.CreateScrollView(_host.Content, out RectTransform content);
                scroll.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
                PhoneUi.AddVertical(content.gameObject, 8f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(content.gameObject);
                var grid = MakeGrid(content, 76f, 60f);
                PiPhoneShadeButton[] buttons = PiPhoneApi.GetShadeButtons();
                for (int i = 0; i < buttons.Length; i++)
                {
                    PiPhoneShadeButton button = buttons[i];
                    if (button == null || !button.CanHide)
                        continue;
                    string id = button.Id;
                    string tip = string.IsNullOrEmpty(button.Tooltip) ? button.Label : button.Tooltip;
                    if (string.IsNullOrEmpty(tip))
                        tip = id;
                    bool on = PhoneTheme.ShadeButtonOn(id, button.DefaultVisible);
                    var cell = new GameObject("T", typeof(RectTransform));
                    cell.transform.SetParent(grid, false);
                    var v = PhoneUi.AddVertical(cell, 2f, new RectOffset(0, 0, 0, 0));
                    v.childAlignment = TextAnchor.UpperCenter;
                    v.childForceExpandWidth = false;
                    v.childForceExpandHeight = false;
                    Sprite icon = ShadeIcon(button);
                    string glyph = string.IsNullOrEmpty(button.Glyph) ? tip : button.Glyph;
                    var chip = PhoneUi.CreateIconChip(cell.transform, glyph, icon, () =>
                    {
                        PiPhoneApi.SetShadeButtonVisible(id, !PhoneTheme.ShadeButtonOn(id, button.DefaultVisible));
                        ShowToolbar();
                    }, false, new Vector2(40f, 36f));
                    PhoneUi.SetTooltip(chip.gameObject, tip);
                    var tog = PhoneUi.CreateToggleChip(cell.transform, on, () =>
                    {
                        PiPhoneApi.SetShadeButtonVisible(id, !PhoneTheme.ShadeButtonOn(id, button.DefaultVisible));
                        ShowToolbar();
                    });
                    PhoneUi.SetTooltip(tog.gameObject, tip + (on ? " on" : " off"));
                }
            }

            private static Sprite ShadeIcon(PiPhoneShadeButton button)
            {
                if (button == null)
                    return null;
                if (button.IconFn != null)
                {
                    try
                    {
                        Sprite sprite = button.IconFn();
                        if (sprite != null)
                            return sprite;
                    }
                    catch (Exception ex)
                    {
                        Plugin.LogError("Shade button '" + button.Id + "' Icon: " + ex.Message);
                    }
                }
                return button.Icon;
            }

            private void ShowDisplay()
            {
                if (!_displayHold)
                {
                    _displayHold = true;
                    _savedLandscape = _host.IsLandscape;
                }
                _page = "display";
                bool land = _host.IsLandscape;
                RectTransform content = BeginPage("Display", LeaveDisplay);
                var row = new GameObject("Orient", typeof(RectTransform));
                row.transform.SetParent(content, false);
                PhoneUi.Size(row, 40f);
                var layout = PhoneUi.AddHorizontal(row, 8f);
                layout.childForceExpandWidth = false;
                layout.childAlignment = TextAnchor.MiddleCenter;
                var prevOrient = PhoneUi.CreateIconChip(row.transform, "Previous", PhoneIcons.Material("chevron_left"), () => PreviewOrientation(!land), false, new Vector2(32f, 32f));
                PhoneUi.SetTooltip(prevOrient.gameObject, "Previous");
                var name = PhoneUi.CreateLabel(row.transform, "Name", land ? "Landscape" : "Vertical", 15f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Size(name.gameObject, 32f, 140f);
                ResetChip(row.transform, "refresh", "Default position", ResetCurrentPosition);
                var nextOrient = PhoneUi.CreateIconChip(row.transform, "Next", PhoneIcons.Material("chevron_right"), () => PreviewOrientation(!land), false, new Vector2(32f, 32f));
                PhoneUi.SetTooltip(nextOrient.gameObject, "Next");
                if (land)
                    PhoneUi.CreateSliderRow(content, "Size", 0.55f, 1.8f, PhoneTheme.PhoneScaleLand, v => PhoneTheme.SetPhoneScaleLand(v), null, "aspect_ratio");
                else
                    PhoneUi.CreateSliderRow(content, "Size", 0.55f, 1.35f, PhoneTheme.PhoneScale, v => PhoneTheme.SetPhoneScale(v), null, "aspect_ratio");
                var hint = PhoneUi.CreateLabel(content, "Hint", "Switching this turns the phone so you can see that size. Leaving Display puts it back the way it was. Drag the rim to move it. The reset icon clears the position you are looking at.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 64f);
                PhoneUi.CreateSliderRow(content, "Brightness", PhoneTheme.MinBrightness, 1f, PhoneTheme.Brightness, v => PhoneTheme.SetBrightness(v), null, "brightness_6");
            }

            private void LeaveDisplay()
            {
                _page = "customize";
                EndDisplayPreview();
                ShowCustomize();
            }

            private void PreviewOrientation(bool land)
            {
                if (PhoneMenu.IsLandscape == land)
                {
                    ShowDisplay();
                    return;
                }
                PhoneMenu.SetUserLandscape(land);
            }

            private void ResetCurrentPosition()
            {
                if (_host.IsLandscape)
                    PhoneTheme.SetPhonePosLand(0f, 0f);
                else
                    PhoneTheme.SetPhonePos(0f, 0f);
                PhoneMenu.ApplyPlacement();
            }

            internal void EndDisplayPreview()
            {
                if (!_displayHold)
                    return;
                _displayHold = false;
                bool restore = _savedLandscape;
                if (PhoneMenu.IsLandscape != restore)
                    PhoneMenu.SetUserLandscape(restore);
            }

            private static void ResetChip(Transform parent, string icon, string tip, UnityEngine.Events.UnityAction click)
            {
                var btn = PhoneUi.CreateIconChip(parent, tip, PhoneIcons.Material(icon), click, false, new Vector2(32f, 32f));
                PhoneUi.SetTooltip(btn.gameObject, tip);
            }

            private static string FontPercent(float value)
            {
                float t = (value - 0.7f) / 0.7f;
                return Mathf.RoundToInt(Mathf.Clamp01(t) * 100f) + "%";
            }

            private void CornerChooser(Transform parent)
            {
                var row = new GameObject("Corners", typeof(RectTransform));
                row.transform.SetParent(parent, false);
                PhoneUi.Size(row, 40f);
                var layout = PhoneUi.AddHorizontal(row, 8f);
                layout.childForceExpandWidth = false;
                layout.childAlignment = TextAnchor.MiddleCenter;
                var prev = PhoneUi.CreateIconChip(row.transform, "Previous", PhoneIcons.Material("chevron_left"), () =>
                {
                    _cornerApps = !_cornerApps;
                    ShowButtons();
                }, false, new Vector2(32f, 32f));
                PhoneUi.SetTooltip(prev.gameObject, "Previous");
                var name = PhoneUi.CreateLabel(row.transform, "Name", _cornerApps ? "Apps" : "Buttons", 15f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Size(name.gameObject, 32f, 120f);
                ResetChip(row.transform, "refresh", "Default", () =>
                {
                    if (_cornerApps)
                        PhoneTheme.SetIconRadius(PhoneTheme.DefaultIconRadius);
                    else
                        PhoneTheme.SetButtonRadius(PhoneTheme.DefaultButtonRadius);
                    ShowButtons();
                });
                var next = PhoneUi.CreateIconChip(row.transform, "Next", PhoneIcons.Material("chevron_right"), () =>
                {
                    _cornerApps = !_cornerApps;
                    ShowButtons();
                }, false, new Vector2(32f, 32f));
                PhoneUi.SetTooltip(next.gameObject, "Next");
                if (_cornerApps)
                    PhoneUi.CreateSliderRow(parent, "App icon corners", 0f, 28f, PhoneTheme.IconRadius, v => PhoneTheme.SetIconRadius(Mathf.RoundToInt(v)), CornerLabel, "rounded_corner");
                else
                    PhoneUi.CreateSliderRow(parent, "Button corners", 0f, 28f, PhoneTheme.ButtonRadius, v => PhoneTheme.SetButtonRadius(Mathf.RoundToInt(v)), CornerLabel, "rounded_corner");
            }

            private static string CornerLabel(float value)
            {
                int n = Mathf.RoundToInt(value);
                return n == 0 ? "Square" : n + "px";
            }

            private void ShowControls()
            {
                _page = "controls";
                RectTransform content = BeginPage("Controls", ShowHome);

                IconToggle(content, "lift_to_talk", "Auto-answer calls", PhoneTheme.AutoAnswer, () =>
                {
                    PhoneTheme.SetAutoAnswer(!PhoneTheme.AutoAnswer);
                    ShowControls();
                });
                IconToggle(content, "variable_add", "Pause menu button", Plugin.GetShowPauseMenuButton(), () =>
                {
                    Plugin.SetShowPauseMenuButton(!Plugin.GetShowPauseMenuButton());
                    ShowControls();
                });
                IconAction(content, "toast", "Place closed-phone alerts", () =>
                {
                    AlertHud.TogglePlacement();
                    ShowControls();
                }, "refresh", "Reset placement", () => AlertHud.ResetPlace());

                PiPhoneKeybind[] binds = PiPhoneApi.GetKeybinds();
                string group = null;
                for (int i = 0; i < binds.Length; i++)
                {
                    PiPhoneKeybind bind = binds[i];
                    if (bind == null)
                        continue;
                    string g = string.IsNullOrEmpty(bind.Group) ? "Apps" : bind.Group;
                    if (g != group)
                    {
                        group = g;
                        var head = PhoneUi.CreateLabel(content, "G", group, 14f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                        PhoneUi.Size(head.gameObject, 22f);
                    }
                    string id = bind.Id;
                    var row = new GameObject("K", typeof(RectTransform));
                    row.transform.SetParent(content, false);
                    PhoneUi.Size(row, 40f);
                    var keyLayout = PhoneUi.AddHorizontal(row, 6f);
                    keyLayout.childForceExpandWidth = false;
                    string keyText = Plugin.CapturingHotkey ? "..." : PhoneKeys.Format(id);
                    var keyBtn = PhoneUi.CreateIconChip(row.transform, keyText, PhoneIcons.Material(KeyIcon(id)), () =>
                    {
                        PhoneKeys.BeginCapture(id, () =>
                        {
                            if (_live != null)
                                ShowControls();
                        });
                        _host.ShowToast(bind.WithModifier
                            ? "Press a key. Hold Ctrl, Alt, or Shift. Esc cancels."
                            : "Press a key. Esc cancels.");
                        ShowControls();
                    }, false, new Vector2(36f, 32f));
                    PhoneUi.SetTooltip(keyBtn.gameObject, bind.Label);
                    var keyName = PhoneUi.CreateLabel(row.transform, "Key", string.IsNullOrEmpty(keyText) ? "None" : keyText, 14f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                    keyName.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                    if (!bind.Required)
                    {
                        var clear = PhoneUi.CreateIconChip(row.transform, "None", PhoneIcons.Material("close"), () =>
                        {
                            PhoneKeys.Clear(id);
                            ShowControls();
                        }, false, new Vector2(32f, 32f));
                        PhoneUi.SetTooltip(clear.gameObject, "Clear " + bind.Label);
                    }
                }
            }

            private void ScrollPage(string title, System.Action<RectTransform> fill)
            {
                RectTransform content = BeginPage(title, ShowCustomize);
                fill(content);
            }

            private RectTransform BeginPage(string title, UnityEngine.Events.UnityAction back)
            {
                Clear();
                _host.SetTitle(title);
                ScrollRect scroll = PhoneUi.CreateScrollView(_host.Content, out RectTransform content);
                var le = scroll.gameObject.AddComponent<LayoutElement>();
                le.flexibleHeight = 1f;
                le.minHeight = 80f;
                PhoneUi.AddVertical(content.gameObject, 6f, new RectOffset(8, 8, 4, 12));
                PhoneUi.FitVertical(content.gameObject);
                var bar = new GameObject("Bar", typeof(RectTransform));
                bar.transform.SetParent(content, false);
                PhoneUi.Size(bar, 40f);
                var barLayout = PhoneUi.AddHorizontal(bar, 8f);
                barLayout.childAlignment = TextAnchor.MiddleLeft;
                barLayout.childForceExpandWidth = false;
                if (back != null)
                {
                    var backBtn = PhoneUi.CreateIconChip(bar.transform, "Back", PhoneIcons.Material("arrow_back"), back, false, new Vector2(36f, 36f), true, true);
                    PhoneUi.SetTooltip(backBtn.gameObject, "Back");
                }
                var lab = PhoneUi.CreateLabel(bar.transform, "T", title, 18f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                lab.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                return content;
            }

            private void Link(Transform parent, string icon, string title, string subtitle, UnityEngine.Events.UnityAction go)
            {
                var row = PhoneUi.CreateImage(parent, "Set_" + PhoneUi.Sanitize(title), PhoneUi.Rounded(16), PhoneUi.Surface);
                PhoneUi.Size(row.gameObject, 58f);
                var img = row.GetComponent<Image>();
                img.raycastTarget = true;
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                PhoneSfx.BindPress(btn, go);
                var layout = PhoneUi.AddHorizontal(row.gameObject, 10f);
                layout.padding = new RectOffset(10, 8, 8, 8);
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                Sprite mark = PhoneIcons.Material(icon);
                if (mark != null)
                {
                    var art = PhoneUi.CreateImage(row, "I", mark, PhoneUi.Text);
                    var artLe = art.gameObject.AddComponent<LayoutElement>();
                    artLe.minWidth = 28f;
                    artLe.preferredWidth = 28f;
                    artLe.minHeight = 28f;
                    artLe.preferredHeight = 28f;
                    var artImg = art.GetComponent<Image>();
                    artImg.preserveAspect = true;
                    artImg.raycastTarget = false;
                }
                var stack = new GameObject("Txt", typeof(RectTransform));
                stack.transform.SetParent(row, false);
                var stackLe = stack.AddComponent<LayoutElement>();
                stackLe.flexibleWidth = 1f;
                stackLe.minHeight = 40f;
                PhoneUi.AddVertical(stack, 0f, new RectOffset(0, 0, 0, 0));
                PhoneUi.CreateLabel(stack.transform, "T", title, 16f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                if (!string.IsNullOrEmpty(subtitle))
                {
                    var sub = PhoneUi.CreateLabel(stack.transform, "S", subtitle, 12f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                    sub.color = PhoneUi.TextDim;
                }
                Sprite chev = PhoneIcons.Material("chevron_right");
                if (chev != null)
                {
                    var arrow = PhoneUi.CreateImage(row, "Go", chev, PhoneUi.TextDim);
                    var arrowLe = arrow.gameObject.AddComponent<LayoutElement>();
                    arrowLe.minWidth = 18f;
                    arrowLe.preferredWidth = 18f;
                    arrowLe.minHeight = 18f;
                    arrowLe.preferredHeight = 18f;
                    var arrowImg = arrow.GetComponent<Image>();
                    arrowImg.preserveAspect = true;
                    arrowImg.raycastTarget = false;
                }
            }

            private void OsRow(Transform parent)
            {
                var row = PhoneUi.CreateImage(parent, "Row_Os", PhoneUi.Rounded(14), PhoneUi.Surface);
                PhoneUi.Size(row.gameObject, 40f);
                Sprite logo = PhoneIcons.Logo();
                float left = 12f;
                if (logo != null)
                {
                    var mark = PhoneUi.CreateImage(row, "Logo", logo, Color.white);
                    mark.anchorMin = mark.anchorMax = new Vector2(0f, 0.5f);
                    mark.pivot = new Vector2(0f, 0.5f);
                    mark.sizeDelta = new Vector2(32f, 32f);
                    mark.anchoredPosition = new Vector2(6f, 0f);
                    var graphic = mark.GetComponent<Image>();
                    graphic.preserveAspect = true;
                    graphic.raycastTarget = false;
                    left = 42f;
                }
                var label = PhoneUi.CreateLabel(row, "Key", PiPhoneApi.OsName, 15f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                label.rectTransform.anchorMin = new Vector2(0f, 0f);
                label.rectTransform.anchorMax = new Vector2(0.62f, 1f);
                label.rectTransform.offsetMin = new Vector2(left, 0f);
                label.rectTransform.offsetMax = Vector2.zero;
                var val = PhoneUi.CreateLabel(row, "Val", Plugin.PluginVersion, 14f, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
                val.color = PhoneUi.TextDim;
                val.rectTransform.anchorMin = new Vector2(0.42f, 0f);
                val.rectTransform.anchorMax = new Vector2(1f, 1f);
                val.rectTransform.offsetMin = Vector2.zero;
                val.rectTransform.offsetMax = new Vector2(-78f, 0f);
                var logs = PhoneUi.CreateLabel(row, "Logs", "Logs", 12f, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
                logs.color = PhoneUi.TextDim;
                logs.rectTransform.anchorMin = new Vector2(1f, 0f);
                logs.rectTransform.anchorMax = new Vector2(1f, 1f);
                logs.rectTransform.pivot = new Vector2(1f, 0.5f);
                logs.rectTransform.sizeDelta = new Vector2(36f, 0f);
                logs.rectTransform.anchoredPosition = new Vector2(-40f, 0f);
                var box = PhoneUi.CreateImage(row, "LogBox", PhoneUi.Rounded(6), PhoneTheme.WriteLogs ? PhoneUi.Accent : PhoneUi.SurfaceAlt);
                box.anchorMin = box.anchorMax = new Vector2(1f, 0.5f);
                box.pivot = new Vector2(1f, 0.5f);
                box.sizeDelta = new Vector2(26f, 26f);
                box.anchoredPosition = new Vector2(-8f, 0f);
                var boxImg = box.GetComponent<Image>();
                boxImg.raycastTarget = true;
                var boxBtn = box.gameObject.AddComponent<Button>();
                boxBtn.targetGraphic = boxImg;
                var tick = PhoneUi.CreateLabel(box, "M", PhoneTheme.WriteLogs ? "X" : string.Empty, 14f, FontStyles.Bold, TextAlignmentOptions.Center);
                PhoneUi.Stretch(tick.rectTransform, 0f, 0f);
                PhoneSfx.BindPress(boxBtn, () =>
                {
                    PhoneTheme.SetWriteLogs(!PhoneTheme.WriteLogs);
                    ShowHome();
                });
            }

            private void Row(Transform parent, string key, string value)
            {
                var row = PhoneUi.CreateImage(parent, "Row_" + PhoneUi.Sanitize(key), PhoneUi.Rounded(14), PhoneUi.Surface);
                PhoneUi.Size(row.gameObject, 40f);
                var label = PhoneUi.CreateLabel(row, "Key", key, 15f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                label.rectTransform.anchorMin = new Vector2(0f, 0f);
                label.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                label.rectTransform.offsetMin = new Vector2(12f, 0f);
                label.rectTransform.offsetMax = Vector2.zero;
                var val = PhoneUi.CreateLabel(row, "Val", value, 15f, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
                val.color = PhoneUi.TextDim;
                val.rectTransform.anchorMin = new Vector2(0.45f, 0f);
                val.rectTransform.anchorMax = new Vector2(1f, 1f);
                val.rectTransform.offsetMin = Vector2.zero;
                val.rectTransform.offsetMax = new Vector2(-12f, 0f);
            }

            private static void Cycle(Transform parent, string key, string value, System.Action<int> nudge)
            {
                var row = PhoneUi.CreateImage(parent, "Cyc_" + PhoneUi.Sanitize(key), PhoneUi.Rounded(14), PhoneUi.Surface);
                PhoneUi.Size(row.gameObject, 48f);
                PhoneUi.AddHorizontal(row.gameObject, 6f);
                row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(8, 8, 6, 6);
                PhoneUi.CreateIconChip(row, "<", PhoneIcons.Material("chevron_left"), () => nudge(-1), false, new Vector2(36f, 32f));
                var mid = PhoneUi.CreateLabel(row, "Mid", key + ": " + value, 14f, FontStyles.Normal, TextAlignmentOptions.Center);
                var midLe = mid.gameObject.AddComponent<LayoutElement>();
                midLe.flexibleWidth = 1f;
                PhoneUi.CreateIconChip(row, ">", PhoneIcons.Material("chevron_right"), () => nudge(1), false, new Vector2(36f, 32f));
            }

            private void IconToggle(Transform parent, string icon, string tip, bool on, UnityEngine.Events.UnityAction click)
            {
                var row = new GameObject("Tog_" + PhoneUi.Sanitize(tip), typeof(RectTransform));
                row.transform.SetParent(parent, false);
                PhoneUi.Size(row, 44f);
                var layout = PhoneUi.AddHorizontal(row, 8f);
                layout.childForceExpandWidth = false;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.padding = new RectOffset(4, 4, 4, 4);
                var art = PhoneUi.CreateIconChip(row.transform, tip, PhoneIcons.Material(icon), click, false, new Vector2(36f, 32f));
                PhoneUi.SetTooltip(art.gameObject, tip);
                var spacer = new GameObject("Sp", typeof(RectTransform));
                spacer.transform.SetParent(row.transform, false);
                spacer.AddComponent<LayoutElement>().flexibleWidth = 1f;
                var tog = PhoneUi.CreateToggleChip(row.transform, on, click);
                PhoneUi.SetTooltip(tog.gameObject, tip);
            }

            private void IconAction(Transform parent, string icon, string tip, UnityEngine.Events.UnityAction iconClick, string actionIcon, string actionTip, UnityEngine.Events.UnityAction click)
            {
                var row = new GameObject("Act_" + PhoneUi.Sanitize(tip), typeof(RectTransform));
                row.transform.SetParent(parent, false);
                PhoneUi.Size(row, 44f);
                var layout = PhoneUi.AddHorizontal(row, 8f);
                layout.childForceExpandWidth = false;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.padding = new RectOffset(4, 4, 4, 4);
                var art = PhoneUi.CreateIconChip(row.transform, tip, PhoneIcons.Material(icon), iconClick, AlertHud.Placing, new Vector2(36f, 32f));
                PhoneUi.SetTooltip(art.gameObject, tip);
                var spacer = new GameObject("Sp", typeof(RectTransform));
                spacer.transform.SetParent(row.transform, false);
                spacer.AddComponent<LayoutElement>().flexibleWidth = 1f;
                var act = PhoneUi.CreateIconChip(row.transform, actionTip, PhoneIcons.Material(actionIcon), click, false, new Vector2(36f, 32f));
                PhoneUi.SetTooltip(act.gameObject, actionTip);
            }

            private static Transform MakeGrid(Transform parent, float w, float h)
            {
                var go = new GameObject("Grid", typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var le = go.AddComponent<LayoutElement>();
                le.minHeight = h;
                le.flexibleWidth = 1f;
                var grid = go.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(w, h);
                grid.spacing = new Vector2(8f, 8f);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = PhoneUi.Landscape ? 6 : 4;
                grid.childAlignment = TextAnchor.UpperCenter;
                var fit = go.AddComponent<ContentSizeFitter>();
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                return go.transform;
            }

            private static string KeyIcon(string id)
            {
                if (id == PhoneKeys.Open) return "mobile";
                if (id == PhoneKeys.Landscape) return "screen_rotation";
                if (id == PhoneKeys.CastHold) return "cast";
                if (id == PhoneKeys.Answer) return "call";
                if (id == PhoneKeys.Decline) return "call_end";
                if (id == PhoneKeys.CamCursor) return "directions_walk";
                if (id == PhoneKeys.CamFlip) return "cached";
                if (id == PhoneKeys.CamRotate) return "screen_rotation";
                if (id == PhoneKeys.CamShutter) return "shutter";
                if (id == PhoneKeys.CamZoomIn) return "zoom_in";
                if (id == PhoneKeys.CamZoomOut) return "zoom_out";
                if (id == PhoneKeys.MusicPlay) return "play";
                if (id == PhoneKeys.MusicNext) return "skip_next";
                if (id == PhoneKeys.MusicPrev) return "skip_previous";
                if (id == PhoneKeys.GameUp) return "keyboard_arrow_up";
                if (id == PhoneKeys.GameDown) return "keyboard_arrow_down";
                if (id == PhoneKeys.GameLeft) return "keyboard_arrow_left";
                if (id == PhoneKeys.GameRight) return "keyboard_arrow_right";
                if (id == PhoneKeys.TetrisRotate) return "rotate_right";
                if (id == PhoneKeys.TetrisDrop) return "keyboard_double_arrow_down";
                if (id == PhoneKeys.BreakoutServe) return "arrow_upward";
                return "keyboard";
            }

            private void Toggle(Transform parent, string key, bool on, System.Action click)
            {
                var row = PhoneUi.CreateImage(parent, "Tog_" + PhoneUi.Sanitize(key), PhoneUi.Rounded(14), PhoneUi.Surface);
                PhoneUi.Size(row.gameObject, 44f);
                var clockRow = PhoneUi.AddHorizontal(row.gameObject, 6f);
                clockRow.padding = new RectOffset(8, 8, 6, 6);
                clockRow.childForceExpandWidth = false;
                var mid = PhoneUi.CreateLabel(row, "Mid", key, 14f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                var midLe = mid.gameObject.AddComponent<LayoutElement>();
                midLe.flexibleWidth = 1f;
                var tog = PhoneUi.CreateToggleChip(row, on, () =>
                {
                    click();
                    ShowClock();
                });
                PhoneUi.SetTooltip(tog.gameObject, key);
            }

            private void Clear()
            {
                for (int i = _host.Content.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(_host.Content.GetChild(i).gameObject);
            }
        }
    }
}
