using TMPro;
using UnityEngine;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// Text-message emoji. Material icons are not emoji. These are the system color set
    /// (Segoe UI Emoji on Windows), stored as normal characters in the message.
    /// </summary>
    internal static class PhoneEmoji
    {
        internal static readonly string[] Common =
        {
            "😀", "😁", "😂", "🤣", "😊", "😍", "😘", "😜", "🤔", "😎",
            "😅", "😭", "😡", "👍", "👎", "👏", "🙏", "🙌", "👀", "💀",
            "👻", "🔥", "❤️", "⭐", "🎉", "✅", "❌", "💯", "🎵", "📷",
            "📞", "💬", "🎁", "🏆", "🌲", "⛰️", "☀️", "🌙", "❄️", "⚡",
            "🐻", "💤"
        };

        private static TMP_FontAsset _font;
        private static bool _tried;

        public static bool HasEmoji(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsSurrogate(c))
                    return true;
                if (c == '❤' || c == '⭐' || c == '☀' || c == '❄' || c == '⚡')
                    return true;
            }
            return false;
        }

        public static void Apply(TMP_Text text)
        {
            if (text == null)
                return;
            TMP_FontAsset font = EmojiFont();
            if (font == null)
                return;
            text.font = font;
            text.fontStyle = FontStyles.Normal;
        }

        private static TMP_FontAsset EmojiFont()
        {
            if (_tried)
                return _font;
            _tried = true;
            try
            {
                Font os = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI Emoji", "Segoe UI Symbol", "Segoe UI" }, 64);
                if (os == null)
                    return null;
                _font = TMP_FontAsset.CreateFontAsset(os);
                if (_font != null)
                {
                    _font.name = "PiP_Emoji";
                    _font.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            catch
            {
                _font = null;
            }
            return _font;
        }
    }
}
