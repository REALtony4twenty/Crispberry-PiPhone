using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
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
                OnOpen = host => { _live = new Session(host); _live.Build(); },
                OnClose = () =>
                {
                    PhoneVideo.StopAll();
                    VoiceIo.StopPlay();
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
            private bool _emojiOpen;
            private TMP_InputField _composer;
            private TextMeshProUGUI _micLabel;
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

            public bool GoBack()
            {
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
                PhoneUi.CreateButton(top.transform, "<", ShowList, new Vector2(40f, 32f));
                PhoneUi.CreateButton(top.transform, "♪", ShowContactTones, new Vector2(36f, 32f));
                PhoneUi.CreateButton(top.transform, "Select", ToggleSelect, new Vector2(72f, 32f));
                PhoneUi.CreateButton(top.transform, "Del chat", () =>
                {
                    PhoneStore.DeleteThread(_threadId);
                    _host.ShowToast("Conversation deleted.");
                    ShowList();
                }, new Vector2(88f, 32f));

                _threadScroll = PhoneUi.CreateScrollView(_threadPage.transform, out _threadContent);
                var tLe = _threadScroll.gameObject.AddComponent<LayoutElement>();
                tLe.flexibleHeight = 1f;
                PhoneUi.AddVertical(_threadContent.gameObject, 6f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(_threadContent.gameObject);

                _mediaBar = new GameObject("Media", typeof(RectTransform));
                _mediaBar.transform.SetParent(_threadPage.transform, false);
                PhoneUi.Size(_mediaBar, 44f);
                PhoneUi.AddHorizontal(_mediaBar, 8f);
                var mh = _mediaBar.GetComponent<HorizontalLayoutGroup>();
                mh.childAlignment = TextAnchor.MiddleCenter;
                mh.childForceExpandWidth = false;
                PhoneUi.CreateButton(_mediaBar.transform, "▣", ShowAttach, new Vector2(56f, 36f));
                PhoneUi.CreateButton(_mediaBar.transform, "GIF", ShowGifSearch, new Vector2(56f, 36f));
                var mic = PhoneUi.CreateButton(_mediaBar.transform, "◉", ToggleVoice, new Vector2(56f, 36f));
                _micLabel = mic.GetComponentInChildren<TextMeshProUGUI>(true);
                _mediaBar.SetActive(false);

                var compose = PhoneUi.CreateImage(_threadPage.transform, "Compose", PhoneUi.White(), new Color(1f, 1f, 1f, 0f));
                _composeRt = compose;
                PhoneUi.Size(compose.gameObject, 56f);
                PhoneUi.AddHorizontal(compose.gameObject, 6f);
                var ch = compose.GetComponent<HorizontalLayoutGroup>();
                ch.childAlignment = TextAnchor.LowerCenter;
                ch.padding = new RectOffset(0, 0, 4, 4);
                PhoneUi.CreateButton(compose, "+", ToggleMedia, new Vector2(36f, 36f));
                PhoneUi.CreateButton(compose, "☺", ToggleEmoji, new Vector2(36f, 36f));
                _composer = PhoneUi.CreateInput(compose, "Message", 800);
                _composer.lineType = TMP_InputField.LineType.MultiLineNewline;
                PhoneUi.Wrap(_composer.textComponent as TextMeshProUGUI);
                PhoneUi.Wrap(_composer.placeholder as TextMeshProUGUI);
                PhoneEmoji.Apply(_composer.textComponent);
                _composer.onValueChanged.AddListener(v => ResizeComposer());
                PhoneUi.CreateButton(compose, "➤", SendText, new Vector2(36f, 36f));

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
                    PhoneUi.CreateButton(row.transform, "Del", () =>
                    {
                        PhoneStore.DeleteThread(id);
                        FillList();
                    }, new Vector2(56f, 40f));
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
                if (!string.IsNullOrEmpty(name))
                    PhoneStore.DismissNotices(BuiltinApps.MessagesId, name);
                ShowThreadUi();
                FillThread();
            }

            private void ToggleMedia()
            {
                if (_mediaBar == null)
                    return;
                _mediaBar.SetActive(!_mediaBar.activeSelf);
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
                PhoneUi.CreateButton(_pickPage.transform, "Back", ShowThreadUi, new Vector2(120f, 32f));
                var hint = PhoneUi.CreateLabel(_pickPage.transform, "H", "Empty = Settings default.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Size(hint.gameObject, 22f);
                PhoneUi.CreateButton(_pickPage.transform, "Ringtone  " + PhoneTones.Label(PhoneTones.ContactRing(_threadId)), () => FillContactTones(true), new Vector2(280f, 40f));
                PhoneUi.CreateButton(_pickPage.transform, "Text tone  " + PhoneTones.Label(PhoneTones.ContactText(_threadId)), () => FillContactTones(false), new Vector2(280f, 40f));
            }

            private void FillContactTones(bool ring)
            {
                Clear(_pickPage.transform);
                PhoneUi.CreateButton(_pickPage.transform, "Back", ShowContactTones, new Vector2(120f, 32f));
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
                    PhoneUi.CreateButton(bar.transform, "All", () =>
                    {
                        List<ChatMessage> all = PhoneStore.Thread(_threadId);
                        _picked.Clear();
                        for (int i = 0; i < all.Count; i++)
                            _picked.Add(all[i].Id);
                        FillThread();
                    }, new Vector2(64f, 32f));
                    PhoneUi.CreateButton(bar.transform, "Delete", () =>
                    {
                        var ids = new List<string>(_picked);
                        PhoneStore.DeleteMessages(_threadId, ids);
                        _selecting = false;
                        _picked.Clear();
                        FillThread();
                    }, new Vector2(80f, 32f));
                    PhoneUi.CreateButton(bar.transform, "Cancel", ToggleSelect, new Vector2(72f, 32f));
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

            private void ResizeComposer()
            {
                if (_composer == null)
                    return;
                TextMeshProUGUI tmp = _composer.textComponent as TextMeshProUGUI;
                if (tmp != null)
                {
                    PhoneUi.Wrap(tmp);
                    tmp.ForceMeshUpdate();
                }
                float h = 48f;
                if (tmp != null)
                    h = Mathf.Clamp(tmp.preferredHeight + 18f, 48f, 140f);
                var le = _composer.GetComponent<LayoutElement>();
                if (le != null)
                {
                    le.minHeight = h;
                    le.preferredHeight = h;
                }
                if (_composeRt != null)
                    PhoneUi.Size(_composeRt.gameObject, h + 10f);
            }

            private void SendText()
            {
                if (_composer == null || string.IsNullOrEmpty(_threadId))
                    return;
                string text = _composer.text;
                if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
                    return;
                text = text.Trim();
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
                _composer.text = string.Empty;
                ResizeComposer();
                if (_threadActor > 0)
                    PhoneNet.SendText(_threadActor, msg);
                else
                    _host.ShowToast("Saved locally. They're not in the lobby.");
                FillThread();
            }

            private void ToggleEmoji()
            {
                _emojiOpen = !_emojiOpen;
                if (_emojiPanel == null)
                    BuildEmojiPanel();
                if (_emojiPanel != null)
                    _emojiPanel.SetActive(_emojiOpen);
            }

            private void BuildEmojiPanel()
            {
                if (_threadPage == null)
                    return;
                _emojiPanel = new GameObject("Emoji", typeof(RectTransform));
                _emojiPanel.transform.SetParent(_threadPage.transform, false);
                if (_composeRt != null)
                    _emojiPanel.transform.SetSiblingIndex(_composeRt.transform.GetSiblingIndex());
                PhoneUi.Size(_emojiPanel, 148f);
                ScrollRect scroll = PhoneUi.CreateScrollView(_emojiPanel.transform, out RectTransform content);
                var le = scroll.gameObject.AddComponent<LayoutElement>();
                le.flexibleHeight = 1f;
                le.minHeight = 120f;
                PhoneUi.ApplyMediaGrid(content);
                string[] faces = PhoneEmoji.Common;
                for (int i = 0; i < faces.Length; i++)
                {
                    string face = faces[i];
                    Button btn = PhoneUi.CreateButton(content, face, () => InsertEmoji(face), new Vector2(40f, 36f));
                    PhoneEmoji.Apply(btn.GetComponentInChildren<TextMeshProUGUI>(true));
                }
            }

            private void InsertEmoji(string face)
            {
                if (_composer == null || string.IsNullOrEmpty(face))
                    return;
                string cur = _composer.text ?? string.Empty;
                int at = Mathf.Clamp(_composer.caretPosition, 0, cur.Length);
                _composer.text = cur.Insert(at, face);
                _composer.caretPosition = at + face.Length;
                PhoneEmoji.Apply(_composer.textComponent);
                ResizeComposer();
            }

            private void ToggleVoice()
            {
                if (_recording)
                {
                    _recording = false;
                    if (_micLabel != null)
                        _micLabel.text = "◉";
                    object clip = VoiceIo.StopRecord();
                    if (clip == null)
                    {
                        _host.ShowToast("No audio. Talk or hold PTT while you record.");
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
                    if (_threadActor > 0)
                        PhoneNet.SendVoice(_threadActor, msg, wav);
                    FillThread();
                    _host.ShowToast("Voice message saved.");
                    return;
                }
                if (!VoiceIo.StartRecord())
                {
                    _host.ShowToast("Microphone unavailable.");
                    return;
                }
                _recording = true;
                if (_micLabel != null)
                    _micLabel.text = "■";
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
                PhoneUi.CreateButton(_pickPage.transform, "Back", ShowThreadUi, new Vector2(120f, 32f));
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
                PhoneUi.CreateButton(_pickPage.transform, "Back", ShowAttach, new Vector2(120f, 32f));
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
                PhoneUi.CreateButton(_pickPage.transform, "Back", ShowThreadUi, new Vector2(120f, 32f));

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
                if (_threadActor > 0)
                    PhoneNet.SendMedia(_threadActor, msg, data, kind);
                else
                    _host.ShowToast("Saved locally. They're not in the lobby.");
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
                var label = PhoneUi.CreateLabel(bubble, "Text", text, 14f, FontStyles.Normal, mine ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft);
                PhoneUi.Wrap(label);
                label.overflowMode = TextOverflowModes.Overflow;
                var lle = label.gameObject.GetComponent<LayoutElement>() ?? label.gameObject.AddComponent<LayoutElement>();
                lle.preferredWidth = innerW;
                lle.minWidth = 48f;
                lle.flexibleWidth = 0f;
                label.ForceMeshUpdate();
                if (PhoneEmoji.HasEmoji(text))
                    PhoneEmoji.Apply(label);

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
                PhoneUi.CreateButton(_pickPage.transform, "Back", ShowThreadUi, new Vector2(120f, 32f));
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
                    PhoneUi.CreateButton(_pickPage.transform, "Play", () =>
                    {
                        if (!string.IsNullOrEmpty(msg.AudioFile))
                            VoiceIo.PlayFile(msg.AudioFile);
                        else if (!string.IsNullOrEmpty(path))
                            _host.StartHostCoroutine(PhoneSounds.LoadAndPlay(path));
                    }, new Vector2(160f, 40f));
                    PhoneUi.CreateButton(_pickPage.transform, "Save as song", () =>
                    {
                        if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        {
                            SoundItem item = PhoneStore.AddSound(path, false, 0f);
                            _host.ShowToast(item != null ? "Saved to Audio." : "Couldn't save that.");
                        }
                    }, new Vector2(200f, 40f));
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
                    PhoneUi.CreateButton(_pickPage.transform, "Save to Gallery", () =>
                    {
                        PhoneStore.AddDownload(File.ReadAllBytes(gifPath), Path.GetExtension(gifPath), string.Empty);
                        _host.ShowToast("Saved to Files.");
                    }, new Vector2(200f, 40f));
                }
                else if (!string.IsNullOrEmpty(path) && File.Exists(path) && (msg.MediaKind == "img" || msg.MediaKind == "vid"))
                {
                    PhoneUi.CreateButton(_pickPage.transform, "Save to Gallery", () =>
                    {
                        _host.ShowToast("Already in Files (Gallery).");
                    }, new Vector2(200f, 40f));
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
}
