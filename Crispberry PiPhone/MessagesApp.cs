using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PhotonPlayer = Photon.Realtime.Player;

namespace Crispberry_PiPhone
{
    internal static class MessagesApp
    {
        private const int MediaCap = 8388608;
        private const int ImageCap = 220000;

        private static Session _live;
        private static string _openId;
        private static string _openName;
        private static int _openActor;

        internal static void Register()
        {
            PiPhoneApi.RegisterApp(new PiPhoneApp
            {
                Id = BuiltinApps.MessagesId,
                DisplayName = "Messages",
                IconGlyph = "M",
                IconBackground = PhoneUi.MessagesIcon,
                SortOrder = 10,
                ShowOnDock = true,
                OnOpen = host =>
                {
                    if (_live != null)
                        _live.Shutdown();
                    _live = new Session(host);
                    _live.Build();
                },
                OnClose = () =>
                {
                    PhoneVideo.StopAll();
                    VoiceIo.StopPlay();
                    if (_live != null)
                        _live.Shutdown();
                    _live = null;
                },
                OnOrientation = () => { if (_live != null) _live.Reload(); }
            });
        }

        internal static void OpenConversation(string threadId, string name)
        {
            _openId = threadId;
            _openName = name ?? "Chat";
            PhotonPlayer p = BuiltinApps.FindById(threadId);
            _openActor = p != null ? p.ActorNumber : 0;
            PhoneMenu.OpenApp(BuiltinApps.MessagesId);
        }

        internal static void RefreshIfOpen()
        {
            if (_live != null)
                _live.Reload();
        }

        internal static bool TryGoBack()
        {
            return _live != null && _live.GoBack();
        }

        private sealed class Session
        {
            private readonly IPiPhoneHost _host;
            private GameObject _listPage;
            private GameObject _threadPage;
            private GameObject _pickPage;
            private RectTransform _listContent;
            private RectTransform _threadContent;
            private ScrollRect _threadScroll;
            private RectTransform _composeRt;
            private GameObject _mediaBar;
            private GameObject _emojiPanel;
            private RectTransform _emojiGrid;
            private TextMeshProUGUI _emojiPageLabel;
            private GameObject _draftFace;
            private int _emojiPage;
            private bool _emojiOpen;
            private TMP_InputField _composer;
            private string _draft = string.Empty;
            private string _shown = string.Empty;
            private char _padChar = '\u00A0';
            private int _padCount = 1;
            private int _logicalCaret;
            private bool _composerLock;
            private Button _micBtn;
            private string _threadId;
            private string _threadName;
            private int _threadActor;
            private bool _recording;
            private bool _selecting;
            private readonly HashSet<string> _picked = new HashSet<string>();

            public Session(IPiPhoneHost host)
            {
                _host = host;
            }

            public void Shutdown()
            {
                Canvas.willRenderCanvases -= PinComposer;
            }

            public bool GoBack()
            {
                if (_emojiOpen)
                {
                    _emojiOpen = false;
                    if (_emojiPanel != null)
                        _emojiPanel.SetActive(false);
                    return true;
                }
                if (_mediaBar != null && _mediaBar.activeSelf)
                {
                    _mediaBar.SetActive(false);
                    return true;
                }
                if (_pickPage != null && _pickPage.activeSelf)
                {
                    ShowThreadUi();
                    return true;
                }
                if (_selecting)
                {
                    _selecting = false;
                    _picked.Clear();
                    FillThread();
                    return true;
                }
                if (_threadPage != null && _threadPage.activeSelf)
                {
                    ShowList();
                    return true;
                }
                return false;
            }

