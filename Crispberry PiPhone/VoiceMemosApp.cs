using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    internal static class VoiceMemosApp
    {
        private static Session _live;

        internal static void Register()
        {
            PiPhoneApi.RegisterApp(new PiPhoneApp
            {
                Id = BuiltinApps.VoiceMemosId,
                DisplayName = "Voice Memos",
                IconGlyph = "M",
                IconBackground = PhoneUi.MemosIcon,
                SortOrder = 22,
                ShowOnHome = true,
                OnOpen = host => { _live = new Session(host); _live.Build(); },
                OnClose = () => { VoiceIo.StopPlay(); _live = null; },
                OnOrientation = () => { if (_live != null) _live.Build(); }
            });
        }

        private sealed class Session
        {
            private readonly IPiPhoneHost _host;

            public Session(IPiPhoneHost host)
            {
                _host = host;
            }

            public void Build()
            {
                for (int i = _host.Content.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(_host.Content.GetChild(i).gameObject);

                _host.SetTitle("Voice Memos");
                RectTransform listCol;
                RectTransform actions;
                PhoneUi.SplitIfLandscape(_host.Content, out listCol, out actions);

                var hint = PhoneUi.CreateLabel(listCol, "Hint", "Play or delete memos. They stay on this phone.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 32f);

                ScrollRect scroll = PhoneUi.CreateScrollView(listCol, out RectTransform content);
                var le = scroll.gameObject.AddComponent<LayoutElement>();
                le.flexibleHeight = 1f;
                PhoneUi.AddVertical(content.gameObject, 6f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(content.gameObject);

                if (PhoneStore.Memos.Count == 0)
                {
                    var empty = PhoneUi.CreateLabel(content, "Empty", "No memos yet.", 16f, FontStyles.Normal, TextAlignmentOptions.Center);
                    empty.color = PhoneUi.TextDim;
                    PhoneUi.Size(empty.gameObject, 40f);
                }
                else
                {
                    for (int i = 0; i < PhoneStore.Memos.Count; i++)
                    {
                        VoiceMemoItem memo = PhoneStore.Memos[i];
                        if (memo == null)
                            continue;
                        string file = memo.File;
                        string id = memo.Id;
                        var row = new GameObject("Memo", typeof(RectTransform));
                        row.transform.SetParent(content, false);
                        PhoneUi.Size(row, 44f);
                        PhoneUi.AddHorizontal(row, 6f);
                        PhoneUi.CreateButton(row.transform, "Memo " + (PhoneStore.Memos.Count - i), () => TogglePlay(file), new Vector2(160f, 40f));
                        PhoneUi.CreateButton(row.transform, "Stop", VoiceIo.StopPlay, new Vector2(56f, 40f));
                        PhoneUi.CreateButton(row.transform, "Del", () =>
                        {
                            VoiceIo.StopPlay();
                            PhoneStore.DeleteMemo(id);
                            _host.ShowToast("Moved to Trash.");
                            Build();
                        }, new Vector2(56f, 40f));
                    }
                }

                PhoneUi.CreateButton(actions, VoiceIo.IsRecording ? "Stop recording" : "Record memo", ToggleMemo, new Vector2(240f, 44f));
            }

            private static void TogglePlay(string file)
            {
                if (VoiceIo.IsPlaying())
                    VoiceIo.StopPlay();
                else
                    VoiceIo.PlayFile(file);
            }

            private void ToggleMemo()
            {
                if (VoiceIo.IsRecording)
                {
                    object clip = VoiceIo.StopRecord();
                    if (clip == null)
                    {
                        _host.ShowToast("No audio. Talk or hold PTT while you record.");
                        Build();
                        return;
                    }
                    PhoneStore.AddMemo(VoiceIo.ToWav(clip));
                    _host.ShowToast("Memo saved.");
                    Build();
                    return;
                }
                if (!VoiceIo.StartRecord())
                    _host.ShowToast("Microphone unavailable.");
                else
                    _host.ShowToast("Recording...");
                Build();
            }
        }
    }
}
