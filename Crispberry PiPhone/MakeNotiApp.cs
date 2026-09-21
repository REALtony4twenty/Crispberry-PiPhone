using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Crispberry_PiPhone
{
    internal static class MakeNotiApp
    {
        private static Session _live;

        internal static void Register()
        {
            PiPhoneApi.RegisterApp(new PiPhoneApp
            {
                Id = BuiltinApps.MakeNotiId,
                DisplayName = "MakeNoti",
                IconGlyph = "N",
                IconBackground = new Color(0.92f, 0.52f, 0.18f, 1f),
                SortOrder = 56,
                ShowOnHome = true,
                Preinstalled = true,
                OnOpen = host => { _live = new Session(host); _live.ShowHome(); },
                OnClose = () => { _live = null; },
                OnBack = TryGoBack,
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
            private string _page = "home";
            private string _editPath;
            private string _editName;
            private float _start;
            private float _end = 20f;
            private float _clipLen = 20f;
            private bool _ringtone = true;
            private DualRangeSlider _range;
            private TextMeshProUGUI _times;
            private TMP_InputField _nameInput;
            private Button _previewBtn;
            private int _previewGen;
            private bool _syncing;
            private const float MinGap = 0.4f;

            public Session(IPiPhoneHost host)
            {
                _host = host;
            }

            public bool GoBack()
            {
                if (_page == "home")
                    return false;
                if (_page == "edit")
                {
                    ShowPick();
                    return true;
                }
                ShowHome();
                return true;
            }

            public void Relayout()
            {
                if (_page == "alert")
                    ShowHome();
                else if (_page == "pick")
                    ShowPick();
                else if (_page == "edit" && !string.IsNullOrEmpty(_editPath))
                    DrawEditor();
                else
                    ShowHome();
            }

            public void ShowHome()
            {
                _page = "home";
                Clear();
                _host.SetTitle("MakeNoti");
                var hint = PhoneUi.CreateLabel(_host.Content, "Hint", "Trim a clip into a ringtone (20s) or text / alert (10s). Drop files in the alerts folder too.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 48f);
                PhoneUi.CreateButton(_host.Content, "Add alert file", AddAlert, new Vector2(240f, 40f));
                PhoneUi.CreateButton(_host.Content, "Trim a file", PickFile, new Vector2(240f, 40f));
                if (PhoneStore.MusicTracks().Count > 0)
                    PhoneUi.CreateButton(_host.Content, "Trim from library", ShowPick, new Vector2(240f, 40f));
                FillAlerts();
            }

            private void FillAlerts()
            {
                List<SoundItem> tones = PhoneStore.AlertTones();
                if (tones.Count == 0)
                {
                    Empty("No notification sounds yet.");
                    return;
                }
                ScrollRect scroll = PhoneUi.CreateScrollView(_host.Content, out RectTransform content);
                scroll.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
                PhoneUi.AddVertical(content.gameObject, 6f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(content.gameObject);
                for (int i = 0; i < tones.Count; i++)
                {
                    SoundItem s = tones[i];
                    if (s == null)
                        continue;
                    SoundItem captured = s;
                    var row = new GameObject("A", typeof(RectTransform));
                    row.transform.SetParent(content, false);
                    PhoneUi.Size(row, 40f);
                    PhoneUi.AddHorizontal(row, 6f);
                    PhoneUi.CreateButton(row.transform, captured.Name, () => ShowAlert(captured), new Vector2(200f, 36f));
                    PhoneUi.CreateButton(row.transform, "Delete", () =>
                    {
                        PhoneStore.DeleteSound(captured.Id);
                        ShowHome();
                    }, new Vector2(56f, 36f));
                }
            }

            private void ShowAlert(SoundItem sound)
            {
                _page = "alert";
                Clear();
                _host.SetTitle(sound.Name);
                PhoneUi.CreateButton(_host.Content, "Back", ShowHome, new Vector2(120f, 36f));
                PhoneUi.CreateButton(_host.Content, "Play", () => Preview(sound), new Vector2(220f, 40f));
                PhoneUi.CreateButton(_host.Content, "Use as ringtone", () => { PhoneTheme.SetTone("ringtone", sound.Id); _host.ShowToast("Ringtone set."); }, new Vector2(240f, 40f));
                PhoneUi.CreateButton(_host.Content, "Use as text tone", () => { PhoneTheme.SetTone("text", sound.Id); _host.ShowToast("Text tone set."); }, new Vector2(240f, 40f));
                PhoneUi.CreateButton(_host.Content, "Use as notification", () => { PhoneTheme.SetTone("notify", sound.Id); _host.ShowToast("Alert sound set."); }, new Vector2(240f, 40f));
                PhoneUi.CreateButton(_host.Content, "Delete", () =>
                {
                    PhoneStore.DeleteSound(sound.Id);
                    ShowHome();
                }, new Vector2(160f, 40f));
            }

            private void ShowPick()
            {
                _page = "pick";
                Clear();
                _host.SetTitle("Trim");
                PhoneUi.CreateButton(_host.Content, "Back", ShowHome, new Vector2(120f, 36f));
                var hint = PhoneUi.CreateLabel(_host.Content, "Hint", "Pick a library song, then keep a short clip.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 36f);
                List<SoundItem> tracks = PhoneStore.MusicTracks();
                if (tracks.Count == 0)
                {
                    Empty("No library songs. Use Trim a file.");
                    return;
                }
                ScrollRect scroll = PhoneUi.CreateScrollView(_host.Content, out RectTransform content);
                scroll.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
                PhoneUi.AddVertical(content.gameObject, 6f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(content.gameObject);
                for (int i = 0; i < tracks.Count; i++)
                {
                    SoundItem s = tracks[i];
                    if (s == null)
                        continue;
                    SoundItem captured = s;
                    PhoneUi.CreateButton(content, captured.Name, () => ShowEditor(PhoneStore.SoundPath(captured.File), captured.Name), new Vector2(280f, 36f));
                }
            }

            private void PickFile()
            {
                _host.StartHostCoroutine(PickFileRoutine());
            }

            private IEnumerator PickFileRoutine()
            {
                _host.ShowToast("Pick a file. Stay in the lobby.");
                string path = null;
                yield return PhoneSounds.PickAudioFile(p => path = p);
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    _host.ShowToast("No file selected.");
                    yield break;
                }
                ShowEditor(path, Path.GetFileNameWithoutExtension(path));
            }

            private void ShowEditor(string path, string name)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    _host.ShowToast("Missing file.");
                    return;
                }
                _page = "edit";
                _editPath = path;
                _editName = string.IsNullOrEmpty(name) ? "Clip" : name;
                _start = 0f;
                _end = 20f;
                _clipLen = 0f;
                Clear();
                _host.SetTitle("Trim  " + _editName);
                var wait = PhoneUi.CreateLabel(_host.Content, "Len", "Loading length...", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Size(wait.gameObject, 22f);
                _host.StartHostCoroutine(MeasureThenDraw());
            }

            private IEnumerator MeasureThenDraw()
            {
                object clip = null;
                yield return PhoneSounds.LoadClip(_editPath, c => clip = c);
                _clipLen = VoiceIo.ClipSeconds(clip);
                if (_clipLen < MinGap)
                    _clipLen = MinGap;
                ApplyKindDefault();
                DrawEditor();
            }

            private void DrawEditor()
            {
                if (string.IsNullOrEmpty(_editPath))
                    return;
                Clear();
                _host.SetTitle("Trim  " + _editName);
                _range = null;
                _times = PhoneUi.CreateLabel(_host.Content, "Times", TimesText(), 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Wrap(_times);
                PhoneUi.Size(_times.gameObject, 40f);
                _nameInput = PhoneUi.CreateInput(_host.Content, "Name this clip", 48);
                _nameInput.text = string.IsNullOrEmpty(_editName) ? "Clip" : _editName;
                if (PhoneUi.Landscape)
                    DrawEditorLand();
                else
                    DrawEditorPort();
            }

            private float WindowSeconds()
            {
                return _ringtone ? 20f : 10f;
            }

            private void DrawEditorPort()
            {
                var body = new GameObject("TrimBody", typeof(RectTransform));
                body.transform.SetParent(_host.Content, false);
                var bodyLe = body.AddComponent<LayoutElement>();
                bodyLe.flexibleHeight = 1f;
                bodyLe.minHeight = 260f;
                var bodyH = PhoneUi.AddHorizontal(body, 10f);
                bodyH.padding = new RectOffset(4, 4, 4, 4);
                bodyH.childAlignment = TextAnchor.MiddleCenter;
                bodyH.childForceExpandWidth = false;
                bodyH.childForceExpandHeight = true;

                var slideCol = new GameObject("SlideCol", typeof(RectTransform));
                slideCol.transform.SetParent(body.transform, false);
                var slideLe = slideCol.AddComponent<LayoutElement>();
                slideLe.minWidth = 52f;
                slideLe.preferredWidth = 56f;
                slideLe.flexibleWidth = 0f;
                slideLe.flexibleHeight = 1f;
                var slideV = PhoneUi.AddVertical(slideCol, 4f, new RectOffset(0, 0, 0, 0));
                slideV.childAlignment = TextAnchor.MiddleCenter;
                slideV.childForceExpandHeight = false;
                var endLab = PhoneUi.CreateLabel(slideCol.transform, "End", "END", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Size(endLab.gameObject, 18f);
                AddRange(slideCol.transform, true);
                var startLab = PhoneUi.CreateLabel(slideCol.transform, "Start", "START", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Size(startLab.gameObject, 18f);

                var optCol = new GameObject("Opts", typeof(RectTransform));
                optCol.transform.SetParent(body.transform, false);
                var optLe = optCol.AddComponent<LayoutElement>();
                optLe.flexibleWidth = 1f;
                optLe.flexibleHeight = 1f;
                var optV = PhoneUi.AddVertical(optCol, 10f, new RectOffset(8, 4, 8, 4));
                optV.childAlignment = TextAnchor.UpperCenter;
                optV.childForceExpandHeight = false;
                AddKindChecks(optCol.transform);
                var spacer = new GameObject("Spacer", typeof(RectTransform));
                spacer.transform.SetParent(optCol.transform, false);
                var spLe = spacer.AddComponent<LayoutElement>();
                spLe.flexibleHeight = 1f;
                spLe.minHeight = 8f;
                AddPreviewSave(optCol.transform, 200f);
            }

            private void DrawEditorLand()
            {
                var band = new GameObject("RangeRow", typeof(RectTransform));
                band.transform.SetParent(_host.Content, false);
                var bandLe = band.AddComponent<LayoutElement>();
                bandLe.flexibleHeight = 1f;
                bandLe.minHeight = 56f;
                bandLe.preferredHeight = 72f;
                var bh = PhoneUi.AddHorizontal(band, 8f);
                bh.padding = new RectOffset(4, 4, 8, 8);
                bh.childAlignment = TextAnchor.MiddleCenter;
                bh.childForceExpandWidth = false;
                bh.childForceExpandHeight = true;
                var startLab = PhoneUi.CreateLabel(band.transform, "Start", "START", 13f, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
                RangeLabelSize(startLab.gameObject);
                AddRange(band.transform, false);
                var endLab = PhoneUi.CreateLabel(band.transform, "End", "END", 13f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                RangeLabelSize(endLab.gameObject);

                var checks = new GameObject("Checks", typeof(RectTransform));
                checks.transform.SetParent(_host.Content, false);
                PhoneUi.Size(checks, 40f);
                var ch = PhoneUi.AddHorizontal(checks, 16f);
                ch.childAlignment = TextAnchor.MiddleCenter;
                ch.childForceExpandWidth = false;
                AddKindChecks(checks.transform);

                var btns = new GameObject("Btns", typeof(RectTransform));
                btns.transform.SetParent(_host.Content, false);
                PhoneUi.Size(btns, 48f);
                var bt = PhoneUi.AddHorizontal(btns, 10f);
                bt.childAlignment = TextAnchor.MiddleCenter;
                bt.childForceExpandWidth = false;
                AddPreviewSave(btns.transform, 180f);
            }

            private void AddRange(Transform parent, bool vertical)
            {
                _range = PhoneUi.CreateDualRange(
                    parent,
                    vertical,
                    0f,
                    Mathf.Max(MinGap, _clipLen),
                    _start,
                    _end,
                    OnStartDrag,
                    OnEndDrag);
            }

            private static void RangeLabelSize(GameObject go)
            {
                var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
                le.minWidth = 52f;
                le.preferredWidth = 56f;
                le.flexibleWidth = 0f;
                le.minHeight = 18f;
                le.preferredHeight = 22f;
                le.flexibleHeight = 0f;
            }

            private void AddKindChecks(Transform parent)
            {
                PhoneUi.CreateCheckRow(parent, "RINGTONE", _ringtone, () => SetKind(true));
                PhoneUi.CreateCheckRow(parent, "ALERT", !_ringtone, () => SetKind(false));
            }

            private void AddPreviewSave(Transform parent, float width)
            {
                _previewBtn = PhoneUi.CreateButton(parent, "PREVIEW", TogglePreview, new Vector2(width, 40f));
                PhoneUi.CreateButton(parent, "SAVE", () => _host.StartHostCoroutine(SaveTrim(WindowSeconds(), _ringtone ? "ringtone" : "notify")), new Vector2(width, 40f));
            }

            private void SetKind(bool ringtone)
            {
                _ringtone = ringtone;
                ApplyKindDefault();
                DrawEditor();
            }

            private void ApplyKindDefault()
            {
                _start = 0f;
                _end = Mathf.Min(WindowSeconds(), _clipLen);
                if (_end < _start + MinGap)
                    _end = Mathf.Min(_clipLen, _start + MinGap);
            }

            private void OnStartDrag(float v)
            {
                if (_syncing)
                    return;
                float maxGap = WindowSeconds();
                _start = Mathf.Clamp(v, 0f, Mathf.Max(0f, _clipLen - MinGap));
                if (_end < _start + MinGap)
                    _end = Mathf.Min(_clipLen, _start + MinGap);
                if (_end - _start > maxGap)
                    _end = _start + maxGap;
                if (_end > _clipLen)
                {
                    _end = _clipLen;
                    if (_end - _start > maxGap)
                        _start = Mathf.Max(0f, _end - maxGap);
                }
                SyncSliders();
            }

            private void OnEndDrag(float v)
            {
                if (_syncing)
                    return;
                float maxGap = WindowSeconds();
                _end = Mathf.Clamp(v, MinGap, _clipLen);
                if (_end < _start + MinGap)
                    _start = Mathf.Max(0f, _end - MinGap);
                if (_end - _start > maxGap)
                    _start = Mathf.Max(0f, _end - maxGap);
                SyncSliders();
            }

            private void SyncSliders()
            {
                _syncing = true;
                if (_range != null)
                    _range.Set(_start, _end);
                if (_times != null)
                    _times.text = TimesText();
                _syncing = false;
            }

            private string TimesText()
            {
                float clip = Mathf.Max(0f, _end - _start);
                return "File  " + _clipLen.ToString("0.0") + "s   ·   Clip  " + clip.ToString("0.0") + "s  (max " + WindowSeconds().ToString("0") + "s)\nStart  " + _start.ToString("0.0") + "s   ·   End  " + _end.ToString("0.0") + "s";
            }

            private void TogglePreview()
            {
                if (VoiceIo.IsPlaying())
                {
                    _previewGen++;
                    VoiceIo.StopPlay();
                    SetPreviewLabel(false);
                    return;
                }
                SetPreviewLabel(true);
                _host.StartHostCoroutine(PreviewTrim());
            }

            private void SetPreviewLabel(bool playing)
            {
                if (_previewBtn == null)
                    return;
                TextMeshProUGUI tmp = _previewBtn.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null)
                    tmp.text = playing ? "STOP" : "PREVIEW";
            }

            private IEnumerator PreviewTrim()
            {
                int gen = ++_previewGen;
                object clip = null;
                yield return PhoneSounds.LoadClip(_editPath, c => clip = c);
                if (gen != _previewGen)
                    yield break;
                object sliced = VoiceIo.Slice(clip, _start, ClampEnd(_end, WindowSeconds()));
                if (sliced == null || gen != _previewGen)
                {
                    SetPreviewLabel(false);
                    yield break;
                }
                VoiceIo.Play(sliced);
                while (gen == _previewGen && VoiceIo.IsPlaying())
                    yield return null;
                if (gen == _previewGen)
                    SetPreviewLabel(false);
            }

            private IEnumerator SaveTrim(float max, string kind)
            {
                object clip = null;
                yield return PhoneSounds.LoadClip(_editPath, c => clip = c);
                object sliced = VoiceIo.Slice(clip, _start, ClampEnd(_end, max));
                if (sliced == null)
                {
                    _host.ShowToast("Couldn't trim that.");
                    yield break;
                }
                string clipName = _nameInput != null ? _nameInput.text.Trim() : _editName;
                if (string.IsNullOrEmpty(clipName))
                    clipName = "Clip";
                SoundItem saved = PhoneStore.AddAlertWav(VoiceIo.ToWav(sliced), clipName);
                if (saved != null)
                {
                    PhoneTheme.SetTone(kind, saved.Id);
                    _host.ShowToast("Saved to MakeNoti.");
                    ShowHome();
                }
            }

            private float ClampEnd(float end, float max)
            {
                float e = Mathf.Max(_start + 0.4f, end);
                if (e - _start > max)
                    e = _start + max;
                if (e > _clipLen)
                    e = _clipLen;
                return e;
            }

            private void AddAlert()
            {
                _host.StartHostCoroutine(AddAlertRoutine());
            }

            private IEnumerator AddAlertRoutine()
            {
                _host.ShowToast("Pick a file. Stay in the lobby.");
                string path = null;
                yield return PhoneSounds.PickAudioFile(p => path = p);
                if (string.IsNullOrEmpty(path))
                {
                    _host.ShowToast("No file selected.");
                    yield break;
                }
                SoundItem item = PhoneStore.AddSound(path, true, 20f);
                _host.ShowToast(item != null ? "Added " + item.Name + "." : "Couldn't add that file.");
                ShowHome();
            }

            private void Preview(SoundItem sound)
            {
                string path = PhoneStore.SoundPath(sound.File);
                if (!string.IsNullOrEmpty(path))
                    _host.StartHostCoroutine(PhoneSounds.LoadAndPlay(path));
            }

            private void Empty(string text)
            {
                var empty = PhoneUi.CreateLabel(_host.Content, "Empty", text, 15f, FontStyles.Normal, TextAlignmentOptions.Center);
                empty.color = PhoneUi.TextDim;
                PhoneUi.Size(empty.gameObject, 32f);
            }

            private void Clear()
            {
                for (int i = _host.Content.childCount - 1; i >= 0; i--)
                    Object.Destroy(_host.Content.GetChild(i).gameObject);
            }
        }
    }
}
