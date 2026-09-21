using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PhotonPlayer = Photon.Realtime.Player;

namespace Crispberry_PiPhone
{
    internal static class DialerApp
    {
        private static Session _live;

        internal static void Register()
        {
            PiPhoneApi.RegisterApp(new PiPhoneApp
            {
                Id = BuiltinApps.PhoneId,
                DisplayName = "Phone",
                IconGlyph = "P",
                IconBackground = PhoneUi.PhoneIcon,
                SortOrder = 0,
                ShowOnDock = true,
                OnOpen = host => { _live = new Session(host); _live.Build(); },
                OnClose = () => { _live = null; },
                OnOrientation = () => { if (_live != null) _live.Relayout(); }
            });
        }

        internal static bool TryGoBack()
        {
            return _live != null && _live.GoBack();
        }

        private sealed class Session
        {
            private readonly IPiPhoneHost _host;
            private string _digits = string.Empty;
            private GameObject _keypadPage;
            private GameObject _contactsPage;
            private GameObject _recentsPage;
            private GameObject _tonesPage;
            private GameObject _profilePage;
            private TextMeshProUGUI _numberLabel;
            private Button _keypadTab;
            private Button _contactsTab;
            private Button _recentsTab;
            private readonly HashSet<int> _selected = new HashSet<int>();
            private string _toneContactId;
            private string _toneContactName;
            private bool _toneRing = true;

            public Session(IPiPhoneHost host)
            {
                _host = host;
            }

            public bool GoBack()
            {
                if (_tonesPage != null && _tonesPage.activeSelf)
                {
                    if (!string.IsNullOrEmpty(_toneContactId))
                        ShowContactProfile(_toneContactId);
                    else
                        ShowContacts();
                    return true;
                }
                if (_profilePage != null && _profilePage.activeSelf)
                {
                    ShowContacts();
                    return true;
                }
                return false;
            }

            public void Relayout()
            {
                bool contacts = _contactsPage != null && _contactsPage.activeSelf;
                bool recents = _recentsPage != null && _recentsPage.activeSelf;
                bool tones = _tonesPage != null && _tonesPage.activeSelf;
                bool profile = _profilePage != null && _profilePage.activeSelf;
                string digits = _digits;
                string toneId = _toneContactId;
                string toneName = _toneContactName;
                for (int i = _host.Content.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(_host.Content.GetChild(i).gameObject);
                Build();
                _digits = digits;
                RefreshNumber();
                if (tones && !string.IsNullOrEmpty(toneId))
                    ShowContactTones(toneId, toneName);
                else if (profile && !string.IsNullOrEmpty(toneId))
                    ShowContactProfile(toneId);
                else if (contacts)
                    ShowContacts();
                else if (recents)
                    ShowRecents();
            }

            public void Build()
            {
                _host.SetTitle("Phone");
                var tabs = PhoneUi.CreateImage(_host.Content, "Tabs", PhoneUi.Rounded(16), PhoneUi.Surface);
                PhoneUi.Size(tabs.gameObject, 40f);
                PhoneUi.AddHorizontal(tabs.gameObject, 6f);
                tabs.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(6, 6, 4, 4);
                _keypadTab = Tab(tabs, "Keypad", ShowKeypad);
                _contactsTab = Tab(tabs, "Contacts", ShowContacts);
                _recentsTab = Tab(tabs, "Recents", ShowRecents);

                _keypadPage = Page("KeypadPage");
                BuildKeypad();
                _contactsPage = Page("ContactsPage");
                BuildContacts();
                _recentsPage = Page("RecentsPage");
                BuildRecents();
                _tonesPage = Page("TonesPage");
                _profilePage = Page("ProfilePage");
                ShowKeypad();
            }

            private GameObject Page(string name)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(_host.Content, false);
                var le = go.AddComponent<LayoutElement>();
                le.flexibleHeight = 1f;
                le.minHeight = PhoneUi.Landscape ? 160f : 280f;
                PhoneUi.AddVertical(go, 6f, new RectOffset(4, 4, 4, 4));
                go.SetActive(false);
                return go;
            }

