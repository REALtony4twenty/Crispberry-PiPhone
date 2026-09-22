using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    internal static class VoicemailApp
    {
        private static Session _live;

        internal static void Register()
        {
            PiPhoneApi.RegisterApp(new PiPhoneApp
            {
                Id = BuiltinApps.VoicemailId,
                DisplayName = "Voicemail",
                IconGlyph = "V",
                IconBackground = PhoneUi.VoicemailIcon,
                SortOrder = 20,
                ShowOnDock = true,
                OnOpen = host => { _live = new Session(host); _live.Build(); },
                OnClose = () => { VoiceIo.StopPlay(); _live = null; },
                OnOrientation = () => { if (_live != null) _live.Build(); }
            });
        }

        internal static void RefreshIfOpen()
        {
            if (_live != null)
                _live.Build();
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
                    Object.Destroy(_host.Content.GetChild(i).gameObject);

                _host.SetTitle("Voicemail");
                RectTransform listCol;
                RectTransform extra;
                PhoneUi.SplitIfLandscape(_host.Content, out listCol, out extra);

                var hint = PhoneUi.CreateLabel(extra, "Hint", "Voicemails stay on this phone until you delete them.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 32f);

                ScrollRect scroll = PhoneUi.CreateScrollView(listCol, out RectTransform content);
                var le = scroll.gameObject.AddComponent<LayoutElement>();
                le.flexibleHeight = 1f;
                PhoneUi.AddVertical(content.gameObject, 6f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(content.gameObject);

                List<ChatMessage> list = PhoneStore.Voicemails();
                if (list.Count == 0)
                {
                    var empty = PhoneUi.CreateLabel(content, "Empty", "No voicemails.", 16f, FontStyles.Normal, TextAlignmentOptions.Center);
                    empty.color = PhoneUi.TextDim;
                    PhoneUi.Size(empty.gameObject, 40f);
                }
                else
                {
                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        ChatMessage msg = list[i];
                        ChatMessage captured = msg;
                        var row = new GameObject("Vm", typeof(RectTransform));
                        row.transform.SetParent(content, false);
                        PhoneUi.Size(row, 44f);
                        PhoneUi.AddHorizontal(row, 6f);
                        PhoneUi.CreateButton(row.transform, captured.FromName ?? "Scout", () => TogglePlay(captured.AudioFile), new Vector2(160f, 40f));
                        PhoneUi.CreateIconChip(row.transform, "Stop", PhoneIcons.Material("stop"), VoiceIo.StopPlay, false, new Vector2(40f, 40f));
                        PhoneUi.CreateIconChip(row.transform, "Delete", PhoneIcons.Material("delete"), () =>
                        {
                            VoiceIo.StopPlay();
                            PhoneStore.DeleteVoicemail(captured.Id);
                            _host.ShowToast("Moved to Trash.");
                            Build();
                        }, false, new Vector2(40f, 40f));
                    }
                }

            }

            private static void TogglePlay(string file)
            {
                if (VoiceIo.IsPlaying())
                    VoiceIo.StopPlay();
                else
                    VoiceIo.PlayFile(file);
            }
        }
    }
}
