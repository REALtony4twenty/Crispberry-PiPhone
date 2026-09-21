using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// Bottom-of-screen incoming call/text strip. Does not open the phone or steal look.
    /// Answer/decline (or open/dismiss text) use the plugin keybinds.
    /// </summary>
    internal sealed class AlertHud : MonoBehaviour
    {
        private static AlertHud _instance;

        private RectTransform _banner;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _sub;
        private TextMeshProUGUI _keys;
        private string _textId;
        private string _textName;
        private string _textBody;
        private bool _call;

        public static void Ensure()
        {
            if (_instance != null)
                return;
            var go = new GameObject("PiP_AlertHud");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AlertHud>();
            _instance.Build();
            go.SetActive(true);
        }

        public static bool HasBanner
        {
            get { return _instance != null && _instance._banner != null && _instance._banner.gameObject.activeSelf; }
        }

        public static void ShowCall()
        {
            Ensure();
            _instance._call = true;
            _instance._textId = string.Empty;
            _instance.Paint();
        }

        public static void ShowOnCall()
        {
            Ensure();
            _instance._call = true;
            _instance._textId = "__active";
            _instance.Paint();
        }

        public static void ShowText(string fromId, string fromName, string preview)
        {
            Ensure();
            if (CallService.State == CallService.Phase.Incoming)
                return;
            _instance._call = false;
            _instance._textId = fromId ?? string.Empty;
            _instance._textName = fromName ?? "Scout";
            _instance._textBody = preview ?? string.Empty;
            _instance.Paint();
        }

        public static void Hide()
        {
            if (_instance == null || _instance._banner == null)
                return;
            _instance._call = false;
            _instance._textId = string.Empty;
            _instance._banner.gameObject.SetActive(false);
        }

        public static void Sync()
        {
            Ensure();
            if (CallService.State == CallService.Phase.Incoming)
            {
                if (!PhoneMenu.IsOpen)
                    ShowCall();
                else
                    Hide();
                return;
            }
            if (CallService.IsOnCall && !PhoneMenu.IsOpen)
            {
                ShowOnCall();
                return;
            }
            if (_instance._call)
                Hide();
        }

        public static void HotkeyAnswer()
        {
            if (CallService.State == CallService.Phase.Incoming)
            {
                if (!PhoneMenu.CanUsePhone())
                    return;
                CallService.Accept();
                Hide();
                return;
            }
            if (HasBanner && _instance != null && !_instance._call)
            {
                _instance.Accept();
                return;
            }
            string id = PhoneNotify.LastTextThreadId;
            string name = PhoneNotify.LastTextName;
            if (string.IsNullOrEmpty(id))
                return;
            PhoneNotify.ClearLastText();
            Hide();
            MessagesApp.OpenConversation(id, name);
        }

        public static void HotkeyDecline()
        {
            if (CallService.IsOnCall)
            {
                CallService.HangUp();
                Hide();
                return;
            }
            if (CallService.State == CallService.Phase.Incoming)
            {
                if (!PhoneMenu.CanUsePhone())
                    return;
                CallService.Reject();
                Hide();
                return;
            }
            PhoneNotify.ClearLastText();
            if (HasBanner)
                Hide();
        }

        private void Build()
        {
            PhoneUi.CreateOverlayCanvas(gameObject, 27950);
            _banner = PhoneUi.CreateImage(transform, "Banner", PhoneUi.Rounded(18), new Color(0.08f, 0.09f, 0.11f, 0.96f));
            _banner.anchorMin = new Vector2(0.5f, 0f);
            _banner.anchorMax = new Vector2(0.5f, 0f);
            _banner.pivot = new Vector2(0.5f, 0f);
            _banner.anchoredPosition = new Vector2(0f, 28f);
            _banner.sizeDelta = new Vector2(440f, 168f);
            PhoneUi.AddVertical(_banner.gameObject, 6f, new RectOffset(14, 14, 10, 10));
            _title = PhoneUi.CreateLabel(_banner, "T", string.Empty, 16f, FontStyles.Normal, TextAlignmentOptions.Center);
            PhoneUi.Size(_title.gameObject, 22f);
            _sub = PhoneUi.CreateLabel(_banner, "S", string.Empty, 13f, FontStyles.Normal, TextAlignmentOptions.Center);
            _sub.color = PhoneUi.TextDim;
            PhoneUi.Size(_sub.gameObject, 20f);
            _keys = PhoneUi.CreateLabel(_banner, "K", string.Empty, 12f, FontStyles.Normal, TextAlignmentOptions.Center);
            _keys.color = PhoneUi.TextDim;
            PhoneUi.Size(_keys.gameObject, 18f);

            var actions = new GameObject("A", typeof(RectTransform));
            actions.transform.SetParent(_banner, false);
            PhoneUi.Size(actions, 36f);
            PhoneUi.AddHorizontal(actions, 8f);
            var ah = actions.GetComponent<HorizontalLayoutGroup>();
            ah.childForceExpandWidth = false;
            PhoneUi.CreateButton(actions.transform, "Yes", Accept, new Vector2(88f, 32f));
            PhoneUi.CreateButton(actions.transform, "No", Dismiss, new Vector2(88f, 32f));

            var tools = new GameObject("Q", typeof(RectTransform));
            tools.transform.SetParent(_banner, false);
            PhoneUi.Size(tools, 36f);
            PhoneUi.AddHorizontal(tools, 6f);
            var th = tools.GetComponent<HorizontalLayoutGroup>();
            th.childForceExpandWidth = false;
            PhoneUi.CreateButton(tools.transform, "DND", () =>
            {
                PhoneTheme.SetDoNotDisturb(!PhoneTheme.DoNotDisturb);
                Paint();
            }, new Vector2(72f, 30f));
            PhoneUi.CreateButton(tools.transform, "Ringer", () =>
            {
                PhoneTheme.CycleRinger();
                Paint();
            }, new Vector2(80f, 30f));
            PhoneUi.CreateButton(tools.transform, MusicPlayer.Playing ? "❚❚" : "▶", MusicPlayer.Toggle, new Vector2(44f, 30f));
            PhoneUi.CreateButton(tools.transform, "►|", MusicPlayer.Next, new Vector2(44f, 30f));

            _banner.gameObject.SetActive(false);
        }

        private void Paint()
        {
            if (_banner == null)
                return;
            if (_call)
            {
                if (_textId == "__active")
                {
                    _title.text = CallService.RemoteName ?? "On a call";
                    _sub.text = CallService.Muted ? "Muted · tap Hang up" : "On a call · look away from the phone to walk";
                    _keys.text = PhoneKeys.Format(PhoneKeys.Decline) + " hang up";
                }
                else
                {
                    _title.text = CallService.RemoteName ?? "Incoming call";
                    _sub.text = CallService.Group ? "Incoming group call" : "Incoming call";
                    _keys.text = Plugin.FormatAlertKeys() + (PhoneTheme.DoNotDisturb ? "  ·  DND" : string.Empty);
                }
            }
            else
            {
                _title.text = _textName ?? "Message";
                _sub.text = string.IsNullOrEmpty(_textBody) ? "New message" : _textBody;
                _keys.text = Plugin.FormatAlertKeys(true) + (PhoneTheme.DoNotDisturb ? "  ·  DND" : string.Empty);
            }
            _banner.gameObject.SetActive(true);
        }

        private void Accept()
        {
            if (_call)
            {
                if (_textId == "__active")
                {
                    PhoneMenu.Open();
                    Hide();
                    return;
                }
                CallService.Accept();
                Hide();
                return;
            }
            string id = _textId;
            string name = _textName;
            PhoneNotify.ClearLastText();
            Hide();
            if (!string.IsNullOrEmpty(id))
                MessagesApp.OpenConversation(id, name);
        }

        private void Dismiss()
        {
            if (_call)
            {
                if (_textId == "__active" || CallService.IsOnCall)
                    CallService.HangUp();
                else
                    CallService.Reject();
            }
            else
                PhoneNotify.ClearLastText();
            Hide();
        }
    }
}