            private Button Tab(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
            {
                Button btn = PhoneUi.CreateButton(parent, label, onClick, new Vector2(100f, 32f));
                var le = btn.GetComponent<LayoutElement>();
                if (le != null)
                    le.flexibleWidth = 1f;
                return btn;
            }

            private void BuildKeypad()
            {
                var displayRow = PhoneUi.CreateImage(_keypadPage.transform, "Display", PhoneUi.White(), new Color(1f, 1f, 1f, 0f));
                PhoneUi.Size(displayRow.gameObject, 56f);
                _numberLabel = PhoneUi.CreateLabel(displayRow, "Number", string.Empty, 28f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Stretch(_numberLabel.rectTransform, 36f, 4f);
                var back = PhoneUi.CreateButton(displayRow.transform, "<", Backspace, new Vector2(36f, 36f));
                var backRt = back.GetComponent<RectTransform>();
                backRt.anchorMin = backRt.anchorMax = new Vector2(1f, 0.5f);
                backRt.pivot = new Vector2(1f, 0.5f);
                backRt.anchoredPosition = new Vector2(-4f, 0f);
                var backLe = back.GetComponent<LayoutElement>();
                if (backLe != null)
                    Object.Destroy(backLe);

                var gridGo = new GameObject("Keys", typeof(RectTransform));
                gridGo.transform.SetParent(_keypadPage.transform, false);
                var gridLe = gridGo.AddComponent<LayoutElement>();
                gridLe.flexibleHeight = 1f;
                bool land = _host.IsLandscape;
                gridLe.minHeight = land ? 160f : 220f;
                var grid = gridGo.AddComponent<GridLayoutGroup>();
                grid.cellSize = land ? new Vector2(88f, 36f) : new Vector2(92f, 52f);
                grid.spacing = land ? new Vector2(12f, 6f) : new Vector2(10f, 8f);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 3;
                grid.childAlignment = TextAnchor.MiddleCenter;
                grid.padding = land ? new RectOffset(48, 48, 2, 2) : new RectOffset(24, 24, 4, 4);

                string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "*", "0", "#" };
                string[] letters = { "", "ABC", "DEF", "GHI", "JKL", "MNO", "PQRS", "TUV", "WXYZ", "", "+", "" };
                for (int i = 0; i < keys.Length; i++)
                    AddKey(gridGo.transform, keys[i], letters[i]);

                var callRow = new GameObject("CallRow", typeof(RectTransform));
                callRow.transform.SetParent(_keypadPage.transform, false);
                PhoneUi.Size(callRow, land ? 52f : 72f);
                PhoneUi.AddHorizontal(callRow, 0f);
                var callLayout = callRow.GetComponent<HorizontalLayoutGroup>();
                callLayout.childForceExpandWidth = false;
                callLayout.childControlWidth = false;
                PhoneUi.CreateCircleButton(callRow.transform, "Call", PlaceCall, land ? 48f : 64f, PhoneUi.CallGreen);
            }

            private void AddKey(Transform parent, string digit, string letters)
            {
                var rt = PhoneUi.CreateImage(parent, "Key_" + PhoneUi.Sanitize(digit), PhoneUi.Circle(), PhoneUi.SurfaceAlt);
                rt.GetComponent<Image>().type = Image.Type.Simple;
                var button = rt.gameObject.AddComponent<Button>();
                button.targetGraphic = rt.GetComponent<Image>();
                button.colors = PhoneUi.TintColors();
                string captured = digit;
                button.onClick.AddListener(() => Press(captured));
                var digitLabel = PhoneUi.CreateLabel(rt, "Digit", digit, 22f, FontStyles.Normal, TextAlignmentOptions.Center);
                digitLabel.rectTransform.anchorMin = new Vector2(0f, 0.28f);
                digitLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
                digitLabel.rectTransform.offsetMin = Vector2.zero;
                digitLabel.rectTransform.offsetMax = Vector2.zero;
                var sub = PhoneUi.CreateLabel(rt, "Letters", letters, 10f, FontStyles.Normal, TextAlignmentOptions.Center);
                sub.color = PhoneUi.TextDim;
                sub.rectTransform.anchorMin = new Vector2(0f, 0.04f);
                sub.rectTransform.anchorMax = new Vector2(1f, 0.36f);
                sub.rectTransform.offsetMin = Vector2.zero;
                sub.rectTransform.offsetMax = Vector2.zero;
            }

            private void BuildContacts()
            {
                var hint = PhoneUi.CreateLabel(_contactsPage.transform, "Hint", "Call or video a scout. Select several names, then Start group call.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 36f);

                ScrollRect scroll = PhoneUi.CreateScrollView(_contactsPage.transform, out RectTransform content);
                var scrollLe = scroll.gameObject.AddComponent<LayoutElement>();
                scrollLe.flexibleHeight = 1f;
                PhoneUi.AddVertical(content.gameObject, 6f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(content.gameObject);

                PhotonPlayer[] players = BuiltinApps.OtherPlayers();
                if (players.Length == 0)
                {
                    var empty = PhoneUi.CreateLabel(content, "Empty", "No scouts nearby.", 15f, FontStyles.Normal, TextAlignmentOptions.Center);
                    empty.color = PhoneUi.TextDim;
                    PhoneUi.Size(empty.gameObject, 36f);
                }
                else
                {
                    for (int i = 0; i < players.Length; i++)
                    {
                        PhotonPlayer player = players[i];
                        int actor = player.ActorNumber;
                        string id = BuiltinApps.PlayerId(player);
                        string real = BuiltinApps.PlayerName(player);
                        PhoneContacts.See(id, real);
                        string name = PhoneContacts.Display(id, real);
                        var row = new GameObject("C", typeof(RectTransform));
                        row.transform.SetParent(content, false);
                        PhoneUi.Size(row, 56f);
                        PhoneUi.AddHorizontal(row, 6f);
                        PhoneContacts.CreateAvatar(row.transform, id, 40f);
                        var textCol = new GameObject("T", typeof(RectTransform));
                        textCol.transform.SetParent(row.transform, false);
                        var textLe = textCol.AddComponent<LayoutElement>();
                        textLe.flexibleWidth = 1f;
                        textLe.minWidth = 80f;
                        PhoneUi.AddVertical(textCol, 0f, new RectOffset(0, 0, 4, 4));
                        string idCap = id;
                        Button nameBtn = PhoneUi.CreateButton(textCol.transform, name, () => ShowContactProfile(idCap), new Vector2(140f, 24f));
                        var nameLe = nameBtn.GetComponent<LayoutElement>();
                        if (nameLe != null)
                            nameLe.flexibleWidth = 1f;
                        var realLab = PhoneUi.CreateLabel(textCol.transform, "R", real, 11f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                        realLab.color = PhoneUi.TextDim;
                        PhoneUi.Size(realLab.gameObject, 14f);
                        int actorCaptured = actor;
                        PhoneUi.CreateButton(row.transform, "C", () => CallService.Dial(new[] { actorCaptured }, false), new Vector2(36f, 36f));
                        PhoneUi.CreateButton(row.transform, "V", () => CallService.Dial(new[] { actorCaptured }, false, true), new Vector2(36f, 36f));
                    }
                }

                PhoneUi.CreateButton(_contactsPage.transform, "Start group call", CallSelected, new Vector2(240f, 44f));
            }

            private void ToggleSelect(int actor, Button row)
            {
                if (_selected.Contains(actor))
                    _selected.Remove(actor);
                else
                    _selected.Add(actor);
                var img = row != null ? row.GetComponent<Image>() : null;
                if (img != null)
                    img.color = _selected.Contains(actor) ? PhoneUi.CallGreen : PhoneUi.SurfaceAlt;
            }

            private void BuildRecents()
            {
                ScrollRect scroll = PhoneUi.CreateScrollView(_recentsPage.transform, out RectTransform content);
                var scrollLe = scroll.gameObject.AddComponent<LayoutElement>();
                scrollLe.flexibleHeight = 1f;
                PhoneUi.AddVertical(content.gameObject, 6f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(content.gameObject);

                if (PhoneStore.Calls.Count == 0)
                {
                    var empty = PhoneUi.CreateLabel(content, "Empty", "No calls yet.", 15f, FontStyles.Normal, TextAlignmentOptions.Center);
                    empty.color = PhoneUi.TextDim;
                    PhoneUi.Size(empty.gameObject, 36f);
                    return;
                }

                int shown = 0;
                for (int i = 0; i < PhoneStore.Calls.Count && shown < 40; i++)
                {
                    CallLogItem call = PhoneStore.Calls[i];
                    if (call == null)
                        continue;
                    string label = (call.Outgoing ? "↑ " : "↓ ") + (call.Missed ? "(missed) " : "") + call.OtherName;
                    string otherId = call.OtherId;
                    PhoneUi.CreateButton(content, label, () =>
                    {
                        PhotonPlayer p = BuiltinApps.FindById(otherId);
                        if (p == null)
                        {
                            _host.ShowToast("They're not in the lobby.");
                            return;
                        }
                        CallService.Dial(new[] { p.ActorNumber }, false);
                    }, new Vector2(300f, 40f));
                    shown++;
                }
            }

            private void Press(string digit)
            {
                if (_digits.Length >= 16)
                    return;
                _digits += digit;
                RefreshNumber();
            }

            private void Backspace()
            {
                if (_digits.Length == 0)
                    return;
                _digits = _digits.Substring(0, _digits.Length - 1);
                RefreshNumber();
            }

            private void RefreshNumber()
            {
                if (_numberLabel != null)
                    _numberLabel.text = _digits;
            }

            private void ShowKeypad()
            {
                SetPage(true, false, false, false, false);
                ColorTab(_keypadTab, true);
                ColorTab(_contactsTab, false);
                ColorTab(_recentsTab, false);
            }

            private void ShowContacts()
            {
                SetPage(false, true, false, false, false);
                ColorTab(_keypadTab, false);
                ColorTab(_contactsTab, true);
                ColorTab(_recentsTab, false);
            }

            private void ShowRecents()
            {
                SetPage(false, false, true, false, false);
                ColorTab(_keypadTab, false);
                ColorTab(_contactsTab, false);
                ColorTab(_recentsTab, true);
            }

            private void ShowContactProfile(string id)
            {
                _toneContactId = id;
                _toneContactName = PhoneContacts.Display(id, "Scout");
                SetPage(false, false, false, false, true);
                DrawContactProfile();
            }

            private void DrawContactProfile()
            {
                ClearPage(_profilePage);
                string id = _toneContactId;
                string real = PhoneContacts.Real(id, "Scout");
                string custom = PhoneContacts.Display(id, real);
                _host.SetTitle(custom);
                PhoneUi.CreateButton(_profilePage.transform, "Back", ShowContacts, new Vector2(120f, 32f));
                var head = new GameObject("Head", typeof(RectTransform));
                head.transform.SetParent(_profilePage.transform, false);
                PhoneUi.Size(head, 80f);
                PhoneUi.AddHorizontal(head, 10f);
                var hl = head.GetComponent<HorizontalLayoutGroup>();
                hl.childAlignment = TextAnchor.MiddleCenter;
                hl.childForceExpandWidth = false;
                PhoneContacts.CreateAvatar(head.transform, id, 64f);
                var names = new GameObject("N", typeof(RectTransform));
                names.transform.SetParent(head.transform, false);
                names.AddComponent<LayoutElement>().flexibleWidth = 1f;
                PhoneUi.AddVertical(names, 2f, new RectOffset(0, 0, 8, 8));
                var shown = PhoneUi.CreateLabel(names.transform, "D", custom, 16f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                PhoneUi.Size(shown.gameObject, 22f);
                var realLab = PhoneUi.CreateLabel(names.transform, "R", "Steam name: " + real, 12f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                realLab.color = PhoneUi.TextDim;
                PhoneUi.Size(realLab.gameObject, 18f);

                TMP_InputField nameIn = PhoneUi.CreateInput(_profilePage.transform, "Custom name");
                nameIn.text = custom == real ? string.Empty : custom;
                PhoneUi.Size(nameIn.gameObject, 40f);
                PhoneUi.CreateButton(_profilePage.transform, "Save name", () =>
                {
                    PhoneContacts.SetCustomName(id, nameIn.text);
                    _toneContactName = PhoneContacts.Display(id, real);
                    DrawContactProfile();
                    _host.ShowToast("Contact saved.");
                }, new Vector2(200f, 36f));
                PhoneUi.CreateButton(_profilePage.transform, "Photo from Gallery", DrawPhotoPicker, new Vector2(220f, 36f));

                PhotonPlayer live = BuiltinApps.FindById(id);
                int actor = live != null ? live.ActorNumber : 0;
                var actions = new GameObject("A", typeof(RectTransform));
                actions.transform.SetParent(_profilePage.transform, false);
                PhoneUi.Size(actions, 44f);
                PhoneUi.AddHorizontal(actions, 8f);
                if (actor > 0)
                {
                    PhoneUi.CreateButton(actions.transform, "Call", () => CallService.Dial(new[] { actor }, false), new Vector2(90f, 36f));
                    PhoneUi.CreateButton(actions.transform, "Video", () => CallService.Dial(new[] { actor }, false, true), new Vector2(90f, 36f));
                }
                PhoneUi.CreateButton(actions.transform, "Tones", () => ShowContactTones(id, PhoneContacts.Display(id, real)), new Vector2(90f, 36f));
            }

            private void DrawPhotoPicker()
            {
                ClearPage(_profilePage);
                _host.SetTitle("Contact photo");
                PhoneUi.CreateButton(_profilePage.transform, "Back", DrawContactProfile, new Vector2(120f, 32f));
                ScrollRect scroll = PhoneUi.CreateScrollView(_profilePage.transform, out RectTransform content);
                scroll.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
                PhoneUi.AddVertical(content.gameObject, 6f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(content.gameObject);
                int shown = 0;
                for (int i = 0; i < PhoneStore.Photos.Count; i++)
                {
                    PhotoItem photo = PhoneStore.Photos[i];
                    if (photo == null || photo.Video)
                        continue;
                    string path = PhoneStore.PhotoPath(photo.File);
                    if (string.IsNullOrEmpty(path) || !File.Exists(path))
                        continue;
                    string pathCap = path;
                    string idCap = _toneContactId;
                    PhoneUi.CreateButton(content, "Photo " + (shown + 1), () =>
                    {
                        try
                        {
                            if (PhoneContacts.SetPhoto(idCap, File.ReadAllBytes(pathCap)))
                                _host.ShowToast("Photo saved.");
                            else
                                _host.ShowToast("Could not save that photo.");
                        }
                        catch
                        {
                            _host.ShowToast("Could not save that photo.");
                        }
                        DrawContactProfile();
                    }, new Vector2(240f, 40f));
                    shown++;
                    if (shown >= 40)
                        break;
                }
                if (shown == 0)
                {
                    var empty = PhoneUi.CreateLabel(content, "E", "Take a photo in Camera first.", 14f, FontStyles.Normal, TextAlignmentOptions.Center);
                    empty.color = PhoneUi.TextDim;
                    PhoneUi.Size(empty.gameObject, 36f);
                }
            }

            private void ShowContactTones(string id, string name)
            {
                _toneContactId = id;
                _toneContactName = name;
                SetPage(false, false, false, true, false);
                DrawContactTones();
            }

            private void DrawContactTones()
            {
                ClearPage(_tonesPage);
                _host.SetTitle(_toneContactName ?? "Sounds");
                PhoneUi.CreateButton(_tonesPage.transform, "Back", () => ShowContactProfile(_toneContactId), new Vector2(120f, 32f));
                var hint = PhoneUi.CreateLabel(_tonesPage.transform, "H", "Empty = Settings default.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Size(hint.gameObject, 22f);
                PhoneUi.CreateButton(_tonesPage.transform, "Ringtone  " + PhoneTones.Label(PhoneTones.ContactRing(_toneContactId)), () => FillDialerTones(true), new Vector2(280f, 40f));
                PhoneUi.CreateButton(_tonesPage.transform, "Text tone  " + PhoneTones.Label(PhoneTones.ContactText(_toneContactId)), () => FillDialerTones(false), new Vector2(280f, 40f));
            }

            private void FillDialerTones(bool ring)
            {
                _toneRing = ring;
                ClearPage(_tonesPage);
                PhoneUi.CreateButton(_tonesPage.transform, "Back", DrawContactTones, new Vector2(120f, 32f));
                PhoneUi.CreateButton(_tonesPage.transform, "Default", () =>
                {
                    if (ring)
                        PhoneTones.SetContactRing(_toneContactId, string.Empty);
                    else
                        PhoneTones.SetContactText(_toneContactId, string.Empty);
                    DrawContactTones();
                }, new Vector2(220f, 40f));
                ScrollRect scroll = PhoneUi.CreateScrollView(_tonesPage.transform, out RectTransform content);
                scroll.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
                PhoneUi.AddVertical(content.gameObject, 6f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(content.gameObject);
                PhoneTones.FillPicker(content, () => FillDialerTones(ring), captured =>
                {
                    if (ring)
                        PhoneTones.SetContactRing(_toneContactId, captured.Id);
                    else
                        PhoneTones.SetContactText(_toneContactId, captured.Id);
                    DrawContactTones();
                });
            }

            private static void ClearPage(GameObject page)
            {
                if (page == null)
                    return;
                for (int i = page.transform.childCount - 1; i >= 0; i--)
                    Object.Destroy(page.transform.GetChild(i).gameObject);
            }

            private void SetPage(bool keypad, bool contacts, bool recents, bool tones, bool profile)
            {
                if (_keypadPage != null) _keypadPage.SetActive(keypad);
                if (_contactsPage != null) _contactsPage.SetActive(contacts);
                if (_recentsPage != null) _recentsPage.SetActive(recents);
                if (_tonesPage != null) _tonesPage.SetActive(tones);
                if (_profilePage != null) _profilePage.SetActive(profile);
                if (!tones && !profile)
                    _host.SetTitle("Phone");
            }

            private static void ColorTab(Button button, bool on)
            {
                if (button == null)
                    return;
                var img = button.GetComponent<Image>();
                if (img != null)
                    img.color = on ? PhoneUi.SurfaceAlt : PhoneUi.Surface;
            }

            private void PlaceCall()
            {
                if (string.IsNullOrEmpty(_digits))
                {
                    _host.ShowToast("Enter a number.");
                    return;
                }
                if (PhoneNumbers.TryCall(_host, _digits))
                    return;
                int actor;
                if (int.TryParse(_digits, out actor))
                {
                    PhotonPlayer p = BuiltinApps.FindByActor(actor);
                    if (p != null)
                    {
                        CallService.Dial(new[] { actor }, false);
                        return;
                    }
                }
                _host.ShowToast("Not in service. Use Contacts.");
            }

            private void CallSelected()
            {
                if (_selected.Count < 2)
                {
                    _host.ShowToast("Select two or more scouts for a group call.");
                    return;
                }
                var actors = new int[_selected.Count];
                _selected.CopyTo(actors);
                CallService.Dial(actors, true);
            }

        }
    }
}