            public void Build()
            {
                Canvas.willRenderCanvases += PinComposer;
                _host.SetTitle("Messages");
                _listPage = Page("List");
                var hint = PhoneUi.CreateLabel(_listPage.transform, "Hint", "Hold a chat to delete it. Texts stay on this phone.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 28f);
                ScrollRect listScroll = PhoneUi.CreateScrollView(_listPage.transform, out _listContent);
                var listLe = listScroll.gameObject.AddComponent<LayoutElement>();
                listLe.flexibleHeight = 1f;
                PhoneUi.AddVertical(_listContent.gameObject, 6f, new RectOffset(2, 2, 2, 2));
                PhoneUi.FitVertical(_listContent.gameObject);

                _threadPage = Page("Thread");
                _threadPage.SetActive(false);
                var top = new GameObject("Top", typeof(RectTransform));
                top.transform.SetParent(_threadPage.transform, false);
                PhoneUi.Size(top, 36f);
                PhoneUi.AddHorizontal(top, 6f);
                PhoneUi.CreateIconChip(top.transform, "<", PhoneIcons.Material("arrow_back"), ShowList, false, new Vector2(36f, 32f));
                PhoneUi.CreateIconChip(top.transform, "Tone", PhoneIcons.Material("library_music"), ShowContactTones, false, new Vector2(36f, 32f));
                PhoneUi.MaterialChip(top.transform, "select", "Select", ToggleSelect, new Vector2(36f, 32f));
                PhoneUi.CreateIconChip(top.transform, "Delete", PhoneIcons.Material("delete"), () =>
                {
                    PhoneStore.DeleteThread(_threadId);
                    _host.ShowToast("Conversation deleted.");
                    ShowList();
                }, false, new Vector2(36f, 32f));

                _threadScroll = PhoneUi.CreateScrollView(_threadPage.transform, out _threadContent);
                var tLe = _threadScroll.gameObject.AddComponent<LayoutElement>();
                tLe.flexibleHeight = 1f;
                PhoneUi.AddVertical(_threadContent.gameObject, 6f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(_threadContent.gameObject);

                var compose = PhoneUi.CreateImage(_threadPage.transform, "Compose", PhoneUi.White(), new Color(1f, 1f, 1f, 0f));
                _composeRt = compose;
                PhoneUi.Size(compose.gameObject, 26f);
                PhoneUi.AddHorizontal(compose.gameObject, 6f);
                var ch = compose.GetComponent<HorizontalLayoutGroup>();
                ch.childAlignment = TextAnchor.LowerCenter;
                ch.childControlHeight = false;
                ch.childForceExpandHeight = false;
                ch.childForceExpandWidth = false;
                ch.padding = new RectOffset(0, 0, 0, 0);
                PhoneUi.CreateIconChip(compose, "+", PhoneIcons.Material("add"), ToggleMedia, false, new Vector2(26f, 26f));
                _composer = PhoneUi.CreateInput(compose, "Message", 2000);
                _composer.lineType = TMP_InputField.LineType.MultiLineNewline;
                _composer.richText = true;
                if (_composer.textViewport != null)
                {
                    _composer.textViewport.offsetMin = new Vector2(8f, 2f);
                    _composer.textViewport.offsetMax = new Vector2(-8f, -2f);
                }
                var inputText = _composer.textComponent as TextMeshProUGUI;
                var inputPh = _composer.placeholder as TextMeshProUGUI;
                if (inputText != null)
                    inputText.alignment = TextAlignmentOptions.MidlineLeft;
                if (inputPh != null)
                    inputPh.alignment = TextAlignmentOptions.MidlineLeft;
                var caretGuard = _composer.gameObject.AddComponent<ComposerCaretGuard>();
                caretGuard.AfterInput = SnapEmojiCaret;
                caretGuard.CopyPlain = CopySelection;
                PhoneUi.Wrap(inputText);
                PhoneUi.Wrap(inputPh);
                PhoneEmoji.Ensure();
                PhoneEmoji.Bind(_composer.textComponent);
                _composer.richText = false;
                if (inputText != null)
                    inputText.richText = false;
                _composer.onValueChanged.AddListener(v => OnComposerChanged());
                PhoneUi.CreateIconChip(compose, "Send", PhoneIcons.Material("send"), SendText, false, new Vector2(26f, 26f));
                ResizeComposer();

                _mediaBar = new GameObject("Media", typeof(RectTransform));
                _mediaBar.transform.SetParent(_threadPage.transform, false);
                PhoneUi.Size(_mediaBar, 40f);
                PhoneUi.AddHorizontal(_mediaBar, 8f);
                var mh = _mediaBar.GetComponent<HorizontalLayoutGroup>();
                mh.childAlignment = TextAnchor.MiddleCenter;
                mh.childForceExpandWidth = false;
                mh.childForceExpandHeight = false;
                PhoneUi.CreateIconChip(_mediaBar.transform, "Emoji", null, ToggleEmoji, false, new Vector2(32f, 32f));
                PhoneUi.CreateIconChip(_mediaBar.transform, "Photos", PhoneIcons.Material("photo_library"), ShowAttach, false, new Vector2(32f, 32f));
                PhoneUi.CreateIconChip(_mediaBar.transform, "GIF", PhoneIcons.Material("gif"), ShowGifSearch, false, new Vector2(32f, 32f));
                var mic = PhoneUi.CreateIconChip(_mediaBar.transform, "Mic", PhoneIcons.Material("mic"), ToggleVoice, false, new Vector2(32f, 32f));
                _micBtn = mic;
                _mediaBar.SetActive(false);
                PaintEmojiButton();

                _pickPage = Page("Pick");
                _pickPage.SetActive(false);

                FillList();
                if (!string.IsNullOrEmpty(_openId))
                {
                    string id = _openId;
                    string name = _openName;
                    int actor = _openActor;
                    _openId = null;
                    OpenThread(id, name, actor);
                }
            }

            public void Reload()
            {
                if (_emojiOpen)
                    ShowEmojiPage();
                if (_threadPage != null && _threadPage.activeSelf && !string.IsNullOrEmpty(_threadId))
                    FillThread();
                else
                    FillList();
            }

            private GameObject Page(string name)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(_host.Content, false);
                var le = go.AddComponent<LayoutElement>();
                le.flexibleHeight = 1f;
                le.minHeight = 240f;
                PhoneUi.AddVertical(go, 6f, new RectOffset(0, 0, 0, 0));
                return go;
            }

            private void FillList()
            {
                Clear(_listContent);
                var seen = new HashSet<string>();
                List<string> ids = PhoneStore.ThreadIds();
                for (int i = 0; i < ids.Count; i++)
                {
                    string id = ids[i];
                    seen.Add(id);
                    string title = PhoneStore.ThreadTitle(id);
                    AddThreadRow(id, title, ActorOf(id), true);
                }

                PhotonPlayer[] others = BuiltinApps.OtherPlayers();
                for (int i = 0; i < others.Length; i++)
                {
                    string id = BuiltinApps.PlayerId(others[i]);
                    if (seen.Contains(id))
                        continue;
                    string name = BuiltinApps.ContactName(others[i]);
                    AddThreadRow(id, name, others[i].ActorNumber, false);
                    seen.Add(id);
                }

                PiPhoneContact[] saved = PhoneContacts.All();
                for (int i = 0; i < saved.Length; i++)
                {
                    PiPhoneContact contact = saved[i];
                    if (contact == null || !PhoneContacts.IsSaved(contact.Id) || seen.Contains(contact.Id))
                        continue;
                    AddThreadRow(contact.Id, PhoneContacts.Display(contact.Id, contact.RealName), 0, false);
                    seen.Add(contact.Id);
                }

                if (_listContent.childCount == 0)
                {
                    var empty = PhoneUi.CreateLabel(_listContent, "Empty", "No conversations yet.", 15f, FontStyles.Normal, TextAlignmentOptions.Center);
                    empty.color = PhoneUi.TextDim;
                    PhoneUi.Size(empty.gameObject, 40f);
                }
            }

            private void AddThreadRow(string id, string title, int actor, bool canDelete)
            {
                var row = new GameObject("T", typeof(RectTransform));
                row.transform.SetParent(_listContent, false);
                PhoneUi.Size(row, 52f);
                PhoneUi.AddHorizontal(row, 6f);
                PhoneContacts.See(id, title);
                string shown = PhoneContacts.Display(id, title);
                PhoneContacts.CreateAvatar(row.transform, id, 36f);
                PhoneUi.CreateButton(row.transform, shown, () => OpenThread(id, shown, actor), new Vector2(canDelete ? 180f : 260f, 40f));
                if (canDelete)
                {
                    PhoneUi.CreateIconChip(row.transform, "Delete", PhoneIcons.Material("delete"), () =>
                    {
                        PhoneStore.DeleteThread(id);
                        FillList();
                    }, false, new Vector2(40f, 40f));
                }
            }

            private void ShowList()
            {
                _host.SetTitle("Messages");
                _selecting = false;
                _picked.Clear();
                if (_threadPage != null)
                    _threadPage.SetActive(false);
                if (_pickPage != null)
                    _pickPage.SetActive(false);
                _listPage.SetActive(true);
                FillList();
            }

            private void ShowThreadUi()
            {
                if (_pickPage != null)
                    _pickPage.SetActive(false);
                _listPage.SetActive(false);
                _threadPage.SetActive(true);
                _host.SetTitle(_threadName ?? "Chat");
            }

            private void OpenThread(string id, string name, int actor)
            {
                _threadId = id;
                _threadName = name;
                _threadActor = actor;
                _selecting = false;
                _picked.Clear();
                _draft = string.Empty;
                if (!string.IsNullOrEmpty(name))
                    PhoneStore.DismissNotices(BuiltinApps.MessagesId, name);
                ShowThreadUi();
                if (_composer != null)
                    PushField(0);
                FillThread();
            }

            private void ToggleMedia()
            {
                if (_mediaBar == null)
                    return;
                bool open = !_mediaBar.activeSelf;
                _mediaBar.SetActive(open);
                if (!open && _emojiOpen)
                {
                    _emojiOpen = false;
                    if (_emojiPanel != null)
                        _emojiPanel.SetActive(false);
                }
            }

            private void ShowContactTones()
            {
                if (_mediaBar != null)
                    _mediaBar.SetActive(false);
                _listPage.SetActive(false);
                _threadPage.SetActive(false);
                _pickPage.SetActive(true);
                Clear(_pickPage.transform);
                _host.SetTitle(_threadName ?? "Sounds");
                PhoneUi.MaterialChip(_pickPage.transform, "arrow_back", "Back", ShowThreadUi, new Vector2(36f, 32f));
                var hint = PhoneUi.CreateLabel(_pickPage.transform, "H", "Empty = Settings default.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Size(hint.gameObject, 22f);
                PhoneUi.CreateButton(_pickPage.transform, "Ringtone  " + PhoneTones.Label(PhoneTones.ContactRing(_threadId)), () => FillContactTones(true), new Vector2(280f, 40f));
                PhoneUi.CreateButton(_pickPage.transform, "Text tone  " + PhoneTones.Label(PhoneTones.ContactText(_threadId)), () => FillContactTones(false), new Vector2(280f, 40f));
            }

            private void FillContactTones(bool ring)
            {
                Clear(_pickPage.transform);
                PhoneUi.MaterialChip(_pickPage.transform, "arrow_back", "Back", ShowContactTones, new Vector2(36f, 32f));
                PhoneUi.CreateButton(_pickPage.transform, "Default", () =>
                {
                    if (ring)
                        PhoneTones.SetContactRing(_threadId, string.Empty);
                    else
                        PhoneTones.SetContactText(_threadId, string.Empty);
                    ShowContactTones();
                }, new Vector2(220f, 40f));
                ScrollRect scroll = PhoneUi.CreateScrollView(_pickPage.transform, out RectTransform content);
                scroll.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
                PhoneUi.AddVertical(content.gameObject, 6f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(content.gameObject);
                PhoneTones.FillPicker(content, () => FillContactTones(ring), captured =>
                {
                    if (ring)
                        PhoneTones.SetContactRing(_threadId, captured.Id);
                    else
                        PhoneTones.SetContactText(_threadId, captured.Id);
                    ShowContactTones();
                });
            }

            private void ToggleSelect()
            {
                _selecting = !_selecting;
                _picked.Clear();
                FillThread();
            }

            private void FillThread()
            {
                Clear(_threadContent);
                if (_selecting)
                {
                    var bar = new GameObject("Sel", typeof(RectTransform));
                    bar.transform.SetParent(_threadContent, false);
                    PhoneUi.Size(bar, 36f);
                    PhoneUi.AddHorizontal(bar, 6f);
                    PhoneUi.MaterialChip(bar.transform, "select_all", "All", () =>
                    {
                        List<ChatMessage> all = PhoneStore.Thread(_threadId);
                        _picked.Clear();
                        for (int i = 0; i < all.Count; i++)
                            _picked.Add(all[i].Id);
                        FillThread();
                    }, new Vector2(36f, 32f));
                    PhoneUi.MaterialChip(bar.transform, "delete", "Delete", () =>
                    {
                        var ids = new List<string>(_picked);
                        PhoneStore.DeleteMessages(_threadId, ids);
                        _selecting = false;
                        _picked.Clear();
                        FillThread();
                    }, new Vector2(36f, 32f));
                    PhoneUi.MaterialChip(bar.transform, "close", "Cancel", ToggleSelect, new Vector2(36f, 32f));
                }

                List<ChatMessage> msgs = PhoneStore.Thread(_threadId);
                if (msgs.Count == 0)
                {
                    var empty = PhoneUi.CreateLabel(_threadContent, "Empty", "No messages yet. They stay saved once sent.", 14f, FontStyles.Normal, TextAlignmentOptions.Center);
                    empty.color = PhoneUi.TextDim;
                    PhoneUi.Wrap(empty);
                    PhoneUi.Size(empty.gameObject, 40f);
                    return;
                }
                string me = BuiltinApps.LocalId();
                for (int i = 0; i < msgs.Count; i++)
                    AddBubble(msgs[i], msgs[i].FromId == me);
                _host.StartHostCoroutine(ScrollThreadEnd());
            }

            private IEnumerator ScrollThreadEnd()
            {
                yield return null;
                Canvas.ForceUpdateCanvases();
                if (_threadScroll != null)
                    _threadScroll.verticalNormalizedPosition = 0f;
                yield return null;
                if (_threadScroll != null)
                    _threadScroll.verticalNormalizedPosition = 0f;
            }

            private const char SoftBreak = '\u2028';

            private void PinComposer()
            {
                if (_composer == null || _composer.textComponent == null)
                    return;
                RectTransform rt = _composer.textComponent.rectTransform;
                Vector2 pos = rt.anchoredPosition;
                Vector2 min = rt.offsetMin;
                Vector2 max = rt.offsetMax;
                if (Mathf.Abs(pos.y) > 0.2f || Mathf.Abs(min.y) > 0.2f || Mathf.Abs(max.y) > 0.2f)
                {
                    rt.anchoredPosition = new Vector2(pos.x, 0f);
                    rt.offsetMin = new Vector2(min.x, 0f);
                    rt.offsetMax = new Vector2(max.x, 0f);
                }
            }

            private void SnapEmojiCaret()
            {
                if (_composerLock || _composer == null || !_composer.isFocused)
                    return;
                string text = _composer.text ?? string.Empty;
                int anchor = SnapDisplayIndex(text, _composer.selectionStringAnchorPosition);
                int focus = SnapDisplayIndex(text, _composer.selectionStringFocusPosition);
                if (anchor == _composer.selectionStringAnchorPosition && focus == _composer.selectionStringFocusPosition)
                    return;
                ApplySelection(anchor, focus);
                _logicalCaret = LogicalCaret(text, focus);
                _composer.ForceLabelUpdate();
            }

            private void CopySelection()
            {
                if (_composer == null || !_composer.isFocused)
                    return;
                string display = _composer.text ?? string.Empty;
                int a = _composer.selectionStringAnchorPosition;
                int b = _composer.selectionStringFocusPosition;
                if (a == b)
                    return;
                if (a > b)
                {
                    int swap = a;
                    a = b;
                    b = swap;
                }
                int plainA = PhoneEmoji.PlainIndexAtField(_draft, LogicalCaret(display, a));
                int plainB = PhoneEmoji.PlainIndexAtField(_draft, LogicalCaret(display, b));
                string draft = _draft ?? string.Empty;
                if (plainA < 0)
                    plainA = 0;
                if (plainB < plainA)
                    plainB = plainA;
                if (plainA > draft.Length)
                    plainA = draft.Length;
                if (plainB > draft.Length)
                    plainB = draft.Length;
                GUIUtility.systemCopyBuffer = draft.Substring(plainA, plainB - plainA);
            }

            private int SnapDisplayIndex(string display, int index)
            {
                if (string.IsNullOrEmpty(display) || index <= 0)
                    return 0;
                if (index >= display.Length)
                    return display.Length;
                int pads = _padCount < 1 ? 1 : _padCount;
                char pad = _padChar == '\0' ? '\u00A0' : _padChar;
                int i = 0;
                while (i < display.Length)
                {
                    if (display[i] == SoftBreak)
                    {
                        i++;
                        continue;
                    }
                    if (display[i] != pad)
                    {
                        if (i >= index)
                            return index;
                        i++;
                        continue;
                    }
                    int start = i;
                    int n = 0;
                    while (i < display.Length && display[i] == pad && n < pads)
                    {
                        n++;
                        i++;
                    }
                    if (index > start && index < i)
                        return (index - start) <= (i - index) ? start : i;
                }
                return index;
            }

            private static string StripSoft(string text)
            {
                if (string.IsNullOrEmpty(text) || text.IndexOf(SoftBreak) < 0)
                    return text ?? string.Empty;
                return text.Replace(SoftBreak.ToString(), string.Empty);
            }

            private int LogicalCaret(string display, int displayIndex)
            {
                if (string.IsNullOrEmpty(display) || displayIndex <= 0)
                    return 0;
                int end = displayIndex < display.Length ? displayIndex : display.Length;
                int pads = _padCount < 1 ? 1 : _padCount;
                char pad = _padChar == '\0' ? '\u00A0' : _padChar;
                int count = 0;
                int i = 0;
                while (i < end)
                {
                    if (display[i] == SoftBreak)
                    {
                        i++;
                        continue;
                    }
                    if (display[i] == pad)
                    {
                        int n = 0;
                        while (i < display.Length && display[i] == pad && n < pads)
                        {
                            n++;
                            i++;
                        }
                        count++;
                        continue;
                    }
                    count++;
                    i++;
                }
                return count;
            }

            private int DisplayCaret(string display, int logicalIndex)
            {
                if (string.IsNullOrEmpty(display) || logicalIndex <= 0)
                    return 0;
                int pads = _padCount < 1 ? 1 : _padCount;
                char pad = _padChar == '\0' ? '\u00A0' : _padChar;
                int seen = 0;
                int i = 0;
                while (i < display.Length)
                {
                    if (display[i] == SoftBreak)
                    {
                        i++;
                        continue;
                    }
                    if (seen == logicalIndex)
                        return i;
                    if (display[i] == pad)
                    {
                        int n = 0;
                        while (i < display.Length && display[i] == pad && n < pads)
                        {
                            n++;
                            i++;
                        }
                        seen++;
                        continue;
                    }
                    seen++;
                    i++;
                }
                return display.Length;
            }

            private string DisplayToLogical(string display)
            {
                if (string.IsNullOrEmpty(display))
                    return string.Empty;
                int pads = _padCount < 1 ? 1 : _padCount;
                char pad = _padChar == '\0' ? '\u00A0' : _padChar;
                var sb = new StringBuilder(display.Length);
                for (int i = 0; i < display.Length; i++)
                {
                    char c = display[i];
                    if (c == SoftBreak)
                        continue;
                    if (c == pad)
                    {
                        int n = 0;
                        while (i < display.Length && display[i] == pad && n < pads)
                        {
                            n++;
                            i++;
                        }
                        i--;
                        if (n > 0)
                            sb.Append(PhoneEmoji.FieldMark);
                        continue;
                    }
                    sb.Append(c);
                }
                return sb.ToString();
            }

            private static bool IsNewlineInsert(string before, string after)
            {
                if (before == null || after == null || after.Length != before.Length + 1)
                    return false;
                int i = 0;
                int n = before.Length;
                while (i < n && before[i] == after[i])
                    i++;
                if (i >= after.Length)
                    return false;
                char c = after[i];
                if (c != '\n' && c != '\r')
                    return false;
                return string.CompareOrdinal(after, i + 1, before, i, n - i) == 0;
            }

            private float EmojiSlot(TMP_Text tmp)
            {
                float size = tmp != null ? tmp.fontSize : 16f;
                if (size < 14f)
                    size = 16f;
                return size + 2f;
            }

            private void MeasurePads(TMP_Text tmp, float slot)
            {
                _padChar = '\u00A0';
                float adv = 0f;
                if (tmp != null && tmp.font != null && tmp.font.characterLookupTable != null)
                {
                    float point = tmp.font.faceInfo.pointSize;
                    float scale = point > 0.01f ? tmp.fontSize / point : 1f;
                    TMP_Character character;
                    if (tmp.font.characterLookupTable.TryGetValue(0x00A0, out character) && character != null && character.glyph != null)
                        adv = character.glyph.metrics.horizontalAdvance * scale;
                    if (adv < 2f && tmp.font.characterLookupTable.TryGetValue(0x0020, out character) && character != null && character.glyph != null)
                        adv = character.glyph.metrics.horizontalAdvance * scale;
                }
                if (adv < 2f)
                    adv = 4f;
                _padCount = Mathf.Clamp(Mathf.CeilToInt(slot / adv), 1, 12);
            }

            private float ComposerWidth()
            {
                if (_composer != null)
                {
                    var field = _composer.GetComponent<RectTransform>();
                    if (field != null)
                        LayoutRebuilder.ForceRebuildLayoutImmediate(field);
                    if (_composer.textViewport != null)
                    {
                        float width = _composer.textViewport.rect.width;
                        if (width > 40f)
                            return width - 4f;
                    }
                    if (field != null && field.rect.width > 40f)
                        return field.rect.width - 16f;
                }
                return Mathf.Max(160f, PhoneUi.ContentWidth() - 70f);
            }

            private string ExpandEmoji(string logical, float limit, float slot)
            {
                if (string.IsNullOrEmpty(logical))
                    return string.Empty;
                int pads = _padCount < 1 ? 1 : _padCount;
                char pad = _padChar == '\0' ? '\u00A0' : _padChar;
                var sb = new StringBuilder(logical.Length + 8);
                float x = 0f;
                float letter = slot * 0.5f;
                for (int i = 0; i < logical.Length; i++)
                {
                    char c = logical[i];
                    if (c == '\n')
                    {
                        sb.Append(c);
                        x = 0f;
                        continue;
                    }
                    if (c == PhoneEmoji.FieldMark)
                    {
                        if (limit > 40f && x > 1f && x + slot > limit)
                        {
                            sb.Append(SoftBreak);
                            x = 0f;
                        }
                        sb.Append(pad, pads);
                        x += slot;
                        continue;
                    }
                    sb.Append(c);
                    x += letter;
                }
                return sb.ToString();
            }

            private void ApplySelection(int anchor, int focus)
            {
                if (_composer == null)
                    return;
                string field = _composer.text ?? string.Empty;
                if (anchor < 0)
                    anchor = 0;
                if (focus < 0)
                    focus = 0;
                if (anchor > field.Length)
                    anchor = field.Length;
                if (focus > field.Length)
                    focus = field.Length;
                TMP_Text tmp = _composer.textComponent;
                if (tmp != null)
                    tmp.ForceMeshUpdate(true);
                _composer.selectionStringAnchorPosition = anchor;
                _composer.selectionStringFocusPosition = focus;
                _logicalCaret = LogicalCaret(field, focus);
            }

            private void ResizeComposer()
            {
                if (_composer == null)
                    return;
                const float bar = 26f;
                TextMeshProUGUI tmp = _composer.textComponent as TextMeshProUGUI;
                if (tmp != null)
                    tmp.richText = false;
                string logical = PhoneEmoji.ToField(_draft);
                bool busy = logical.IndexOf(PhoneEmoji.FieldMark) >= 0 || logical.IndexOf('\n') >= 0 || logical.Length > 24;
                float h = bar;
                if (busy && tmp != null)
                {
                    float slot = EmojiSlot(tmp);
                    if (logical.IndexOf(PhoneEmoji.FieldMark) >= 0)
                        MeasurePads(tmp, slot);
                    _composerLock = true;
                    string previous = _composer.text ?? string.Empty;
                    int anchor = _composer.selectionStringAnchorPosition;
                    int focus = _composer.selectionStringFocusPosition;
                    int logicalAnchor = LogicalCaret(previous, anchor);
                    int logicalFocus = LogicalCaret(previous, focus);
                    string flowed = logical.IndexOf(PhoneEmoji.FieldMark) >= 0
                        ? ExpandEmoji(logical, ComposerWidth(), slot)
                        : logical;
                    bool changed = previous != flowed;
                    if (changed)
                        _composer.text = flowed;
                    tmp.ForceMeshUpdate(true);
                    int lines = tmp.textInfo != null ? tmp.textInfo.lineCount : 1;
                    TextAlignmentOptions align = lines > 1 ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;
                    if (tmp.alignment != align)
                    {
                        tmp.alignment = align;
                        tmp.ForceMeshUpdate(true);
                        lines = tmp.textInfo != null ? tmp.textInfo.lineCount : lines;
                    }
                    if (changed)
                        ApplySelection(DisplayCaret(_composer.text, logicalAnchor), DisplayCaret(_composer.text, logicalFocus));
                    else
                        _logicalCaret = logicalFocus;
                    _shown = _composer.text ?? string.Empty;
                    _composerLock = false;
                    if (lines > 1)
                    {
                        float sum = 0f;
                        if (tmp.textInfo != null && tmp.textInfo.lineInfo != null)
                        {
                            int n = lines < tmp.textInfo.lineInfo.Length ? lines : tmp.textInfo.lineInfo.Length;
                            for (int i = 0; i < n; i++)
                                sum += Mathf.Max(slot, tmp.textInfo.lineInfo[i].lineHeight);
                        }
                        else
                            sum = lines * slot;
                        h = Mathf.Clamp(sum + 16f, bar, 280f);
                    }
                }
                else if (tmp != null && tmp.alignment != TextAlignmentOptions.MidlineLeft)
                    tmp.alignment = TextAlignmentOptions.MidlineLeft;
                var rt = _composer.GetComponent<RectTransform>();
                if (rt != null && Mathf.Abs(rt.rect.height - h) > 0.5f)
                    rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
                var le = _composer.GetComponent<LayoutElement>();
                if (le != null && Mathf.Abs(le.preferredHeight - h) > 0.5f)
                {
                    le.minHeight = h;
                    le.preferredHeight = h;
                    le.flexibleHeight = 0f;
                    le.flexibleWidth = 1f;
                }
                if (_composeRt != null && Mathf.Abs(_composeRt.rect.height - h) > 0.5f)
                    PhoneUi.Size(_composeRt.gameObject, h);
            }

            private void SendText()
            {
                if (_composer == null || string.IsNullOrEmpty(_threadId))
                    return;
                string text = (_draft ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(text))
                    return;
                if (_threadActor <= 0)
                {
                    _host.ShowToast("Failed to send.");
                    return;
                }
                var msg = new ChatMessage
                {
                    Id = PhoneStore.NewId(),
                    ThreadId = _threadId,
                    FromId = BuiltinApps.LocalId(),
                    FromName = BuiltinApps.LocalName(),
                    Text = text,
                    UnixMs = PhoneStore.NowMs()
                };
                PhoneStore.AddMessage(msg);
                _draft = string.Empty;
                PushField(0);
                PhoneNet.SendText(_threadActor, msg);
                FillThread();
            }

            private void ToggleEmoji()
            {
                _emojiOpen = !_emojiOpen;
                if (_emojiOpen)
                    PhoneEmoji.Rush();
                if (_emojiPanel == null)
                    BuildEmojiPanel();
                if (_emojiPanel != null)
                {
                    _emojiPanel.SetActive(_emojiOpen);
                    if (_emojiOpen)
                        _emojiPanel.transform.SetAsLastSibling();
                }
            }

            private void PaintEmojiButton()
            {
                if (_mediaBar == null)
                    return;
                Transform chip = _mediaBar.transform.Find("Chip");
                if (chip == null)
                    return;
                Sprite smile = PhoneEmoji.SpriteFor("\U0001F60A");
                if (smile == null && PhoneEmoji.Names.Count > 0)
                    smile = PhoneEmoji.SpriteFor(PhoneEmoji.Sequence(PhoneEmoji.Names[0]));
                var btn = chip.GetComponent<Button>();
                if (btn != null && smile != null)
                {
                    PhoneUi.SetChipIcon(btn, smile, "Emoji", false);
                }
            }

            private void BuildEmojiPanel()
            {
                if (_threadPage == null)
                    return;
                PhoneEmoji.Ensure();
                _emojiPanel = new GameObject("Emoji", typeof(RectTransform));
                _emojiPanel.transform.SetParent(_threadPage.transform, false);
                _emojiPanel.transform.SetAsLastSibling();
                PhoneUi.Size(_emojiPanel, 196f);
                PhoneUi.AddVertical(_emojiPanel, 4f, new RectOffset(0, 0, 0, 0));

                var pages = new GameObject("Pages", typeof(RectTransform));
                pages.transform.SetParent(_emojiPanel.transform, false);
                PhoneUi.Size(pages, 32f);
                var pageRow = PhoneUi.AddHorizontal(pages, 8f);
                pageRow.childForceExpandWidth = false;
                pageRow.childAlignment = TextAnchor.MiddleCenter;
                PhoneUi.CreateButton(pages.transform, "<", () => StepEmojiPage(-1), new Vector2(36f, 28f));
                _emojiPageLabel = PhoneUi.CreateLabel(pages.transform, "Page", "1 / 1", 14f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Size(_emojiPageLabel.gameObject, 28f, 72f);
                PhoneUi.CreateButton(pages.transform, ">", () => StepEmojiPage(1), new Vector2(36f, 28f));

                var gridGo = new GameObject("Grid", typeof(RectTransform));
                gridGo.transform.SetParent(_emojiPanel.transform, false);
                var gridLe = gridGo.AddComponent<LayoutElement>();
                gridLe.flexibleWidth = 1f;
                gridLe.preferredHeight = 156f;
                gridLe.minHeight = 156f;
                _emojiGrid = gridGo.GetComponent<RectTransform>();
                ShowEmojiPage();
                PaintEmojiButton();
            }

            private void StepEmojiPage(int delta)
            {
                int pages = EmojiPageCount();
                if (pages < 1)
                    pages = 1;
                _emojiPage += delta;
                if (_emojiPage < 0)
                    _emojiPage = pages - 1;
                if (_emojiPage >= pages)
                    _emojiPage = 0;
                ShowEmojiPage();
            }

            private int EmojiColumnCount(out float cell)
            {
                float width = PhoneUi.ContentWidth() - 28f;
                if (width < 120f)
                    width = 120f;
                const float gap = 6f;
                const float pad = 8f;
                int cols = Mathf.FloorToInt((width - pad + gap) / (36f + gap));
                if (cols < 4)
                    cols = 4;
                cell = Mathf.Floor((width - pad - gap * (cols - 1)) / cols);
                if (cell < 28f)
                    cell = 28f;
                if (cell > 48f)
                    cell = 48f;
                return cols;
            }

            private int EmojiPageCount()
            {
                float cell;
                int cols = EmojiColumnCount(out cell);
                int pageSize = cols * 4;
                int count = PhoneEmoji.Names.Count;
                if (pageSize < 1)
                    return 1;
                return Mathf.Max(1, (count + pageSize - 1) / pageSize);
            }

            private void ShowEmojiPage()
            {
                if (_emojiGrid == null)
                    return;
                for (int i = _emojiGrid.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(_emojiGrid.GetChild(i).gameObject);
                float cell;
                int cols = EmojiColumnCount(out cell);
                int pageSize = cols * 4;
                int pages = EmojiPageCount();
                if (_emojiPage >= pages)
                    _emojiPage = pages - 1;
                if (_emojiPage < 0)
                    _emojiPage = 0;
                if (_emojiPageLabel != null)
                    _emojiPageLabel.text = (_emojiPage + 1) + " / " + pages;
                var grid = _emojiGrid.gameObject.GetComponent<GridLayoutGroup>();
                if (grid == null)
                    grid = _emojiGrid.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(cell, cell);
                grid.spacing = new Vector2(6f, 6f);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = cols;
                grid.padding = new RectOffset(4, 4, 2, 2);
                grid.childAlignment = TextAnchor.UpperLeft;
                grid.startAxis = GridLayoutGroup.Axis.Horizontal;
                grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
                IList<string> names = PhoneEmoji.Names;
                int start = _emojiPage * pageSize;
                int end = start + pageSize;
                if (end > names.Count)
                    end = names.Count;
                for (int i = start; i < end; i++)
                {
                    string seq = PhoneEmoji.Sequence(names[i]);
                    Sprite sprite = PhoneEmoji.SpriteFor(seq);
                    if (sprite == null || string.IsNullOrEmpty(seq))
                        continue;
                    string captured = seq;
                    PhoneUi.CreateIconChip(_emojiGrid, names[i], sprite, () => InsertEmoji(captured), false, new Vector2(cell, cell), false);
                }
            }

            private void OnComposerChanged()
            {
                if (_composerLock || _composer == null)
                    return;
                string raw = _composer.text ?? string.Empty;
                string before = PhoneEmoji.ToField(_draft);
                string now = DisplayToLogical(raw);
                if (IsNewlineInsert(before, now))
                {
                    bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    if (!shift)
                    {
                        int at = now.LastIndexOf('\n');
                        if (at < 0)
                            at = now.LastIndexOf('\r');
                        string trimmed = at >= 0 ? now.Remove(at, 1) : now;
                        _draft = PhoneEmoji.ApplyFieldEdit(_draft, before, trimmed);
                        if (string.IsNullOrEmpty(PhoneEmoji.ToField(_draft)) || (!PhoneEmoji.HasEmoji(_draft) && string.IsNullOrEmpty(PhoneEmoji.ToField(_draft).Trim())))
                        {
                            PushField(trimmed.Length);
                            return;
                        }
                        SendText();
                        return;
                    }
                }
                if (string.Equals(now, before, StringComparison.Ordinal))
                {
                    ResizeComposer();
                    RefreshDraft();
                    return;
                }
                _draft = PhoneEmoji.ApplyFieldEdit(_draft, before, now);
                string field = PhoneEmoji.ToField(_draft);
                if (string.Equals(field, DisplayToLogical(_composer.text), StringComparison.Ordinal))
                {
                    ResizeComposer();
                    RefreshDraft();
                    return;
                }
                PushField(LogicalCaret(raw, _composer.stringPosition));
            }

            private void PushField(int logicalCaret)
            {
                if (_composer == null)
                    return;
                _composerLock = true;
                string field = PhoneEmoji.ToField(_draft);
                if (StripSoft(_composer.text) != field)
                    _composer.text = field;
                ApplySelection(logicalCaret, logicalCaret);
                _shown = _composer.text ?? string.Empty;
                _composerLock = false;
                ResizeComposer();
                RefreshDraft();
            }

            private void InsertEmoji(string face)
            {
                if (_composer == null || string.IsNullOrEmpty(face))
                    return;
                string field = PhoneEmoji.ToField(_draft);
                int at = LogicalCaret(_composer.text, _composer.selectionStringFocusPosition);
                if (at < 0)
                    at = 0;
                if (at > field.Length)
                    at = field.Length;
                int plainAt = PhoneEmoji.PlainIndexAtField(_draft, at);
                _draft = (_draft ?? string.Empty).Insert(plainAt, face);
                PushField(at + 1);
            }

            private void RefreshDraft()
            {
                if (_composer == null || _composer.textComponent == null)
                    return;
                var tmp = _composer.textComponent as TextMeshProUGUI;
                if (tmp != null)
                    tmp.color = PhoneUi.Text;
                bool emoji = PhoneEmoji.HasEmoji(_draft);
                if (_draftFace == null && _composer.textComponent != null)
                {
                    _draftFace = new GameObject("DraftFace", typeof(RectTransform));
                    _draftFace.transform.SetParent(_composer.textComponent.transform, false);
                    var canvas = _draftFace.AddComponent<CanvasGroup>();
                    canvas.blocksRaycasts = false;
                    canvas.interactable = false;
                }
                if (_draftFace == null)
                    return;
                for (int i = _draftFace.transform.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(_draftFace.transform.GetChild(i).gameObject);
                if (!emoji || tmp == null)
                {
                    _draftFace.SetActive(false);
                    return;
                }
                _draftFace.SetActive(true);
                var faceRt = _draftFace.GetComponent<RectTransform>();
                faceRt.anchorMin = Vector2.zero;
                faceRt.anchorMax = Vector2.one;
                faceRt.offsetMin = Vector2.zero;
                faceRt.offsetMax = Vector2.zero;
                tmp.ForceMeshUpdate(true);
                TMP_TextInfo info = tmp.textInfo;
                if (info == null || info.characterInfo == null)
                    return;
                string field = _composer.text ?? string.Empty;
                float slot = EmojiSlot(tmp);
                int pads = _padCount < 1 ? 1 : _padCount;
                char pad = _padChar == '\0' ? '\u00A0' : _padChar;
                Vector2 pivot = tmp.rectTransform.pivot;
                int placed = 0;
                int logicalIndex = 0;
                int ci = 0;
                while (ci < field.Length && ci < info.characterCount)
                {
                    if (field[ci] == SoftBreak)
                    {
                        ci++;
                        continue;
                    }
                    if (field[ci] != pad)
                    {
                        logicalIndex++;
                        ci++;
                        continue;
                    }
                    int start = ci;
                    int nPads = 0;
                    while (ci < field.Length && ci < info.characterCount && field[ci] == pad && nPads < pads)
                    {
                        nPads++;
                        ci++;
                    }
                    int plainAt = PhoneEmoji.PlainIndexAtField(_draft, logicalIndex);
                    logicalIndex++;
                    int n = PhoneEmoji.SequenceLength(_draft, plainAt);
                    Sprite sprite = n > 0 ? PhoneEmoji.SpriteFor(_draft.Substring(plainAt, n)) : null;
                    TMP_CharacterInfo first = info.characterInfo[start];
                    TMP_CharacterInfo last = info.characterInfo[start + nPads - 1];
                    float run = last.xAdvance - first.origin;
                    float lineH = slot;
                    float midY = (first.bottomLeft.y + first.topRight.y) * 0.5f;
                    if (info.lineInfo != null && first.lineNumber >= 0 && first.lineNumber < info.lineCount && first.lineNumber < info.lineInfo.Length)
                    {
                        TMP_LineInfo line = info.lineInfo[first.lineNumber];
                        if (line.lineHeight > 1f)
                            lineH = line.lineHeight;
                        midY = (line.ascender + line.descender) * 0.5f;
                    }
                    float size = Mathf.Min(lineH * 0.92f, Mathf.Max(run, 4f));
                    if (sprite != null)
                    {
                        var img = PhoneUi.CreateImage(_draftFace.transform, "E", sprite, Color.white);
                        var art = img.GetComponent<Image>();
                        art.preserveAspect = true;
                        art.raycastTarget = false;
                        img.anchorMin = img.anchorMax = pivot;
                        img.pivot = new Vector2(0.5f, 0.5f);
                        img.anchoredPosition = new Vector2(first.origin + run * 0.5f, midY);
                        img.sizeDelta = new Vector2(size, size);
                        placed++;
                    }
                    for (int p = start; p < start + nPads && p < info.characterCount; p++)
                    {
                        TMP_CharacterInfo ch = info.characterInfo[p];
                        int mesh = ch.materialReferenceIndex;
                        int vi = ch.vertexIndex;
                        if (info.meshInfo == null || mesh < 0 || mesh >= info.meshInfo.Length)
                            continue;
                        Color32[] colors = info.meshInfo[mesh].colors32;
                        if (colors != null && vi >= 0 && vi + 3 < colors.Length)
                        {
                            colors[vi].a = 0;
                            colors[vi + 1].a = 0;
                            colors[vi + 2].a = 0;
                            colors[vi + 3].a = 0;
                        }
                    }
                }
                if (placed > 0)
                    tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            }

            private void ToggleVoice()
            {
                if (_recording)
                {
                    _recording = false;
                    if (_micBtn != null)
                        PhoneUi.SetChipIcon(_micBtn, PhoneIcons.Material("mic"), "Mic");
                    object clip = VoiceIo.StopRecord();
                    if (clip == null)
                    {
                        _host.ShowToast("No audio. Talk or hold PTT while you record.");
                        return;
                    }
                    if (_threadActor <= 0)
                    {
                        _host.ShowToast("Failed to send.");
                        return;
                    }
                    byte[] wav = VoiceIo.ToWav(clip);
                    string id = PhoneStore.NewId();
                    string file = PhoneStore.SaveAudio(id, wav);
                    var msg = new ChatMessage
                    {
                        Id = id,
                        ThreadId = _threadId,
                        FromId = BuiltinApps.LocalId(),
                        FromName = BuiltinApps.LocalName(),
                        Text = "Voice message",
                        AudioFile = file,
                        UnixMs = PhoneStore.NowMs()
                    };
                    PhoneStore.AddMessage(msg);
                    PhoneNet.SendVoice(_threadActor, msg, wav);
                    FillThread();
                    _host.ShowToast("Voice message sent.");
                    return;
                }
                if (!VoiceIo.StartRecord())
                {
                    _host.ShowToast("Microphone unavailable.");
                    return;
                }
                _recording = true;
                if (_micBtn != null)
                    PhoneUi.SetChipIcon(_micBtn, PhoneIcons.Material("stop"), "Stop");
                _host.ShowToast("Recording... tap again to send.");
            }

            private void ShowAttach()
            {
                if (_mediaBar != null)
                    _mediaBar.SetActive(false);
                PhoneUi.ReleaseUiFocus();
                _listPage.SetActive(false);
                _threadPage.SetActive(false);
                _pickPage.SetActive(true);
                Clear(_pickPage.transform);
                _host.SetTitle("Send media");
                PhoneUi.MaterialChip(_pickPage.transform, "arrow_back", "Back", ShowThreadUi, new Vector2(36f, 32f));
                PhoneUi.CreateButton(_pickPage.transform, "Photos", () => ShowAttachFolder("photos"), new Vector2(280f, 44f));
                var note = PhoneUi.CreateLabel(_pickPage.transform, "Note", "Photos, GIFs, and voice messages can be sent. Videos and other files are staying off until they can go through without dropping the lobby.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                note.color = PhoneUi.TextDim;
                PhoneUi.Wrap(note);
                PhoneUi.Size(note.gameObject, 56f);
            }

            private void ShowAttachFolder(string folder)
            {
                if (_mediaBar != null)
                    _mediaBar.SetActive(false);
                PhoneUi.ReleaseUiFocus();
                _listPage.SetActive(false);
                _threadPage.SetActive(false);
                _pickPage.SetActive(true);
                Clear(_pickPage.transform);
                string title = folder == "downloads" ? "Downloads" : (folder == "videos" ? "Videos" : (folder == "audio" ? "Audio" : "Photos"));
                _host.SetTitle(title);
                PhoneUi.MaterialChip(_pickPage.transform, "arrow_back", "Back", ShowAttach, new Vector2(36f, 32f));
                if (folder == "audio")
                {
                    FillAttachAudio();
                    return;
                }
                ScrollRect scroll = PhoneUi.CreateScrollView(_pickPage.transform, out RectTransform content);
                scroll.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
                PhoneUi.ApplyMediaGrid(content);
                int shown = 0;
                if (folder == "photos" || folder == "videos")
                {
                    bool wantVideo = folder == "videos";
                    for (int i = 0; i < PhoneStore.Photos.Count; i++)
                    {
                        PhotoItem p = PhoneStore.Photos[i];
                        if (p == null || p.Video != wantVideo)
                            continue;
                        PhotoItem captured = p;
                        string path = p.Video ? PhoneStore.VideoThumbPath(p) : PhoneStore.PhotoPath(p.File);
                        AddAttachThumb(content, path, p.Video ? "VID" : null, () => _host.StartHostCoroutine(SendPhoto(captured)));
                        shown++;
                    }
                }
                else
                {
                    for (int i = 0; i < PhoneStore.Downloads.Count; i++)
                    {
                        DownloadItem d = PhoneStore.Downloads[i];
                        if (d == null)
                            continue;
                        DownloadItem captured = d;
                        string path = PhoneStore.DownloadPath(d.File);
                        string badge = null;
                        if (PhoneVideo.IsVideoPath(path) || IsAudioPath(path))
                            badge = IsAudioPath(path) ? "AUD" : "VID";
                        AddAttachThumb(content, path, badge, () => _host.StartHostCoroutine(SendDownload(captured)));
                        shown++;
                    }
                }
                if (shown == 0)
                {
                    var empty = PhoneUi.CreateLabel(_pickPage.transform, "Empty", "Nothing in here yet.", 14f, FontStyles.Normal, TextAlignmentOptions.Center);
                    empty.color = PhoneUi.TextDim;
                    PhoneUi.Size(empty.gameObject, 36f);
                }
            }

            private void FillAttachAudio()
            {
                ScrollRect scroll = PhoneUi.CreateScrollView(_pickPage.transform, out RectTransform content);
                scroll.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
                PhoneUi.AddVertical(content.gameObject, 6f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(content.gameObject);
                int shown = 0;
                List<SoundItem> tracks = PhoneStore.MusicTracks();
                for (int i = 0; i < tracks.Count; i++)
                {
                    SoundItem s = tracks[i];
                    if (s == null)
                        continue;
                    SoundItem captured = s;
                    PhoneUi.CreateButton(content, captured.Name, () => SendAudioFile(captured), new Vector2(280f, 40f));
                    shown++;
                }
                List<SoundItem> alerts = PhoneStore.AlertTones();
                for (int i = 0; i < alerts.Count; i++)
                {
                    SoundItem s = alerts[i];
                    if (s == null)
                        continue;
                    SoundItem captured = s;
                    PhoneUi.CreateButton(content, captured.Name + "  (alert)", () => SendAudioFile(captured), new Vector2(280f, 40f));
                    shown++;
                }
                if (shown == 0)
                {
                    var empty = PhoneUi.CreateLabel(content, "Empty", "No audio to send yet.", 14f, FontStyles.Normal, TextAlignmentOptions.Center);
                    empty.color = PhoneUi.TextDim;
                    PhoneUi.Size(empty.gameObject, 36f);
                }
            }

            private static void AddAttachThumb(Transform parent, string path, string badge, UnityEngine.Events.UnityAction onClick)
            {
                var thumb = PhoneUi.CreateImage(parent, "T", PhoneUi.Rounded(10), PhoneUi.Surface);
                Texture2D tex = PhoneImages.LoadFile(path);
                if (tex != null)
                {
                    var rawGo = new GameObject("Raw", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                    rawGo.transform.SetParent(thumb, false);
                    PhoneUi.Stretch(rawGo.GetComponent<RectTransform>(), 4f, 4f);
                    var raw = rawGo.GetComponent<RawImage>();
                    raw.texture = tex;
                    raw.color = Color.white;
                    raw.raycastTarget = false;
                    PhoneUi.FitContained(raw, tex);
                }
                else
                {
                    var label = PhoneUi.CreateLabel(thumb, "Miss", badge ?? "?", 14f, FontStyles.Normal, TextAlignmentOptions.Center);
                    PhoneUi.Stretch(label.rectTransform, 4f, 4f);
                }
                if (!string.IsNullOrEmpty(badge) && tex != null)
                {
                    var tag = PhoneUi.CreateLabel(thumb, "Badge", badge, 11f, FontStyles.Normal, TextAlignmentOptions.BottomRight);
                    PhoneUi.Stretch(tag.rectTransform, 6f, 4f);
                }
                var btn = thumb.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                if (onClick != null)
                    btn.onClick.AddListener(onClick);
            }

            private void ShowGifSearch()
            {
                if (_mediaBar != null)
                    _mediaBar.SetActive(false);
                PhoneUi.ReleaseUiFocus();
                _listPage.SetActive(false);
                _threadPage.SetActive(false);
                _pickPage.SetActive(true);
                Clear(_pickPage.transform);
                _host.SetTitle("GIFs");
                PhoneUi.MaterialChip(_pickPage.transform, "arrow_back", "Back", ShowThreadUi, new Vector2(36f, 32f));

                var searchRow = new GameObject("SearchRow", typeof(RectTransform));
                searchRow.transform.SetParent(_pickPage.transform, false);
                PhoneUi.Size(searchRow, 40f);
                PhoneUi.AddHorizontal(searchRow, 6f);
                TMP_InputField q = PhoneUi.CreateInput(searchRow.transform, "Search GIFs", 80);
                q.lineType = TMP_InputField.LineType.SingleLine;

                ScrollRect scroll = PhoneUi.CreateScrollView(_pickPage.transform, out RectTransform content);
                scroll.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
                PhoneUi.CreateButton(searchRow.transform, "Go", () => _host.StartHostCoroutine(RunGifSearch(q.text, content)), new Vector2(56f, 36f));
                q.onSubmit.AddListener(s => _host.StartHostCoroutine(RunGifSearch(s, content)));
                _host.StartHostCoroutine(ActivateField(q));
            }

            private static IEnumerator ActivateField(TMP_InputField input)
            {
                yield return null;
                PhoneUi.FocusInput(input);
            }

            private static void ResetPickLayout(RectTransform content)
            {
                if (content == null)
                    return;
                var grid = content.GetComponent<GridLayoutGroup>();
                if (grid != null)
                    UnityEngine.Object.DestroyImmediate(grid);
                var vert = content.GetComponent<VerticalLayoutGroup>();
                if (vert != null)
                    UnityEngine.Object.DestroyImmediate(vert);
                var fit = content.GetComponent<ContentSizeFitter>();
                if (fit != null)
                    UnityEngine.Object.DestroyImmediate(fit);
            }

            private IEnumerator RunGifSearch(string query, RectTransform content)
            {
                Clear(content);
                ResetPickLayout(content);
                PhoneUi.AddVertical(content.gameObject, 6f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(content.gameObject);
                var wait = PhoneUi.CreateLabel(content, "W", "Searching...", 14f, FontStyles.Normal, TextAlignmentOptions.Center);
                wait.color = PhoneUi.TextDim;
                List<GiphyHit> hits = null;
                string err = null;
                yield return PhoneGiphy.Search(query, (list, e) => { hits = list; err = e; });
                Clear(content);
                ResetPickLayout(content);
                if (hits == null || hits.Count == 0)
                {
                    PhoneUi.AddVertical(content.gameObject, 6f, new RectOffset(4, 4, 4, 4));
                    PhoneUi.FitVertical(content.gameObject);
                    var empty = PhoneUi.CreateLabel(content, "E", err ?? "No GIFs.", 14f, FontStyles.Normal, TextAlignmentOptions.Center);
                    empty.color = PhoneUi.TextDim;
                    yield break;
                }
                PhoneUi.ApplyMediaGrid(content);
                for (int i = 0; i < hits.Count; i++)
                {
                    GiphyHit hit = hits[i];
                    string thumbUrl = !string.IsNullOrEmpty(hit.ThumbUrl) ? hit.ThumbUrl : hit.Url;
                    byte[] data = null;
                    yield return PhoneImages.Download(thumbUrl, (b, e, er) => { data = b; });
                    Texture2D tex = data != null ? PhoneImages.LoadTexture(data) : null;
                    AddGifThumb(content, tex, hit);
                }
            }

            private void AddGifThumb(Transform parent, Texture2D tex, GiphyHit hit)
            {
                var thumb = PhoneUi.CreateImage(parent, "G", PhoneUi.Rounded(10), PhoneUi.Surface);
                if (tex != null)
                {
                    var rawGo = new GameObject("Raw", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                    rawGo.transform.SetParent(thumb, false);
                    PhoneUi.Stretch(rawGo.GetComponent<RectTransform>(), 2f, 2f);
                    var raw = rawGo.GetComponent<RawImage>();
                    raw.texture = tex;
                    raw.color = Color.white;
                    raw.raycastTarget = false;
                    PhoneUi.FitContained(raw, tex);
                    var hover = thumb.gameObject.AddComponent<GifHover>();
                    hover.Raw = raw;
                    hover.Still = tex;
                    hover.Hit = hit;
                }
                else
                {
                    var label = PhoneUi.CreateLabel(thumb, "Miss", "GIF", 12f, FontStyles.Normal, TextAlignmentOptions.Center);
                    PhoneUi.Stretch(label.rectTransform, 4f, 4f);
                }
                GiphyHit captured = hit;
                var btn = thumb.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => _host.StartHostCoroutine(SendGifUrl(captured)));
            }

            private IEnumerator SendGifUrl(GiphyHit hit)
            {
                _host.ShowToast("Loading GIF...");
                byte[] data = null;
                string ext = null;
                string err = null;
                string url = hit != null ? (hit.Url ?? hit.ThumbUrl) : null;
                yield return PhoneImages.Download(url, (b, e, er) => { data = b; ext = e; err = er; });
                if (data == null || !PhoneGif.IsGif(data))
                {
                    _host.ShowToast(err ?? "Couldn't load that GIF.");
                    yield break;
                }
                SendMediaBytes(data, string.IsNullOrEmpty(ext) ? ".gif" : ext, "gif", hit.Title);
            }

            private IEnumerator SendPhoto(PhotoItem photo)
            {
                if (photo.Video)
                {
                    _host.ShowToast("Videos can't be sent yet.");
                    yield break;
                }
                string file = PhoneStore.PhotoPath(photo.File);
                if (string.IsNullOrEmpty(file) || !File.Exists(file))
                {
                    _host.ShowToast("Missing photo.");
                    yield break;
                }
                SendMediaBytes(ShrinkImage(File.ReadAllBytes(file)), ".jpg", "img", "Photo");
            }

            private IEnumerator SendDownload(DownloadItem item)
            {
                string path = PhoneStore.DownloadPath(item.File);
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    _host.ShowToast("Missing file.");
                    yield break;
                }
                byte[] data = File.ReadAllBytes(path);
                string ext = Path.GetExtension(item.File);
                if (PhoneGif.IsGif(data) || string.Equals(ext, ".gif", StringComparison.OrdinalIgnoreCase))
                {
                    SendMediaBytes(data, ".gif", "gif", "GIF");
                    yield break;
                }
                if (PhoneVideo.IsVideoPath(path) || PhoneImages.GuessExtFromBytes(data, null) == ".avi" || PhoneImages.GuessExtFromBytes(data, null) == ".mp4")
                {
                    SendMediaBytes(data, string.IsNullOrEmpty(ext) ? ".mp4" : ext, "vid", "Video");
                    yield break;
                }
                if (IsAudioPath(path))
                {
                    SendMediaBytes(data, string.IsNullOrEmpty(ext) ? ".wav" : ext, "aud", "Audio");
                    yield break;
                }
                SendMediaBytes(ShrinkImage(data), ".jpg", "img", "Photo");
            }

            private void SendAudioFile(SoundItem sound)
            {
                string path = PhoneStore.SoundPath(sound.File);
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    _host.ShowToast("Missing audio.");
                    return;
                }
                byte[] data = File.ReadAllBytes(path);
                string ext = Path.GetExtension(path);
                if (string.IsNullOrEmpty(ext))
                    ext = ".wav";
                SendMediaBytes(data, ext, "aud", sound.Name);
            }

            private void SendMediaBytes(byte[] data, string ext, string kind, string label)
            {
                if (kind == "vid" || kind == "aud")
                {
                    _host.ShowToast("That can't be sent yet.");
                    return;
                }
                if (data == null || data.Length < 8)
                {
                    _host.ShowToast("Couldn't send that.");
                    return;
                }
                int cap = kind == "img" ? ImageCap : PhoneNet.MaxMediaBytes;
                if (data.Length > cap)
                {
                    _host.ShowToast("Too large to send (" + PhoneNet.SizeLabel(data.Length) + ", max " + PhoneNet.SizeLabel(cap) + ").");
                    return;
                }
                if (_threadActor <= 0)
                {
                    _host.ShowToast("Failed to send.");
                    return;
                }
                string mediaFile;
                if (kind == "gif" || kind == "aud")
                {
                    mediaFile = PhoneStore.SaveChatMedia(data, ext);
                    if (kind == "aud")
                    {
                        string src = PhoneStore.MediaPath(mediaFile);
                        if (!string.IsNullOrEmpty(src))
                            PhoneStore.AddSound(src, false, 0f);
                    }
                }
                else
                {
                    DownloadItem saved = PhoneStore.AddDownload(data, ext, string.Empty);
                    mediaFile = saved != null ? "downloads/" + saved.File : string.Empty;
                }
                var msg = new ChatMessage
                {
                    Id = PhoneStore.NewId(),
                    ThreadId = _threadId,
                    FromId = BuiltinApps.LocalId(),
                    FromName = BuiltinApps.LocalName(),
                    Text = label,
                    MediaFile = mediaFile,
                    MediaKind = kind,
                    UnixMs = PhoneStore.NowMs()
                };
                PhoneStore.AddMessage(msg);
                PhoneNet.SendMedia(_threadActor, msg, data, kind);
                ShowThreadUi();
                FillThread();
            }

            private static byte[] ShrinkImage(byte[] bytes)
            {
                Texture2D tex = PhoneImages.LoadTexture(bytes);
                if (tex == null)
                    return bytes;
                int w = tex.width;
                int h = tex.height;
                int max = 480;
                if (w > max || h > max)
                {
                    float s = max / (float)Mathf.Max(w, h);
                    int nw = Mathf.Max(2, Mathf.RoundToInt(w * s));
                    int nh = Mathf.Max(2, Mathf.RoundToInt(h * s));
                    var rt = RenderTexture.GetTemporary(nw, nh, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(tex, rt);
                    RenderTexture prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    var small = new Texture2D(nw, nh, TextureFormat.RGB24, false);
                    small.ReadPixels(new Rect(0, 0, nw, nh), 0, 0);
                    small.Apply();
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);
                    UnityEngine.Object.Destroy(tex);
                    tex = small;
                }
                byte[] jpg = PhoneImages.EncodeJpg(tex, 48);
                UnityEngine.Object.Destroy(tex);
                return jpg != null && jpg.Length > 16 ? jpg : bytes;
            }

            private void AddBubble(ChatMessage msg, bool mine)
            {
                string path = PhoneStore.MediaPath(msg.MediaFile);
                bool hasMedia = !string.IsNullOrEmpty(msg.MediaFile);
                bool audio = hasMedia && (msg.MediaKind == "aud" || IsAudioPath(path));
                bool visual = hasMedia && !audio;
                string body = BreakLong(msg.Text ?? string.Empty);
                if (!string.IsNullOrEmpty(msg.AudioFile))
                    body = "▶ " + body;
                bool caption = visual && HasMediaCaption(msg, body);
                bool showBubble = !visual || caption || !string.IsNullOrEmpty(msg.AudioFile);
                if (!visual && string.IsNullOrEmpty(body.Trim()) && string.IsNullOrEmpty(msg.AudioFile))
                    showBubble = false;

                var row = new GameObject(mine ? "Me" : "Them", typeof(RectTransform));
                row.transform.SetParent(_threadContent, false);
                PhoneUi.AddHorizontal(row, 4f);
                var h = row.GetComponent<HorizontalLayoutGroup>();
                h.padding = _selecting
                    ? new RectOffset(4, 4, 2, 2)
                    : new RectOffset(mine ? 36 : 8, mine ? 8 : 36, 2, 2);
                h.childAlignment = mine ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
                h.childForceExpandWidth = false;
                h.childForceExpandHeight = false;
                h.childControlWidth = true;
                h.childControlHeight = true;

                if (_selecting)
                {
                    bool on = _picked.Contains(msg.Id);
                    PhoneUi.CreateButton(row.transform, on ? "✓" : "○", () =>
                    {
                        if (on)
                            _picked.Remove(msg.Id);
                        else
                            _picked.Add(msg.Id);
                        FillThread();
                    }, new Vector2(28f, 28f));
                }

                var col = new GameObject("Col", typeof(RectTransform));
                col.transform.SetParent(row.transform, false);
                PhoneUi.AddVertical(col, 4f, new RectOffset(0, 0, 0, 0));
                var cv = col.GetComponent<VerticalLayoutGroup>();
                cv.childAlignment = mine ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
                cv.childForceExpandWidth = false;
                cv.childForceExpandHeight = false;
                cv.childControlWidth = true;
                cv.childControlHeight = true;
                PhoneUi.FitVertical(col);

                float mediaW = Mathf.Clamp(PhoneUi.ContentWidth() - (_selecting ? 80f : 48f), 160f, 320f);
                if (visual)
                    AddMediaCard(col.transform, msg, mediaW);

                if (!showBubble)
                    return;

                float bubbleW = visual ? Mathf.Min(mediaW, 248f) : (_selecting ? 188f : 248f);
                float innerW = bubbleW - 20f;
                var bubble = PhoneUi.CreateImage(col.transform, "B", PhoneUi.Rounded(16), mine ? PhoneUi.CallGreen : PhoneUi.SurfaceAlt);
                var ble = bubble.gameObject.AddComponent<LayoutElement>();
                ble.preferredWidth = bubbleW;
                ble.minWidth = 64f;
                ble.flexibleWidth = 0f;
                var v = PhoneUi.AddVertical(bubble.gameObject, 4f, new RectOffset(10, 10, 8, 8));
                v.childAlignment = mine ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
                v.childForceExpandWidth = false;
                v.childForceExpandHeight = false;
                v.childControlWidth = true;
                v.childControlHeight = true;
                var fitter = PhoneUi.FitVertical(bubble.gameObject);
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

                string text = visual ? body : (string.IsNullOrEmpty(body) ? " " : body);
                if (PhoneEmoji.HasEmoji(text))
                    PhoneEmoji.DrawMessage(bubble, text, innerW, 14f, mine ? Color.white : PhoneUi.Text, mine ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft);
                else
                {
                    var label = PhoneUi.CreateLabel(bubble, "Text", text, 14f, FontStyles.Normal, mine ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft);
                    PhoneUi.Wrap(label);
                    label.overflowMode = TextOverflowModes.Overflow;
                    var lle = label.gameObject.GetComponent<LayoutElement>() ?? label.gameObject.AddComponent<LayoutElement>();
                    lle.preferredWidth = innerW;
                    lle.minWidth = 48f;
                    lle.flexibleWidth = 0f;
                }

                if (_selecting)
                {
                    string id = msg.Id;
                    var pick = bubble.gameObject.AddComponent<Button>();
                    pick.onClick.AddListener(() =>
                    {
                        if (_picked.Contains(id))
                            _picked.Remove(id);
                        else
                            _picked.Add(id);
                        FillThread();
                    });
                }
                else if (!string.IsNullOrEmpty(msg.AudioFile))
                {
                    string file = msg.AudioFile;
                    var btn = bubble.gameObject.AddComponent<Button>();
                    btn.onClick.AddListener(() => VoiceIo.PlayFile(file));
                }
            }

            private static bool HasMediaCaption(ChatMessage msg, string body)
            {
                if (msg == null || string.IsNullOrEmpty(body))
                    return false;
                string t = body.Trim();
                if (t.Length == 0)
                    return false;
                if (string.Equals(t, "Photo", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(t, "Video", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(t, "GIF", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(t, "Audio", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(t, "Video (preview)", StringComparison.OrdinalIgnoreCase))
                    return false;
                return true;
            }

            private void AddMediaCard(Transform parent, ChatMessage msg, float width)
            {
                string path = PhoneStore.MediaPath(msg.MediaFile);
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return;
                bool video = msg.MediaKind == "vid" || PhoneVideo.IsVideoPath(path);
                PhoneGif.Clip gif = null;
                if (msg.MediaKind == "gif" || path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                    gif = PhoneGif.DecodeFile(path, 80);
                Texture2D tex = gif != null && gif.Frames != null && gif.Frames.Length > 0
                    ? gif.Frames[0]
                    : PhoneImages.LoadFile(path);
                float w = width;
                float h = 180f;
                if (tex != null)
                {
                    float aspect = tex.height > 0 ? tex.width / (float)tex.height : 1f;
                    h = w / aspect;
                    if (h > 280f)
                    {
                        h = 280f;
                        w = h * aspect;
                    }
                    if (h < 72f)
                        h = 72f;
                }

                var wrap = PhoneUi.CreateImage(parent, "Media", PhoneUi.Rounded(18), Color.white);
                var wrapImg = wrap.GetComponent<Image>();
                wrapImg.type = Image.Type.Sliced;
                var mask = wrap.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                var le = wrap.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = w;
                le.minWidth = w;
                le.preferredHeight = h;
                le.minHeight = h;
                le.flexibleWidth = 0f;
                le.flexibleHeight = 0f;

                if (tex != null)
                {
                    var rawGo = new GameObject("Raw", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                    rawGo.transform.SetParent(wrap, false);
                    PhoneUi.Stretch(rawGo.GetComponent<RectTransform>(), 0f, 0f);
                    var raw = rawGo.GetComponent<RawImage>();
                    raw.texture = tex;
                    raw.color = Color.white;
                    raw.raycastTarget = false;
                    if (gif != null && gif.Frames != null && gif.Frames.Length > 1)
                    {
                        var play = wrap.gameObject.AddComponent<PhoneGifPlay>();
                        play.Target = raw;
                        play.Clip = gif;
                        play.Viewport = _threadScroll != null ? _threadScroll.viewport : null;
                    }
                }
                else
                {
                    wrapImg.color = new Color(0.12f, 0.13f, 0.16f, 1f);
                    var lab = PhoneUi.CreateLabel(wrap, "A", video ? "▶  Video" : "Media", 14f, FontStyles.Normal, TextAlignmentOptions.Center);
                    PhoneUi.Stretch(lab.rectTransform, 8f, 8f);
                }
                if (video && tex != null)
                {
                    var play = PhoneUi.CreateLabel(wrap, "Play", "▶", 22f, FontStyles.Normal, TextAlignmentOptions.Center);
                    play.color = new Color(1f, 1f, 1f, 0.92f);
                    PhoneUi.Stretch(play.rectTransform, 0f, 0f);
                    play.raycastTarget = false;
                }

                ChatMessage captured = msg;
                var btn = wrap.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = wrapImg;
                if (_selecting)
                {
                    string id = msg.Id;
                    btn.onClick.AddListener(() =>
                    {
                        if (_picked.Contains(id))
                            _picked.Remove(id);
                        else
                            _picked.Add(id);
                        FillThread();
                    });
                }
                else
                    btn.onClick.AddListener(() => ShowMedia(captured));
            }

            private void ShowMedia(ChatMessage msg)
            {
                if (msg == null)
                    return;
                _listPage.SetActive(false);
                _threadPage.SetActive(false);
                _pickPage.SetActive(true);
                if (_mediaBar != null)
                    _mediaBar.SetActive(false);
                Clear(_pickPage.transform);
                _host.SetTitle(string.IsNullOrEmpty(msg.Text) ? "Media" : msg.Text);
                PhoneUi.MaterialChip(_pickPage.transform, "arrow_back", "Back", ShowThreadUi, new Vector2(36f, 32f));
                string path = PhoneStore.MediaPath(msg.MediaFile);
                bool audio = msg.MediaKind == "aud" || IsAudioPath(path);
                bool video = msg.MediaKind == "vid" || PhoneVideo.IsVideoPath(path);
                var wrap = new GameObject("ViewWrap", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                wrap.transform.SetParent(_pickPage.transform, false);
                wrap.GetComponent<Image>().color = new Color(0f, 0f, 0f, 1f);
                var wrapLe = wrap.AddComponent<LayoutElement>();
                wrapLe.flexibleHeight = 1f;
                wrapLe.minHeight = 180f;
                var preview = new GameObject("View", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                preview.transform.SetParent(wrap.transform, false);
                PhoneUi.Stretch(preview.GetComponent<RectTransform>(), 8f, 8f);
                var raw = preview.GetComponent<RawImage>();
                raw.color = Color.white;
                if (!audio && !string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    Texture2D tex = PhoneImages.LoadFile(path);
                    if (tex != null)
                    {
                        raw.texture = tex;
                        PhoneUi.FitContained(raw, tex);
                    }
                }
                if (audio)
                {
                    PhoneUi.MaterialChip(_pickPage.transform, "play", "Play", () =>
                    {
                        if (!string.IsNullOrEmpty(msg.AudioFile))
                            VoiceIo.PlayFile(msg.AudioFile);
                        else if (!string.IsNullOrEmpty(path))
                            _host.StartHostCoroutine(PhoneSounds.LoadAndPlay(path));
                    }, new Vector2(36f, 32f));
                    PhoneUi.CreateButton(_pickPage.transform, "Save as song", () =>
                    {
                        if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        {
                            SoundItem item = PhoneStore.AddSound(path, false, 0f);
                            _host.ShowToast(item != null ? "Saved to Audio." : "Couldn't save that.");
                        }
                    }, new Vector2(36f, 32f));
                    PhoneUi.CreateButton(_pickPage.transform, "Save as ringtone", () =>
                    {
                        if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        {
                            SoundItem item = PhoneStore.AddSound(path, true, 20f);
                            _host.ShowToast(item != null ? "Saved as alert." : "Couldn't save that.");
                        }
                    }, new Vector2(220f, 40f));
                }
                else if (video && !string.IsNullOrEmpty(path))
                {
                    _host.StartHostCoroutine(PhoneVideo.PlayFile(preview, raw, path));
                }
                else if (!string.IsNullOrEmpty(path) && path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                {
                    PhoneGif.Clip clip = PhoneGif.Decode(File.ReadAllBytes(path), 80);
                    if (clip != null && clip.Frames != null && clip.Frames.Length > 1)
                        _host.StartHostCoroutine(PlayGif(clip, raw));
                }
                if (!string.IsNullOrEmpty(path) && File.Exists(path) && msg.MediaKind == "gif")
                {
                    string gifPath = path;
                    PhoneUi.MaterialChip(_pickPage.transform, "save", "Save", () =>
                    {
                        PhoneStore.AddDownload(File.ReadAllBytes(gifPath), Path.GetExtension(gifPath), string.Empty);
                        _host.ShowToast("Saved to Files.");
                    }, new Vector2(36f, 32f));
                }
                else if (!string.IsNullOrEmpty(path) && File.Exists(path) && (msg.MediaKind == "img" || msg.MediaKind == "vid"))
                {
                    PhoneUi.MaterialChip(_pickPage.transform, "save", "Save", () =>
                    {
                        _host.ShowToast("Already in Files (Gallery).");
                    }, new Vector2(36f, 32f));
                }
            }

            private static IEnumerator PlayGif(PhoneGif.Clip clip, RawImage raw)
            {
                int i = 0;
                while (raw != null && clip != null && clip.Frames != null && clip.Frames.Length > 0)
                {
                    if (clip.Frames[i] != null)
                    {
                        raw.texture = clip.Frames[i];
                        PhoneUi.FitContained(raw, clip.Frames[i]);
                    }
                    float wait = clip.Delays != null && i < clip.Delays.Length ? clip.Delays[i] : 0.1f;
                    if (wait < 0.04f)
                        wait = 0.04f;
                    i++;
                    if (i >= clip.Frames.Length)
                        i = 0;
                    yield return new WaitForSecondsRealtime(wait);
                }
            }

            private static bool IsAudioPath(string path)
            {
                if (string.IsNullOrEmpty(path))
                    return false;
                string e = Path.GetExtension(path);
                return string.Equals(e, ".wav", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(e, ".mp3", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(e, ".ogg", StringComparison.OrdinalIgnoreCase);
            }

            private static string BreakLong(string s)
            {
                if (string.IsNullOrEmpty(s))
                    return string.Empty;
                var sb = new StringBuilder(s.Length + 8);
                int run = 0;
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    if (char.IsWhiteSpace(c))
                    {
                        run = 0;
                        sb.Append(c);
                    }
                    else
                    {
                        if (char.IsSurrogate(c))
                        {
                            sb.Append(c);
                            continue;
                        }
                        if (run >= 18)
                        {
                            sb.Append('\u200B');
                            run = 0;
                        }
                        sb.Append(c);
                        run++;
                    }
                }
                return sb.ToString();
            }

            private static int ActorOf(string id)
            {
                PhotonPlayer p = BuiltinApps.FindById(id);
                return p != null ? p.ActorNumber : 0;
            }

            private static void Clear(Transform t)
            {
                if (t == null)
                    return;
                for (int i = t.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
            }

            private sealed class GifHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
            {
                public RawImage Raw;
                public Texture Still;
                public GiphyHit Hit;
                private bool _over;
                private Coroutine _play;

                public void OnPointerEnter(PointerEventData eventData)
                {
                    _over = true;
                    if (_play != null)
                        StopCoroutine(_play);
                    _play = StartCoroutine(Preview());
                }

                public void OnPointerExit(PointerEventData eventData)
                {
                    _over = false;
                    if (_play != null)
                    {
                        StopCoroutine(_play);
                        _play = null;
                    }
                    if (Raw != null && Still != null)
                        Raw.texture = Still;
                }

                private IEnumerator Preview()
                {
                    if (Hit == null || Raw == null)
                        yield break;
                    string url = !string.IsNullOrEmpty(Hit.Url) ? Hit.Url : Hit.ThumbUrl;
                    PhoneGif.Clip clip = null;
                    if (!string.IsNullOrEmpty(url) && _gifCache.TryGetValue(url, out clip) && clip != null)
                    {
                    }
                    else
                    {
                        byte[] data = null;
                        yield return PhoneImages.Download(url, (b, e, er) => { data = b; });
                        if (!_over || Raw == null)
                            yield break;
                        if (data != null && PhoneGif.IsGif(data))
                        {
                            clip = PhoneGif.Decode(data, 24);
                            if (clip != null && !string.IsNullOrEmpty(url))
                                _gifCache[url] = clip;
                        }
                    }
                    if (!_over || clip == null || clip.Frames == null || clip.Frames.Length == 0 || Raw == null)
                        yield break;
                    int i = 0;
                    while (_over && Raw != null)
                    {
                        if (clip.Frames[i] != null)
                            Raw.texture = clip.Frames[i];
                        float wait = clip.Delays != null && i < clip.Delays.Length ? clip.Delays[i] : 0.08f;
                        if (wait < 0.04f)
                            wait = 0.04f;
                        i++;
                        if (i >= clip.Frames.Length)
                            i = 0;
                        yield return new WaitForSecondsRealtime(wait);
                    }
                    if (Raw != null && Still != null)
                        Raw.texture = Still;
                }
            }

            private static readonly Dictionary<string, PhoneGif.Clip> _gifCache = new Dictionary<string, PhoneGif.Clip>();
        }
    }

    [DefaultExecutionOrder(32000)]
    internal sealed class ComposerCaretGuard : MonoBehaviour
    {
        public Action AfterInput;
        public Action CopyPlain;
        private bool _copy;

        private void Update()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
            if (ctrl && (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.X)))
                _copy = true;
        }

        private void LateUpdate()
        {
            if (AfterInput != null)
                AfterInput();
            if (!_copy)
                return;
            _copy = false;
            if (CopyPlain != null)
                CopyPlain();
        }
    }
}
