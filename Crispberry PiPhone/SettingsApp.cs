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
                OnClose = () => { _live = null; },
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
            private string _toneKind = "notify";
            private string _toneAppId;
            private string _toneContactId;
            private bool _toneContactRing = true;

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
                if (_page == "sound")
                {
                    ShowCustomize();
                    return true;
                }
                if (_page == "customize" || _page == "size" || _page == "notifications" || _page == "dock" || _page == "controls" || _page == "logs")
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
                    case "dock": ShowDock(); break;
                    case "theme":
                    case "look": ShowLook(); break;
                    case "case": ShowCase(); break;
                    case "clock": ShowClock(); break;
                    case "buttons": ShowButtons(); break;
                    case "language": ShowLanguage(); break;
                    case "toolbar": ShowToolbar(); break;
                    case "brightness": ShowBrightness(); break;
                    case "sound": ShowSound(); break;
                    case "size": ShowSize(); break;
                    case "controls": ShowControls(); break;
                    case "logs": ShowLogs(); break;
                    case "tones": ShowNotifications(); break;
                    default: ShowHome(); break;
                }
            }

            public void ShowHome()
            {
                _page = "home";
                Clear();
                _host.SetTitle("Settings");

                RectTransform col;
                RectTransform extra;
                PhoneUi.SplitIfLandscape(_host.Content, out col, out extra);

                Row(col, PiPhoneApi.OsName, Plugin.PluginVersion);
                Row(col, "You are", _host.IsMasterClient ? "Expedition leader" : "Scout");

                PhoneUi.CreateButton(col, "Controls", ShowControls, new Vector2(280f, 48f));
                PhoneUi.CreateButton(col, "Customize", ShowCustomize, new Vector2(280f, 48f));
                PhoneUi.CreateButton(col, "Home screen", ShowDock, new Vector2(280f, 44f));
                PhoneUi.CreateButton(col, "Notifications", ShowNotifications, new Vector2(280f, 44f));
                PhoneUi.CreateButton(col, "Logs", ShowLogs, new Vector2(280f, 44f));
                PhoneUi.CreateButton(col, "Phone size", ShowSize, new Vector2(280f, 44f));
                PhoneUi.CreateButton(col, "Close phone", _host.ClosePhone, new Vector2(220f, 40f));

                var keys = PhoneUi.CreateLabel(extra, "OpenKey", "Open phone  " + Plugin.FormatHotkey(), 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                keys.color = PhoneUi.TextDim;
                PhoneUi.Wrap(keys);
                PhoneUi.Size(keys.gameObject, 28f);
            }

            public void ShowCustomize()
            {
                _page = "customize";
                Clear();
                _host.SetTitle("Customize");
                PhoneUi.CreateButton(_host.Content, "Back", ShowHome, new Vector2(120f, 36f));
                PhoneUi.CreateButton(_host.Content, PhoneLang.T("look", "Look"), ShowLook, new Vector2(280f, 44f));
                PhoneUi.CreateButton(_host.Content, PhoneLang.T("language", "Language"), ShowLanguage, new Vector2(280f, 44f));
                PhoneUi.CreateButton(_host.Content, "Case", ShowCase, new Vector2(280f, 44f));
                PhoneUi.CreateButton(_host.Content, "Clock", ShowClock, new Vector2(280f, 44f));
                PhoneUi.CreateButton(_host.Content, PhoneLang.T("buttons", "Buttons"), ShowButtons, new Vector2(280f, 44f));
                PhoneUi.CreateButton(_host.Content, "Toolbar", ShowToolbar, new Vector2(280f, 44f));
                PhoneUi.CreateButton(_host.Content, "Brightness", ShowBrightness, new Vector2(280f, 44f));
                PhoneUi.CreateButton(_host.Content, "Sound", ShowSound, new Vector2(280f, 44f));
            }

            private void ShowNotifications()
            {
                _page = "notifications";
                PhoneSounds.StopPreview();
                Clear();
                _host.SetTitle("Notifications");
                PhoneUi.CreateButton(_host.Content, "Back", ShowHome, new Vector2(120f, 36f));
                PhoneUi.CreateButton(_host.Content, PhoneTones.DuckMusic ? "Fade music on alerts  On" : "Fade music on alerts  Off", () =>
                {
                    PhoneTones.SetDuckMusic(!PhoneTones.DuckMusic);
                    ShowNotifications();
                }, new Vector2(280f, 40f));
                ScrollRect scroll = PhoneUi.CreateScrollView(_host.Content, out RectTransform content);
                scroll.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
                PhoneUi.AddVertical(content.gameObject, 8f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(content.gameObject);

                var hint = PhoneUi.CreateLabel(content, "Hint", "These are the defaults until you set a per-person sound. Phone is the ringtone, Messages is the text tone. Drop files in the alerts folder. Each row can go back to Default.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 48f);

                PiPhoneApp[] apps = PiPhoneApi.GetApps();
                for (int i = 0; i < apps.Length; i++)
                {
                    PiPhoneApp app = apps[i];
                    if (app == null || !app.PostsNotices)
                        continue;
                    string id = app.Id;
                    bool on = PhoneTheme.AppNoticesOn(id);
                    var row = new GameObject("N", typeof(RectTransform));
                    row.transform.SetParent(content, false);
                    PhoneUi.Size(row, 40f);
                    PhoneUi.AddHorizontal(row, 6f);
                    var name = PhoneUi.CreateLabel(row.transform, "N", PhoneLang.AppName(app), 14f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                    name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                    Button toggle = PhoneUi.CreateButton(row.transform, on ? "On" : "Off", null, new Vector2(52f, 32f));
                    string appId = id;
                    toggle.onClick.AddListener(() =>
                    {
                        bool next = !PhoneTheme.AppNoticesOn(appId);
                        PhoneTheme.SetAppNotices(appId, next);
                        var tmp = toggle.GetComponentInChildren<TextMeshProUGUI>(true);
                        if (tmp != null)
                            tmp.text = next ? "On" : "Off";
                    });
                    PhoneUi.CreateButton(row.transform, "♪", () => ShowTones("app", id, null, true), new Vector2(40f, 32f));
                }
            }

            private void ShowTones(string kind, string appId, string contactId, bool contactRing)
            {
                _page = "tones";
                _toneKind = kind ?? "notify";
                _toneAppId = appId;
                _toneContactId = contactId;
                _toneContactRing = contactRing;
                Clear();
                _host.SetTitle("Choose sound");
                PhoneUi.CreateButton(_host.Content, "Back", ShowNotifications, new Vector2(120f, 36f));
                PhoneUi.CreateButton(_host.Content, "Default", () =>
                {
                    ApplyTone(string.Empty);
                    ShowNotifications();
                }, new Vector2(220f, 40f));
                System.Collections.Generic.List<SoundItem> tones = PhoneStore.AlertTones();
                if (tones.Count == 0)
                {
                    var empty = PhoneUi.CreateLabel(_host.Content, "Empty", "Add alert sounds in MakeNoti, or drop files in the alerts folder.", 14f, FontStyles.Normal, TextAlignmentOptions.Center);
                    empty.color = PhoneUi.TextDim;
                    PhoneUi.Wrap(empty);
                    PhoneUi.Size(empty.gameObject, 40f);
                    return;
                }
                ScrollRect scroll = PhoneUi.CreateScrollView(_host.Content, out RectTransform content);
                scroll.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
                PhoneUi.AddVertical(content.gameObject, 6f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(content.gameObject);
                PhoneTones.FillPicker(content, () => ShowTones(_toneKind, _toneAppId, _toneContactId, _toneContactRing), captured =>
                {
                    ApplyTone(captured.Id);
                    ShowNotifications();
                });
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
                Clear();
                _host.SetTitle("Home screen");
                PhoneUi.CreateButton(_host.Content, "Back", ShowHome, new Vector2(120f, 36f));
                PhoneUi.CreateButton(_host.Content, PhoneTheme.HideDock ? "Show dock" : "Hide dock", () =>
                {
                    PhoneTheme.SetHideDock(!PhoneTheme.HideDock);
                    ShowDock();
                }, new Vector2(240f, 40f));
                var hint = PhoneUi.CreateLabel(_host.Content, "Hint", "Pick up to four apps for the home dock. You can also hold an app on the home screen.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 40f);
                ScrollRect scroll = PhoneUi.CreateScrollView(_host.Content, out RectTransform content);
                scroll.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
                PhoneUi.AddVertical(content.gameObject, 6f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(content.gameObject);
                PiPhoneApp[] apps = PiPhoneApi.GetInstalledApps();
                for (int i = 0; i < apps.Length; i++)
                {
                    PiPhoneApp app = apps[i];
                    if (app == null)
                        continue;
                    string id = app.Id;
                    bool on = PhoneStore.IsOnDock(id);
                    var row = new GameObject("D", typeof(RectTransform));
                    row.transform.SetParent(content, false);
                    PhoneUi.Size(row, 40f);
                    PhoneUi.AddHorizontal(row, 6f);
                    var name = PhoneUi.CreateLabel(row.transform, "N", PhoneLang.AppName(app), 14f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                    name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                    PhoneUi.CreateButton(row.transform, on ? "Docked" : "Dock", () =>
                    {
                        if (on)
                            PhoneStore.RemoveDock(id);
                        else if (!PhoneStore.AddDock(id))
                            _host.ShowToast("Dock is full (4 apps).");
                        PhoneMenu.OnAppsChanged();
                        ShowDock();
                    }, new Vector2(80f, 32f));
                }
            }

            private void ShowLook()
            {
                _page = "look";
                ScrollPage("Look", content =>
                {
                    var hint = PhoneUi.CreateLabel(content, "Hint", "Each color and size is independent. Other mods can set these through PiPhoneApi (SetLook / SetScreenColor / SetButtonRadius).", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                    hint.color = PhoneUi.TextDim;
                    PhoneUi.Wrap(hint);
                    PhoneUi.Size(hint.gameObject, 48f);
                    Label(content, "Screen");
                    PhoneColorPicker.Create(content, PhoneTheme.ScreenColor, PhoneTheme.SetScreenColor);
                    Label(content, "Cards / surfaces");
                    PhoneColorPicker.Create(content, PhoneTheme.SurfaceColor, PhoneTheme.SetSurfaceColor);
                    Label(content, "Nav bar");
                    PhoneColorPicker.Create(content, PhoneTheme.NavColor, PhoneTheme.SetNavColor);
                    Label(content, "Text");
                    PhoneColorPicker.Create(content, PhoneTheme.TextColor, PhoneTheme.SetTextColor);
                    Label(content, "Accent");
                    PhoneColorPicker.Create(content, PhoneTheme.AccentColor, PhoneTheme.SetAccentColor);
                    Label(content, "App icon glyphs");
                    PhoneColorPicker.Create(content, PhoneTheme.IconColor, PhoneTheme.SetIconColor);
                    PhoneUi.CreateSliderRow(content, "Font", 0.7f, 1.6f, PhoneTheme.FontScale, v => PhoneTheme.SetFontScale(v), v => Mathf.RoundToInt(v * 100f) + "%");
                    PhoneUi.CreateButton(content, PhoneLang.T("reset_look", "Reset look to defaults"), () =>
                    {
                        PhoneTheme.ResetLook();
                        ShowLook();
                    }, new Vector2(280f, 40f));
                });
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

            private static void Label(Transform parent, string text)
            {
                var lab = PhoneUi.CreateLabel(parent, "L", text, 15f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Size(lab.gameObject, 24f);
            }

            private void ShowCase()
            {
                _page = "case";
                ScrollPage("Case", content =>
                {
                    PhoneColorPicker.Create(content, PhoneTheme.CaseColor, PhoneTheme.SetCaseColor);
                    PhoneUi.CreateButton(content, PhoneLang.T("reset", "Reset"), () =>
                    {
                        PhoneTheme.ResetCase();
                        ShowCase();
                    }, new Vector2(220f, 40f));
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
                    var hint = PhoneUi.CreateLabel(content, "Hint", "PEAK time follows the in-game day/night cycle and never shows a date. Clock color is independent of button text.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                    hint.color = PhoneUi.TextDim;
                    PhoneUi.Wrap(hint);
                    PhoneUi.Size(hint.gameObject, 56f);
                    var colorLbl = PhoneUi.CreateLabel(content, "ClockColor", "Clock color", 15f, FontStyles.Normal, TextAlignmentOptions.Center);
                    PhoneUi.Size(colorLbl.gameObject, 24f);
                    PhoneColorPicker.Create(content, PhoneTheme.ClockColor, c =>
                    {
                        PhoneTheme.SetClockColor(c);
                    });
                    PhoneUi.CreateButton(content, PhoneLang.T("reset", "Reset"), () =>
                    {
                        PhoneTheme.ResetClockColor();
                        ShowClock();
                    }, new Vector2(220f, 40f));
                });
            }

            private void ShowButtons()
            {
                _page = "buttons";
                ScrollPage("Buttons", content =>
                {
                    var fill = PhoneUi.CreateLabel(content, "Fill", "Button color", 15f, FontStyles.Normal, TextAlignmentOptions.Center);
                    PhoneUi.Size(fill.gameObject, 24f);
                    PhoneColorPicker.Create(content, PhoneTheme.ButtonFillColor, PhoneTheme.SetButtonFill);
                    var font = PhoneUi.CreateLabel(content, "Font", "Button font color", 15f, FontStyles.Normal, TextAlignmentOptions.Center);
                    PhoneUi.Size(font.gameObject, 24f);
                    PhoneColorPicker.Create(content, PhoneTheme.ButtonFontColor, PhoneTheme.SetButtonFont);
                    PhoneUi.CreateSliderRow(content, "Button corners", 0f, 28f, PhoneTheme.ButtonRadius, v => PhoneTheme.SetButtonRadius(Mathf.RoundToInt(v)), v => Mathf.RoundToInt(v) == 0 ? "Square" : Mathf.RoundToInt(v) + "px");
                    PhoneUi.CreateSliderRow(content, "App icon corners", 0f, 28f, PhoneTheme.IconRadius, v => PhoneTheme.SetIconRadius(Mathf.RoundToInt(v)), v => Mathf.RoundToInt(v) == 0 ? "Square" : Mathf.RoundToInt(v) + "px");
                    var shapeHint = PhoneUi.CreateLabel(content, "SH", "0 is a square. Button corners and app-icon wells are separate.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                    shapeHint.color = PhoneUi.TextDim;
                    PhoneUi.Wrap(shapeHint);
                    PhoneUi.Size(shapeHint.gameObject, 36f);
                    PhoneUi.CreateButton(content, PhoneLang.T("reset_buttons", "Reset buttons to defaults"), () =>
                    {
                        PhoneTheme.ResetButtons();
                        ShowButtons();
                    }, new Vector2(280f, 40f));
                });
            }

            private void ShowToolbar()
            {
                _page = "toolbar";
                Clear();
                _host.SetTitle("Toolbar");
                PhoneUi.CreateButton(_host.Content, "Back", ShowCustomize, new Vector2(120f, 36f));
                ScrollRect scroll = PhoneUi.CreateScrollView(_host.Content, out RectTransform content);
                scroll.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
                PhoneUi.AddVertical(content.gameObject, 8f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(content.gameObject);
                var hint = PhoneUi.CreateLabel(content, "Hint", "These chips sit in Quick settings (pull the status bar). Turn one off to hide it. Other mods can add chips with PiPhoneApi.RegisterShadeButton.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 52f);
                PiPhoneShadeButton[] buttons = PiPhoneApi.GetShadeButtons();
                for (int i = 0; i < buttons.Length; i++)
                {
                    PiPhoneShadeButton button = buttons[i];
                    if (button == null)
                        continue;
                    string id = button.Id;
                    bool on = PhoneTheme.ShadeButtonOn(id, button.DefaultVisible);
                    var row = new GameObject("T", typeof(RectTransform));
                    row.transform.SetParent(content, false);
                    PhoneUi.Size(row, 40f);
                    PhoneUi.AddHorizontal(row, 6f);
                    string name = string.IsNullOrEmpty(button.Label) ? id : button.Label;
                    var lab = PhoneUi.CreateLabel(row.transform, "N", name, 14f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                    lab.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                    PhoneUi.CreateButton(row.transform, on ? "On" : "Off", () =>
                    {
                        PiPhoneApi.SetShadeButtonVisible(id, !on);
                        ShowToolbar();
                    }, new Vector2(64f, 32f));
                }
            }

            private void ShowBrightness()
            {
                _page = "brightness";
                ScrollPage("Brightness", content =>
                {
                    var hint = PhoneUi.CreateLabel(content, "Hint", "A black overlay sits on top of the phone screen. Slide right for a brighter screen.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                    hint.color = PhoneUi.TextDim;
                    PhoneUi.Wrap(hint);
                    PhoneUi.Size(hint.gameObject, 48f);
                    PhoneUi.CreateSliderRow(content, "Level", PhoneTheme.MinBrightness, 1f, PhoneTheme.Brightness, v => PhoneTheme.SetBrightness(v));
                });
            }

            private void ShowSound()
            {
                _page = "sound";
                ScrollPage("Sound", content =>
                {
                    PhoneUi.CreateSliderRow(content, "Ringer", 0f, 1f, PhoneTheme.RingVolume, v => PhoneTheme.SetRingVolume(v));
                    PhoneUi.CreateSliderRow(content, "Music", 0f, 1f, PhoneTheme.MusicVolume, v => PhoneTheme.SetMusicVolume(v));
                });
            }

            private void ShowSize()
            {
                _page = "size";
                Clear();
                _host.SetTitle("Phone size");
                PhoneUi.CreateButton(_host.Content, "Back", ShowHome, new Vector2(120f, 36f));
                ScrollRect scroll = PhoneUi.CreateScrollView(_host.Content, out RectTransform content);
                var le = scroll.gameObject.AddComponent<LayoutElement>();
                le.flexibleHeight = 1f;
                PhoneUi.AddVertical(content.gameObject, 8f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(content.gameObject);
                PhoneUi.CreateSliderRow(content, "Vertical", 0.55f, 1.35f, PhoneTheme.PhoneScale, v => PhoneTheme.SetPhoneScale(v));
                PhoneUi.CreateSliderRow(content, "Landscape", 0.55f, 1.8f, PhoneTheme.PhoneScaleLand, v => PhoneTheme.SetPhoneScaleLand(v));
                var hint = PhoneUi.CreateLabel(content, "Hint", "Landscape keeps the same phone shape, turned on its side. Drag the case (the rim) to move it. Each orientation has its own size and position.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 56f);
                PhoneUi.CreateButton(content, "Reset vertical position", () =>
                {
                    PhoneTheme.SetPhonePos(0f, 0f);
                    PhoneMenu.ApplyPlacement();
                }, new Vector2(260f, 40f));
                PhoneUi.CreateButton(content, "Reset landscape position", () =>
                {
                    PhoneTheme.SetPhonePosLand(0f, 0f);
                    PhoneMenu.ApplyPlacement();
                }, new Vector2(260f, 40f));
            }

            private void ShowLogs()
            {
                _page = "logs";
                Clear();
                _host.SetTitle("Logs");
                PhoneUi.CreateButton(_host.Content, "Back", ShowHome, new Vector2(120f, 36f));
                PhoneUi.CreateButton(_host.Content, PhoneTheme.WriteLogs ? "Write logs  On" : "Write logs  Off", () =>
                {
                    PhoneTheme.SetWriteLogs(!PhoneTheme.WriteLogs);
                    ShowLogs();
                }, new Vector2(280f, 44f));
                var hint = PhoneUi.CreateLabel(_host.Content, "Hint", "Info lines go to BepInEx LogOutput.log and Player.log. Errors always write. Leave this on while testing; we will default it off for release.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 64f);
            }

            private void ShowControls()
            {
                _page = "controls";
                Clear();
                _host.SetTitle("Controls");
                PhoneUi.CreateButton(_host.Content, "Back", ShowHome, new Vector2(120f, 36f));
                ScrollRect scroll = PhoneUi.CreateScrollView(_host.Content, out RectTransform content);
                var le = scroll.gameObject.AddComponent<LayoutElement>();
                le.flexibleHeight = 1f;
                PhoneUi.AddVertical(content.gameObject, 6f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(content.gameObject);

                PhoneUi.CreateButton(content, PhoneTheme.AutoAnswer ? "Auto-answer calls  On" : "Auto-answer calls  Off", () =>
                {
                    PhoneTheme.SetAutoAnswer(!PhoneTheme.AutoAnswer);
                    ShowControls();
                }, new Vector2(280f, 40f));
                PhoneUi.CreateButton(content, Plugin.GetShowPauseMenuButton() ? "Pause menu button  On" : "Pause menu button  Off", () =>
                {
                    Plugin.SetShowPauseMenuButton(!Plugin.GetShowPauseMenuButton());
                    ShowControls();
                }, new Vector2(280f, 40f));

                var hint = PhoneUi.CreateLabel(content, "Hint", Plugin.CapturingHotkey
                    ? "Press a key. Hold Ctrl, Alt, or Shift for Open phone. Esc cancels."
                    : "Tap a bind to recapture it. None unbinds any control except Open phone. Music starts empty. Arrow keys still work in games.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 52f);

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
                    PhoneUi.AddHorizontal(row, 6f);
                    string label = bind.Label + "  " + (Plugin.CapturingHotkey ? "..." : PhoneKeys.Format(id));
                    PhoneUi.CreateButton(row.transform, label, () =>
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
                    }, new Vector2(220f, 36f)).GetComponent<LayoutElement>().flexibleWidth = 1f;
                    if (!bind.Required)
                    {
                        PhoneUi.CreateButton(row.transform, "None", () =>
                        {
                            PhoneKeys.Clear(id);
                            ShowControls();
                        }, new Vector2(56f, 36f));
                    }
                }
            }

            private void ScrollPage(string title, System.Action<RectTransform> fill)
            {
                Clear();
                _host.SetTitle(title);
                PhoneUi.CreateButton(_host.Content, "Back", ShowCustomize, new Vector2(120f, 36f));
                ScrollRect scroll = PhoneUi.CreateScrollView(_host.Content, out RectTransform content);
                var le = scroll.gameObject.AddComponent<LayoutElement>();
                le.flexibleHeight = 1f;
                PhoneUi.AddVertical(content.gameObject, 8f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(content.gameObject);
                fill(content);
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
                PhoneUi.CreateButton(row, "<", () => nudge(-1), new Vector2(36f, 32f));
                var mid = PhoneUi.CreateLabel(row, "Mid", key + ": " + value, 14f, FontStyles.Normal, TextAlignmentOptions.Center);
                var midLe = mid.gameObject.AddComponent<LayoutElement>();
                midLe.flexibleWidth = 1f;
                PhoneUi.CreateButton(row, ">", () => nudge(1), new Vector2(36f, 32f));
            }

            private void Toggle(Transform parent, string key, bool on, System.Action click)
            {
                var row = PhoneUi.CreateImage(parent, "Tog_" + PhoneUi.Sanitize(key), PhoneUi.Rounded(14), PhoneUi.Surface);
                PhoneUi.Size(row.gameObject, 44f);
                PhoneUi.AddHorizontal(row.gameObject, 6f);
                row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(8, 8, 6, 6);
                var mid = PhoneUi.CreateLabel(row, "Mid", key, 14f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                var midLe = mid.gameObject.AddComponent<LayoutElement>();
                midLe.flexibleWidth = 1f;
                PhoneUi.CreateButton(row, on ? "On" : "Off", () =>
                {
                    click();
                    ShowClock();
                }, new Vector2(64f, 32f));
            }

            private void Clear()
            {
                for (int i = _host.Content.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(_host.Content.GetChild(i).gameObject);
            }
        }
    }
}
