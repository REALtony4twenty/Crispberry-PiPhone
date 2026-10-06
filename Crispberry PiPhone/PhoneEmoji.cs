using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Drawing = System.Drawing;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// Message emoji as pictures. Text is stored as normal characters.
    /// The picker pictures are Twemoji (CC-BY 4.0), in Icons/emoji.
    /// </summary>
    internal static class PhoneEmoji
    {
        private static readonly Dictionary<string, string> SeqToName = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> NameToSeq = new Dictionary<string, string>();
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, string> ResourceByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<string>> ByPrefix = new Dictionary<string, List<string>>();
        private static readonly List<string> Order = new List<string>();
        private static TMP_SpriteAsset _asset;
        private static bool _ready;
        private static int _maxLen = 1;
        private static bool _logged;
        private static int _warmed;
        private static bool _warmDone;
        private static bool _rush;

        public const int WarmPage = 32;

        public static bool WarmDone
        {
            get { return _warmDone; }
        }

        public static bool Rushing
        {
            get { return _rush; }
        }

        public static void Rush()
        {
            _rush = true;
        }

        public static bool WarmLoopStarted
        {
            get { return _warmLoop; }
        }

        private static bool _warmLoop;

        public static IEnumerator WarmOverTime()
        {
            _warmLoop = true;
            Ensure();
            while (!_warmDone)
            {
                WarmSome(2);
                if (_warmDone)
                    yield break;
                yield return new WaitForSecondsRealtime(0.1f);
            }
        }

        public static void WarmSome(int count)
        {
            Ensure();
            if (_warmDone || Order.Count == 0)
            {
                _warmDone = true;
                return;
            }
            if (count < 1)
                count = 1;
            int end = _warmed + count;
            if (end > Order.Count)
                end = Order.Count;
            for (int i = _warmed; i < end; i++)
                SpriteFor(Sequence(Order[i]));
            _warmed = end;
            if (_warmed >= Order.Count)
                _warmDone = true;
        }

        public static void WarmOnePage()
        {
            WarmSome(WarmPage);
        }

        public static IList<string> Names
        {
            get
            {
                Ensure();
                return Order;
            }
        }

        public static bool HasEmoji(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            if (text.IndexOf("<sprite", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            Ensure();
            for (int i = 0; i < text.Length; i++)
            {
                if (MatchAt(text, i) > 0)
                    return true;
                if (char.IsSurrogate(text[i]))
                    return true;
            }
            return false;
        }

        public static Sprite SpriteFor(string sequence)
        {
            Ensure();
            string name;
            if (string.IsNullOrEmpty(sequence) || !SeqToName.TryGetValue(sequence, out name))
                return null;
            Sprite sprite;
            if (Sprites.TryGetValue(name, out sprite) && sprite != null)
                return sprite;
            return LoadOne(name);
        }

        public static string Sequence(string name)
        {
            Ensure();
            string seq;
            return !string.IsNullOrEmpty(name) && NameToSeq.TryGetValue(name, out seq) ? seq : string.Empty;
        }

        public static string TagFor(string sequence)
        {
            Ensure();
            string name;
            if (string.IsNullOrEmpty(sequence) || !SeqToName.TryGetValue(sequence, out name))
                return sequence ?? string.Empty;
            return "<sprite name=\"" + name + "\">";
        }

        public static string ToDisplay(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text ?? string.Empty;
            text = ToPlain(text);
            Ensure();
            if (_asset == null || SeqToName.Count == 0)
                return text;
            var sb = new StringBuilder(text.Length + 16);
            int i = 0;
            while (i < text.Length)
            {
                int n = MatchAt(text, i);
                if (n > 0)
                {
                    sb.Append(TagFor(text.Substring(i, n)));
                    i += n;
                    continue;
                }
                sb.Append(text[i]);
                i++;
            }
            return sb.ToString();
        }

        public static string ToPlain(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text ?? string.Empty;
            Ensure();
            if (text.IndexOf("<sprite", StringComparison.OrdinalIgnoreCase) < 0)
                return text;
            var sb = new StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == '<' && i + 7 < text.Length && text.Substring(i, 7).Equals("<sprite", StringComparison.OrdinalIgnoreCase))
                {
                    int end = text.IndexOf('>', i);
                    if (end < 0)
                    {
                        sb.Append(text[i]);
                        i++;
                        continue;
                    }
                    string tag = text.Substring(i, end - i + 1);
                    string seq = SequenceFromTag(tag);
                    if (!string.IsNullOrEmpty(seq))
                        sb.Append(seq);
                    i = end + 1;
                    continue;
                }
                sb.Append(text[i]);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>One wide space in the text field for each emoji picture.</summary>
        public const char FieldMark = '\u2003';

        public static string ToField(string plain)
        {
            if (string.IsNullOrEmpty(plain))
                return string.Empty;
            Ensure();
            var sb = new StringBuilder(plain.Length);
            int i = 0;
            while (i < plain.Length)
            {
                int n = MatchAt(plain, i);
                if (n > 0)
                {
                    sb.Append(FieldMark);
                    i += n;
                    continue;
                }
                if (char.IsHighSurrogate(plain[i]) && i + 1 < plain.Length && char.IsLowSurrogate(plain[i + 1]))
                {
                    i += 2;
                    continue;
                }
                if (char.IsSurrogate(plain[i]))
                {
                    i++;
                    continue;
                }
                sb.Append(plain[i]);
                i++;
            }
            return sb.ToString();
        }

        public static int SequenceLength(string text, int index)
        {
            if (string.IsNullOrEmpty(text) || index < 0 || index >= text.Length)
                return 0;
            Ensure();
            return MatchAt(text, index);
        }

        public static int PlainIndexAtField(string plain, int fieldIndex)
        {
            if (string.IsNullOrEmpty(plain) || fieldIndex <= 0)
                return 0;
            Ensure();
            int field = 0;
            int i = 0;
            while (i < plain.Length)
            {
                if (field >= fieldIndex)
                    return i;
                int n = MatchAt(plain, i);
                if (n > 0)
                {
                    i += n;
                    field++;
                    continue;
                }
                if (char.IsHighSurrogate(plain[i]) && i + 1 < plain.Length && char.IsLowSurrogate(plain[i + 1]))
                {
                    i += 2;
                    continue;
                }
                if (char.IsSurrogate(plain[i]))
                {
                    i++;
                    continue;
                }
                i++;
                field++;
            }
            return plain.Length;
        }

        public static string ApplyFieldEdit(string plain, string beforeField, string afterField)
        {
            plain = plain ?? string.Empty;
            beforeField = beforeField ?? string.Empty;
            afterField = afterField ?? string.Empty;
            int pre = 0;
            int limit = beforeField.Length < afterField.Length ? beforeField.Length : afterField.Length;
            while (pre < limit && beforeField[pre] == afterField[pre])
                pre++;
            int b = beforeField.Length;
            int a = afterField.Length;
            while (b > pre && a > pre && beforeField[b - 1] == afterField[a - 1])
            {
                b--;
                a--;
            }
            int start = PlainIndexAtField(plain, pre);
            int end = PlainIndexAtField(plain, b);
            if (end < start)
                end = start;
            if (start > plain.Length)
                start = plain.Length;
            if (end > plain.Length)
                end = plain.Length;
            string inserted = a > pre ? afterField.Substring(pre, a - pre) : string.Empty;
            if (inserted.IndexOf(FieldMark) >= 0)
                inserted = inserted.Replace(FieldMark.ToString(), string.Empty);
            return plain.Substring(0, start) + inserted + plain.Substring(end);
        }

        private static string SequenceFromTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return string.Empty;
            int nameAt = tag.IndexOf("name=\"", StringComparison.OrdinalIgnoreCase);
            if (nameAt >= 0)
            {
                int start = nameAt + 6;
                int end = tag.IndexOf('"', start);
                if (end > start)
                {
                    string seq = Sequence(tag.Substring(start, end - start));
                    if (!string.IsNullOrEmpty(seq))
                        return seq;
                }
            }
            int eq = tag.IndexOf("sprite=\"", StringComparison.OrdinalIgnoreCase);
            if (eq >= 0)
            {
                int start = eq + 8;
                int end = tag.IndexOf('"', start);
                if (end > start)
                {
                    string seq = Sequence(tag.Substring(start, end - start));
                    if (!string.IsNullOrEmpty(seq))
                        return seq;
                }
            }
            int num = tag.IndexOf('=');
            if (num >= 0)
            {
                int start = num + 1;
                while (start < tag.Length && (tag[start] == '"' || tag[start] == ' '))
                    start++;
                int n = 0;
                bool any = false;
                while (start < tag.Length && char.IsDigit(tag[start]))
                {
                    any = true;
                    n = n * 10 + (tag[start] - '0');
                    start++;
                }
                if (any && n >= 0 && n < Order.Count)
                    return Sequence(Order[n]);
            }
            return string.Empty;
        }

        public static void DrawMessage(Transform parent, string text, float maxWidth, float fontSize, Color color, TextAlignmentOptions align)
        {
            if (parent == null)
                return;
            text = ToPlain(text ?? string.Empty);
            Ensure();
            if (maxWidth < 48f)
                maxWidth = 48f;
            var root = new GameObject("Rich", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rootRt = root.GetComponent<RectTransform>();
            bool rightAlign = align == TextAlignmentOptions.TopRight || align == TextAlignmentOptions.MidlineRight || align == TextAlignmentOptions.Right;
            rootRt.anchorMin = new Vector2(rightAlign ? 1f : 0f, 1f);
            rootRt.anchorMax = rootRt.anchorMin;
            rootRt.pivot = new Vector2(rightAlign ? 1f : 0f, 1f);
            rootRt.anchoredPosition = Vector2.zero;
            rootRt.sizeDelta = new Vector2(maxWidth, 8f);
            var rootLe = root.AddComponent<LayoutElement>();
            rootLe.preferredWidth = maxWidth;
            rootLe.flexibleWidth = 0f;
            var lines = PhoneUi.AddVertical(root, 2f, new RectOffset(0, 0, 0, 0));
            bool right = rightAlign;
            lines.childAlignment = right ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            lines.childForceExpandWidth = false;
            lines.childForceExpandHeight = false;
            lines.childControlWidth = true;
            lines.childControlHeight = true;
            GameObject line = null;
            float used = 0f;
            var run = new StringBuilder();
            float icon = fontSize + 8f;

            StartLine();
            int i = 0;
            while (i < text.Length)
            {
                int n = MatchAt(text, i);
                if (n > 0)
                {
                    FlushRun();
                    string seq = text.Substring(i, n);
                    Sprite sprite = SpriteFor(seq);
                    if (sprite != null)
                    {
                        if (used + icon > maxWidth && used > 1f)
                            StartLine();
                        var img = PhoneUi.CreateImage(line.transform, "E", sprite, Color.white);
                        var art = img.GetComponent<Image>();
                        art.preserveAspect = true;
                        art.raycastTarget = false;
                        var le = PhoneUi.Size(img.gameObject, icon, icon);
                        le.flexibleWidth = 0f;
                        le.flexibleHeight = 0f;
                        used += icon + 2f;
                    }
                    else
                        run.Append(seq);
                    i += n;
                    continue;
                }
                if (char.IsSurrogate(text[i]))
                {
                    i += i + 1 < text.Length && char.IsSurrogatePair(text, i) ? 2 : 1;
                    continue;
                }
                run.Append(text[i]);
                i++;
            }
            FlushRun();
            if (line != null && line.transform.childCount == 0)
                UnityEngine.Object.Destroy(line);
            PhoneUi.FitVertical(root);

            void StartLine()
            {
                line = new GameObject("Line", typeof(RectTransform));
                line.transform.SetParent(root.transform, false);
                PhoneUi.Size(line, icon);
                var row = PhoneUi.AddHorizontal(line, 2f);
                row.childAlignment = right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                row.childForceExpandWidth = false;
                row.childForceExpandHeight = false;
                row.childControlWidth = true;
                row.childControlHeight = true;
                used = 0f;
            }

            void FlushRun()
            {
                if (run.Length == 0 || line == null)
                    return;
                string s = run.ToString();
                run.Length = 0;
                float w = Mathf.Clamp(s.Length * fontSize * 0.55f, 12f, maxWidth);
                if (used + w > maxWidth && used > 1f)
                    StartLine();
                var lab = PhoneUi.CreateLabel(line.transform, "T", s, fontSize, FontStyles.Normal, right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft);
                lab.color = color;
                lab.raycastTarget = false;
                PhoneUi.Wrap(lab);
                var le = PhoneUi.Size(lab.gameObject, icon, w);
                le.flexibleWidth = 0f;
                le.flexibleHeight = 0f;
                used += w;
            }
        }

        public static void Bind(TMP_Text text)
        {
            if (text == null)
                return;
            Ensure();
            text.richText = true;
            if (_asset != null)
                text.spriteAsset = _asset;
        }

        private static int MatchAt(string text, int index)
        {
            if (string.IsNullOrEmpty(text) || index < 0 || index >= text.Length)
                return 0;
            string key = char.IsHighSurrogate(text[index]) && index + 1 < text.Length
                ? text.Substring(index, 2)
                : text.Substring(index, 1);
            List<string> list;
            if (!ByPrefix.TryGetValue(key, out list))
                return 0;
            for (int i = 0; i < list.Count; i++)
            {
                string seq = list[i];
                if (index + seq.Length <= text.Length && string.CompareOrdinal(text, index, seq, 0, seq.Length) == 0)
                    return seq.Length;
            }
            return 0;
        }

        public static void Ensure()
        {
            if (_ready)
                return;
            _ready = true;
            try
            {
                IndexEmbedded();
                if (Order.Count == 0)
                    BakeFont();
                SortCatalog();
            }
            catch (Exception ex)
            {
                if (!_logged)
                {
                    _logged = true;
                    Plugin.LogError("Emoji pictures failed: " + ex.Message);
                }
            }
        }

        private static void IndexEmbedded()
        {
            Assembly asm = typeof(PhoneEmoji).Assembly;
            string[] names = asm.GetManifestResourceNames();
            for (int i = 0; i < names.Length; i++)
            {
                string res = names[i];
                int at = res.IndexOf(".emoji.emoji_u", StringComparison.OrdinalIgnoreCase);
                if (at < 0 || !res.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    continue;
                string stem = res.Substring(at + ".emoji.".Length);
                stem = stem.Substring(0, stem.Length - 4);
                Remember(stem, SequenceFromName(stem), true);
                ResourceByName[stem] = res;
            }
        }

        private static Sprite LoadOne(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            string res;
            if (!ResourceByName.TryGetValue(name, out res))
                return null;
            Sprite sprite;
            try
            {
                Assembly asm = typeof(PhoneEmoji).Assembly;
                byte[] bytes;
                using (var stream = asm.GetManifestResourceStream(res))
                {
                    if (stream == null)
                        return null;
                    bytes = new byte[stream.Length];
                    stream.Read(bytes, 0, bytes.Length);
                }
                Texture2D tex = PhoneImages.LoadTexture(bytes);
                if (tex == null)
                    return null;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                tex.hideFlags = HideFlags.HideAndDontSave;
                sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                sprite.hideFlags = HideFlags.HideAndDontSave;
                sprite.name = name;
            }
            catch (Exception ex)
            {
                if (!_logged)
                {
                    _logged = true;
                    Plugin.LogError("Emoji picture failed: " + ex.Message);
                }
                return null;
            }
            Sprites[name] = sprite;
            return sprite;
        }

        private static void BakeFont()
        {
            var font = new Drawing.Font("Segoe UI Emoji", 40f, Drawing.GraphicsUnit.Pixel);
            try
            {
                var catalog = new List<string>();
                for (int cp = 0x1F600; cp <= 0x1F64F; cp++)
                    catalog.Add(char.ConvertFromUtf32(cp));
                int[] extra =
                {
                    0x1F44D, 0x1F44E, 0x1F44F, 0x1F64C, 0x1F64F, 0x2764, 0x1F495, 0x1F525,
                    0x1F389, 0x1F440, 0x1F4AF, 0x2B50, 0x2705, 0x274C, 0x26A1
                };
                for (int i = 0; i < extra.Length; i++)
                    catalog.Add(char.ConvertFromUtf32(extra[i]));
                for (int i = 0; i < catalog.Count; i++)
                {
                    string seq = catalog[i];
                    Sprite sprite = DrawOne(font, seq);
                    if (sprite == null)
                        continue;
                    AddSprite("u" + CodeName(seq), seq, sprite);
                }
            }
            finally
            {
                font.Dispose();
            }
        }

        private static Sprite DrawOne(Drawing.Font font, string seq)
        {
            var bmp = new Drawing.Bitmap(72, 72, Drawing.Imaging.PixelFormat.Format32bppArgb);
            int opaque = 0;
            try
            {
                using (Drawing.Graphics g = Drawing.Graphics.FromImage(bmp))
                {
                    g.Clear(Drawing.Color.Transparent);
                    g.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    g.DrawString(seq, font, Drawing.Brushes.White, new Drawing.RectangleF(2f, 6f, 68f, 64f));
                }
                var tex = new Texture2D(bmp.Width, bmp.Height, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                tex.hideFlags = HideFlags.HideAndDontSave;
                var px = new Color32[bmp.Width * bmp.Height];
                for (int y = 0; y < bmp.Height; y++)
                {
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        Drawing.Color c = bmp.GetPixel(x, bmp.Height - 1 - y);
                        if (c.A > 24)
                            opaque++;
                        px[y * bmp.Width + x] = new Color32(c.R, c.G, c.B, c.A);
                    }
                }
                if (opaque < 8)
                {
                    UnityEngine.Object.Destroy(tex);
                    return null;
                }
                tex.SetPixels32(px);
                tex.Apply(false, false);
                Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                sprite.hideFlags = HideFlags.HideAndDontSave;
                return sprite;
            }
            finally
            {
                bmp.Dispose();
            }
        }

        private static void AddSprite(string name, string sequence, Sprite sprite)
        {
            if (sprite == null)
                return;
            Remember(name, sequence, true);
            name = name.ToLowerInvariant();
            Sprites[name] = sprite;
        }

        private static void Remember(string name, string sequence, bool catalog)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(sequence))
                return;
            name = name.ToLowerInvariant();
            if (!SeqToName.ContainsKey(sequence))
            {
                SeqToName[sequence] = name;
                if (!NameToSeq.ContainsKey(name))
                    NameToSeq[name] = sequence;
                if (catalog)
                    Order.Add(name);
                NotePrefix(sequence);
                if (sequence.Length > _maxLen)
                    _maxLen = sequence.Length;
            }
            if (sequence.Length > 1 && sequence[sequence.Length - 1] == '\uFE0F')
                Alias(name, sequence.Substring(0, sequence.Length - 1));
            else
                Alias(name, sequence + "\uFE0F");
        }

        private static void Alias(string name, string sequence)
        {
            if (string.IsNullOrEmpty(sequence) || SeqToName.ContainsKey(sequence))
                return;
            SeqToName[sequence] = name;
            NotePrefix(sequence);
            if (sequence.Length > _maxLen)
                _maxLen = sequence.Length;
        }

        private static void NotePrefix(string sequence)
        {
            if (string.IsNullOrEmpty(sequence))
                return;
            string key = char.IsHighSurrogate(sequence[0]) && sequence.Length >= 2
                ? sequence.Substring(0, 2)
                : sequence.Substring(0, 1);
            List<string> list;
            if (!ByPrefix.TryGetValue(key, out list))
            {
                list = new List<string>();
                ByPrefix[key] = list;
            }
            list.Add(sequence);
        }

        private static void SortCatalog()
        {
            Order.Sort((a, b) =>
            {
                string sa;
                string sb;
                NameToSeq.TryGetValue(a, out sa);
                NameToSeq.TryGetValue(b, out sb);
                return string.CompareOrdinal(sa ?? string.Empty, sb ?? string.Empty);
            });
            foreach (KeyValuePair<string, List<string>> pair in ByPrefix)
            {
                pair.Value.Sort((a, b) => b.Length.CompareTo(a.Length));
            }
        }

        private static string SequenceFromName(string stem)
        {
            string[] parts = stem.Split('_');
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                string hex = parts[i];
                if (hex.StartsWith("emoji", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (hex.Length > 0 && (hex[0] == 'u' || hex[0] == 'U'))
                    hex = hex.Substring(1);
                int cp;
                if (!int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out cp) || cp <= 0)
                    continue;
                sb.Append(char.ConvertFromUtf32(cp));
            }
            return sb.ToString();
        }

        private static string CodeName(string sequence)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < sequence.Length; i++)
            {
                int cp = char.ConvertToUtf32(sequence, i);
                if (sb.Length > 0)
                    sb.Append('_');
                sb.Append(cp.ToString("x"));
                if (cp > 0xFFFF)
                    i++;
            }
            return sb.ToString();
        }

        private static void BuildAsset()
        {
            if (Sprites.Count == 0)
                return;
            int tile = 64;
            int cols = 16;
            int rows = (Order.Count + cols - 1) / cols;
            int size = Mathf.NextPowerOfTwo(Mathf.Max(cols, rows) * tile);
            var atlas = new Texture2D(size, size, TextureFormat.RGBA32, false);
            atlas.wrapMode = TextureWrapMode.Clamp;
            atlas.filterMode = FilterMode.Bilinear;
            atlas.hideFlags = HideFlags.HideAndDontSave;
            var clear = new Color32[size * size];
            atlas.SetPixels32(clear);
            _asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            _asset.name = "PiP_EmojiSprites";
            _asset.hideFlags = HideFlags.HideAndDontSave;
            _asset.hashCode = TMP_TextUtilities.GetSimpleHashCode(_asset.name);
            _asset.spriteSheet = atlas;
            MarkSpriteVersion(_asset);
            Material mat = SpriteMaterial(atlas);
            if (mat != null)
                _asset.material = mat;
            if (_asset.spriteGlyphTable == null || _asset.spriteCharacterTable == null)
                return;
            for (int i = 0; i < Order.Count; i++)
            {
                string name = Order[i];
                Sprite sprite = Sprites[name];
                int col = i % cols;
                int row = i / cols;
                int x = col * tile;
                int y = size - (row + 1) * tile;
                Blit(atlas, sprite, x, y, tile);
                var rect = new GlyphRect(x, y, tile, tile);
                var metrics = new GlyphMetrics(tile, tile, 0f, tile * 0.9f, tile);
                var glyph = new TMP_SpriteGlyph((uint)i, metrics, rect, 1f, 0, sprite);
                var character = new TMP_SpriteCharacter(0, _asset, glyph);
                character.name = name;
                character.scale = 1.1f;
                _asset.spriteGlyphTable.Add(glyph);
                _asset.spriteCharacterTable.Add(character);
            }
            atlas.Apply(false, false);
            _asset.UpdateLookupTables();
            MaterialReferenceManager.AddSpriteAsset(_asset);
        }

        private static void MarkSpriteVersion(TMP_SpriteAsset asset)
        {
            if (asset == null)
                return;
            FieldInfo field = typeof(TMP_Asset).GetField("m_Version", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(asset, "1.1.0");
        }

        private static Material SpriteMaterial(Texture2D atlas)
        {
            Material source = null;
            if (TMP_Settings.defaultSpriteAsset != null)
                source = TMP_Settings.defaultSpriteAsset.material;
            if (source == null)
            {
                TMP_SpriteAsset[] assets = Resources.FindObjectsOfTypeAll<TMP_SpriteAsset>();
                for (int i = 0; i < assets.Length; i++)
                {
                    if (assets[i] != null && assets[i].material != null)
                    {
                        source = assets[i].material;
                        break;
                    }
                }
            }
            Shader shader = source != null ? source.shader : null;
            if (shader == null)
                shader = Shader.Find("TextMeshPro/Sprite");
            if (shader == null)
                shader = Shader.Find("Hidden/TextMeshPro/Sprite");
            if (shader == null)
                return null;
            var mat = new Material(shader);
            mat.mainTexture = atlas;
            mat.hideFlags = HideFlags.HideAndDontSave;
            return mat;
        }

        private static void Blit(Texture2D atlas, Sprite sprite, int x, int y, int tile)
        {
            if (sprite == null || sprite.texture == null)
                return;
            Texture2D src = sprite.texture;
            Color[] px = src.GetPixels();
            int sw = src.width;
            int sh = src.height;
            var block = new Color[tile * tile];
            for (int dy = 0; dy < tile; dy++)
            {
                int sy = dy * sh / tile;
                for (int dx = 0; dx < tile; dx++)
                {
                    int sx = dx * sw / tile;
                    block[dy * tile + dx] = px[sy * sw + sx];
                }
            }
            atlas.SetPixels(x, y, tile, tile, block);
        }

        public static int CategoryCount
        {
            get { return 11; }
        }

        public static bool InCategory(string name, int category)
        {
            if (category < 2)
                return true;
            return Bucket(FirstCode(name)) == category;
        }

        public static bool Matches(string name, string query)
        {
            if (string.IsNullOrEmpty(query))
                return true;
            query = query.Trim();
            if (query.Length == 0)
                return true;
            if (!string.IsNullOrEmpty(name) && name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            string seq = Sequence(name);
            if (!string.IsNullOrEmpty(seq) && seq.IndexOf(query, StringComparison.Ordinal) >= 0)
                return true;
            return WordHit(name, query);
        }

        public static bool TryShortcode(string token, out string sequence)
        {
            sequence = string.Empty;
            if (string.IsNullOrEmpty(token))
                return false;
            token = token.Trim().ToLowerInvariant();
            var hits = new List<string>();
            Suggest(token, hits, 1);
            if (hits.Count == 0)
                return false;
            if (!WordEquals(hits[0], token))
                return false;
            sequence = Sequence(hits[0]);
            return !string.IsNullOrEmpty(sequence);
        }

        public static void Suggest(string prefix, List<string> into, int max)
        {
            into.Clear();
            if (string.IsNullOrEmpty(prefix) || max < 1)
                return;
            prefix = prefix.Trim().ToLowerInvariant();
            var scored = new List<KeyValuePair<int, string>>();
            IList<string> names = Names;
            for (int i = 0; i < names.Count; i++)
            {
                int rank = RankName(names[i], prefix);
                if (rank < 0)
                    continue;
                scored.Add(new KeyValuePair<int, string>(rank, names[i]));
            }
            scored.Sort(CompareRank);
            int take = Mathf.Min(max, scored.Count);
            for (int i = 0; i < take; i++)
                into.Add(scored[i].Value);
        }

        private static int CompareRank(KeyValuePair<int, string> a, KeyValuePair<int, string> b)
        {
            return a.Key.CompareTo(b.Key);
        }

        public static void RememberRecent(string sequence)
        {
            string name = NameOf(sequence);
            if (string.IsNullOrEmpty(name))
                return;
            PhoneTheme.EmojiRecent = PushName(PhoneTheme.EmojiRecent, name, 32);
            PhoneTheme.Save();
        }

        public static bool IsFavorite(string sequence)
        {
            return ListHas(PhoneTheme.EmojiFav, NameOf(sequence));
        }

        public static bool ToggleFavorite(string sequence)
        {
            string name = NameOf(sequence);
            if (string.IsNullOrEmpty(name))
                return false;
            bool on = !ListHas(PhoneTheme.EmojiFav, name);
            PhoneTheme.EmojiFav = on ? PushName(PhoneTheme.EmojiFav, name, 48) : DropName(PhoneTheme.EmojiFav, name);
            PhoneTheme.Save();
            return on;
        }

        public static List<string> RecentList()
        {
            return SplitNames(PhoneTheme.EmojiRecent);
        }

        public static List<string> FavoriteList()
        {
            return SplitNames(PhoneTheme.EmojiFav);
        }

        private static bool ListHas(string csv, string name)
        {
            if (string.IsNullOrEmpty(csv) || string.IsNullOrEmpty(name))
                return false;
            string[] parts = csv.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.Equals(parts[i], name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static List<string> SplitNames(string csv)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(csv))
                return list;
            string[] parts = csv.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrEmpty(parts[i]))
                    list.Add(parts[i]);
            }
            return list;
        }

        private static string PushName(string csv, string name, int cap)
        {
            List<string> list = SplitNames(csv);
            list.RemoveAll(delegate(string item) { return string.Equals(item, name, StringComparison.OrdinalIgnoreCase); });
            list.Insert(0, name);
            if (list.Count > cap)
                list.RemoveRange(cap, list.Count - cap);
            return string.Join(",", list.ToArray());
        }

        private static string DropName(string csv, string name)
        {
            List<string> list = SplitNames(csv);
            list.RemoveAll(delegate(string item) { return string.Equals(item, name, StringComparison.OrdinalIgnoreCase); });
            return string.Join(",", list.ToArray());
        }

        private static string NameOf(string sequence)
        {
            Ensure();
            string name;
            if (string.IsNullOrEmpty(sequence) || !SeqToName.TryGetValue(sequence, out name))
                return string.Empty;
            return name;
        }

        private static int FirstCode(string name)
        {
            string seq = Sequence(name);
            if (string.IsNullOrEmpty(seq))
                return 0;
            if (char.IsHighSurrogate(seq[0]) && seq.Length >= 2)
                return char.ConvertToUtf32(seq, 0);
            return seq[0];
        }

        private static int Bucket(int cp)
        {
            if ((cp >= 0x1F1E6 && cp <= 0x1F1FF) || cp == 0x1F3F3 || cp == 0x1F3F4)
                return 10;
            if ((cp >= 0x1F600 && cp <= 0x1F64F) || (cp >= 0x1F910 && cp <= 0x1F92F) || (cp >= 0x1F970 && cp <= 0x1F97A) || cp == 0x2639 || cp == 0x263A || cp == 0x1F479 || cp == 0x1F47A || cp == 0x1F47F)
                return 2;
            if (IsPeople(cp))
                return 3;
            if ((cp >= 0x1F400 && cp <= 0x1F43F) || (cp >= 0x1F980 && cp <= 0x1F9AE))
                return 4;
            if ((cp >= 0x1F32D && cp <= 0x1F37F) || (cp >= 0x1F950 && cp <= 0x1F96F))
                return 5;
            if ((cp >= 0x1F680 && cp <= 0x1F6FF) || (cp >= 0x1F30D && cp <= 0x1F320) || (cp >= 0x1F5FA && cp <= 0x1F5FF))
                return 6;
            if ((cp >= 0x1F3A0 && cp <= 0x1F3C9) || (cp >= 0x1F3CF && cp <= 0x1F3F0) || (cp >= 0x1F93C && cp <= 0x1F945) || (cp >= 0x1F3AF && cp <= 0x1F3B3))
                return 7;
            if ((cp >= 0x2600 && cp <= 0x27BF) || (cp >= 0x1F7E0 && cp <= 0x1F7EB) || (cp >= 0x1F4A0 && cp <= 0x1F4AF))
                return 9;
            return 8;
        }

        private static bool IsPeople(int cp)
        {
            if (cp == 0x1F977 || cp == 0x1F9B8 || cp == 0x1F9B9)
                return true;
            if (cp >= 0x1F466 && cp <= 0x1F487)
                return true;
            if (cp >= 0x1F48B && cp <= 0x1F48F)
                return true;
            if (cp >= 0x1F440 && cp <= 0x1F450)
                return true;
            if (cp >= 0x1F590 && cp <= 0x1F596)
                return true;
            if (cp >= 0x1F9B0 && cp <= 0x1F9B7)
                return true;
            if (cp >= 0x1F9D1 && cp <= 0x1F9DF)
                return true;
            if (cp >= 0x1F91A && cp <= 0x1F91F)
                return true;
            if (cp >= 0x1F932 && cp <= 0x1F93A)
                return true;
            if (cp >= 0x1FA70 && cp <= 0x1FA74)
                return true;
            if (cp >= 0x270A && cp <= 0x270D)
                return true;
            return cp == 0x1F4AA || cp == 0x1F9CE || cp == 0x1F9CD;
        }

        private static string Words(int cp)
        {
            EnsureWords();
            string extra;
            string range = RangeWords(Bucket(cp));
            if (_words.TryGetValue(cp, out extra))
                return extra + " " + range;
            return range;
        }

        private static string RangeWords(int bucket)
        {
            if (bucket == 2) return "smile face happy sad";
            if (bucket == 3) return "people person hand";
            if (bucket == 4) return "animal nature";
            if (bucket == 5) return "food drink";
            if (bucket == 6) return "travel place";
            if (bucket == 7) return "play sport game activity";
            if (bucket == 9) return "symbol sign heart";
            if (bucket == 10) return "flag";
            return "object thing";
        }

        private static Dictionary<int, string> _words;
        private static Dictionary<string, string> _nameWords;

        private static bool WordHit(string name, string query)
        {
            return ScanWords(name, query, true);
        }

        private static bool WordEquals(string name, string query)
        {
            return ScanWords(name, query, false);
        }

        private static bool ScanWords(string name, string query, bool prefix)
        {
            return RankName(name, query) >= 0 && (prefix || WordExact(name, query));
        }

        private static bool WordExact(string name, string query)
        {
            int rank = RankName(name, query);
            return rank >= 0 && rank < 200;
        }

        private static int RankName(string name, string query)
        {
            string blob = WordsForName(name);
            if (string.IsNullOrEmpty(blob) || string.IsNullOrEmpty(query))
                return -1;
            int best = -1;
            int wordIndex = 0;
            int i = 0;
            while (i < blob.Length)
            {
                while (i < blob.Length && blob[i] == ' ')
                    i++;
                int start = i;
                while (i < blob.Length && blob[i] != ' ')
                    i++;
                int len = i - start;
                if (len == 0)
                    break;
                bool starts = len >= query.Length && string.Compare(blob, start, query, 0, query.Length, StringComparison.OrdinalIgnoreCase) == 0;
                if (starts)
                {
                    bool exact = len == query.Length;
                    int codes = CodeCount(name);
                    int rank = exact
                        ? (wordIndex == 0 ? codes : 40 + codes)
                        : (wordIndex == 0 ? 200 + codes : 240 + codes);
                    if (best < 0 || rank < best)
                        best = rank;
                }
                wordIndex++;
            }
            return best;
        }

        private static int CodeCount(string name)
        {
            if (string.IsNullOrEmpty(name))
                return 9;
            int count = 0;
            string[] bits = name.Split('_');
            for (int i = 0; i < bits.Length; i++)
            {
                if (bits[i].Length > 1 && (bits[i][0] == 'u' || bits[i][0] == 'U'))
                    count++;
            }
            return count < 1 ? 9 : count;
        }

        private static string WordsForName(string name)
        {
            EnsureWords();
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            string cached;
            if (_nameWords != null && _nameWords.TryGetValue(name, out cached))
                return cached;
            if (_nameWords == null)
                _nameWords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var sb = new StringBuilder();
            string[] bits = name.ToLowerInvariant().Split('_');
            for (int i = 0; i < bits.Length; i++)
            {
                string bit = bits[i];
                if (bit.Length < 2 || bit[0] != 'u')
                    continue;
                int cp;
                if (!int.TryParse(bit.Substring(1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out cp))
                    continue;
                if ((cp >= 0x1F3FB && cp <= 0x1F3FF) || cp == 0x200D || cp == 0xFE0F || cp == 0x2640 || cp == 0x2642)
                    continue;
                string extra;
                if (_words.TryGetValue(cp, out extra))
                    sb.Append(' ').Append(extra);
                sb.Append(' ').Append(RangeWords(Bucket(cp)));
            }
            string text = sb.ToString();
            _nameWords[name] = text;
            return text;
        }

        private static void EnsureWords()
        {
            if (_words != null)
                return;
            _words = new Dictionary<int, string>();
            AddWords(KeywordTable);
            AddWords(KeywordExtra);
        }

        private static void AddWords(string table)
        {
            if (string.IsNullOrEmpty(table))
                return;
            string[] lines = table.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                int space = line.IndexOf(' ');
                if (space <= 0)
                    continue;
                int cp;
                if (!int.TryParse(line.Substring(0, space), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out cp))
                    continue;
                string words = line.Substring(space + 1);
                string have;
                if (_words.TryGetValue(cp, out have) && have.IndexOf(words, StringComparison.OrdinalIgnoreCase) < 0)
                    _words[cp] = have + " " + words;
                else if (string.IsNullOrEmpty(have))
                    _words[cp] = words;
            }
        }

        private const string KeywordExtra =
            "1f977 ninja\n1f9b8 hero superhero\n1f9b9 villain supervillain\n1f479 ogre devil oni\n1f47a goblin devil oni\n1f47f devil angry horns\n1f608 devil smile horns imp\n";

        private const string KeywordTable =
            "1f600 grin smile\n1f603 smile happy\n1f604 laugh smile\n1f601 grin\n1f605 sweat smile\n1f602 laugh cry joy\n1f923 rofl laugh\n1f60a smile blush\n1f607 angel innocent\n1f642 smile slight\n1f643 upside\n1f609 wink\n1f60c relief\n1f60d love heart eyes\n1f970 love heart face\n1f618 kiss\n1f617 kiss\n1f61a kiss\n1f60b yum\n1f61b tongue\n1f61c wink tongue\n1f92a zany\n1f61d tongue\n1f911 money\n1f917 hug\n1f929 star eyes\n1f914 think\n1f910 zip quiet\n1f928 raised eyebrow\n1f610 neutral\n1f611 expressionless\n1f636 no mouth\n1f60f smirk\n1f612 unamused\n1f644 roll eyes\n1f62c grimace\n1f925 lying\n1f60e cool sunglasses\n1f913 nerd\n1f9d0 monocle\n1f615 confused\n1f61f worry\n1f641 sad\n1f62e open mouth\n1f627 angry\n1f628 fear\n1f630 sweat\n1f625 sad\n1f622 cry\n1f62d sob cry\n1f631 scream\n1f616 confounded\n1f623 persevere\n1f61e disappointed\n1f613 sweat\n1f629 weary\n1f62b tired\n1f971 yawn\n1f624 triumph\n1f621 angry rage\n1f620 angry\n1f92c swear\n1f608 devil smile\n1f47f devil angry\n1f480 skull\n1f4a9 poop\n1f921 clown\n1f47b ghost\n1f47d alien\n1f916 robot\n1f63a cat smile\n1f638 cat grin\n1f639 cat joy\n1f63b cat heart\n1f63c cat smirk\n1f63d cat kiss\n1f640 cat scream\n1f63f cat cry\n1f63e cat angry\n1f648 see no evil\n1f649 hear no evil\n1f64a speak no evil\n1f48b kiss\n1f48c love letter\n1f498 heart cupid\n1f49d heart gift\n1f496 heart sparkle\n1f497 heart grow\n1f493 heart beat\n1f49e hearts\n1f495 hearts\n1f49f heart box\n2764 heart love\n1f494 heart broken\n1f44d thumb up yes\n1f44e thumb down no\n1f44c ok\n270c peace\n1f91e fingers cross\n1f91f love you\n1f918 rock\n1f919 call me\n1f44b wave\n1f596 vulcan\n1f590 hand\n270b hand stop\n1f44a punch\n1f91c left fist\n1f91b right fist\n1f44f clap\n1f64c raise hands\n1f450 open hands\n1f932 palms\n1f91d handshake\n1f64f pray thanks\n270d write\n1f485 nail\n1f4aa muscle strong\n1f9be bone\n1f9bf leg\n1f9b5 leg\n1f9b6 foot\n1f442 ear\n1f443 nose\n1f9e0 brain\n1f9b7 tooth\n1f9b4 bone\n1f440 eyes\n1f441 eye\n1f445 tongue\n1f444 mouth\n1f436 dog puppy\n1f431 cat\n1f42d mouse\n1f439 hamster\n1f430 rabbit\n1f98a fox\n1f43b bear\n1f43c panda\n1f428 koala\n1f42f tiger\n1f981 lion\n1f42e cow\n1f437 pig\n1f438 frog\n1f435 monkey\n1f412 monkey\n1f414 chicken\n1f427 penguin\n1f426 bird\n1f424 chick\n1f986 duck\n1f985 eagle\n1f989 owl\n1f987 bat\n1f43a wolf\n1f417 boar\n1f434 horse\n1f984 unicorn\n1f41d bee\n1f41b bug\n1f98b butterfly\n1f40c snail\n1f41e ladybug\n1f997 cricket\n1f577 spider\n1f982 scorpion\n1f422 turtle\n1f40d snake\n1f98e lizard\n1f996 t rex dinosaur\n1f995 dinosaur\n1f419 octopus\n1f41f fish\n1f420 fish\n1f42c dolphin\n1f433 whale\n1f40b whale\n1f988 shark\n1f41a shell\n1f40a crocodile\n1f409 dragon\n1f335 cactus\n1f332 tree\n1f333 tree\n1f334 palm\n1f335 cactus\n1f33f herb\n1f340 clover luck\n1f338 flower blossom\n1f33a flower\n1f33b flower sunflower\n1f33c flower\n1f337 tulip\n1f339 rose\n1f940 wilt flower\n1f344 mushroom\n1f347 grape\n1f348 melon\n1f349 watermelon\n1f34a orange\n1f34b lemon\n1f34c banana\n1f34d pineapple\n1f34e apple\n1f34f apple\n1f350 pear\n1f351 peach\n1f352 cherry\n1f353 strawberry\n1f95d kiwi\n1f345 tomato\n1f951 avocado\n1f346 eggplant\n1f954 potato\n1f955 carrot\n1f33d corn\n1f336 pepper\n1f952 cucumber\n1f96c leaf\n1f966 broccoli\n1f344 mushroom\n1f95c peanut\n1f330 chestnut\n1f35e bread\n1f950 croissant\n1f956 baguette\n1f968 pretzel\n1f96f bagel\n1f95e pancake\n1f9c0 cheese\n1f356 meat\n1f357 chicken leg\n1f969 steak\n1f953 bacon\n1f354 burger hamburger\n1f35f fries\n1f355 pizza\n1f32d hotdog\n1f96a sandwich\n1f32e taco\n1f32f burrito\n1f959 stuffed\n1f95a egg\n1f373 cooking egg\n1f958 pan\n1f372 stew\n1f963 bowl\n1f957 salad\n1f37f popcorn\n1f9c2 salt\n1f96b canned\n1f371 bento\n1f358 rice\n1f359 rice\n1f35a rice\n1f35b curry\n1f35c ramen noodle\n1f35d spaghetti\n1f360 sweet potato\n1f362 oden\n1f363 sushi\n1f364 fish cake\n1f365 moon cake\n1f96e dumpling\n1f361 dango\n1f366 icecream\n1f367 shaved ice\n1f368 icecream\n1f369 donut\n1f36a cookie\n1f382 cake birthday\n1f370 cake\n1f36b chocolate\n1f36c candy\n1f36d lollipop\n1f36e custard\n1f36f honey\n1f37c milk baby\n2615 coffee\n1f375 tea\n1f376 sake\n1f37a beer\n1f37b beer\n1f942 clink drink\n1f377 wine\n1f378 cocktail\n1f379 tropical drink\n1f37e champagne\n1f376 sake\n1f9c3 juice\n1f9c9 mate\n1f9ca ice\n1f942 cheers\n1f697 car\n1f695 taxi\n1f699 suv\n1f68c bus\n1f68e trolley\n1f3ce race car\n1f693 police car\n1f691 ambulance\n1f692 fire truck\n1f690 van\n1f69a truck\n1f69b truck\n1f69c tractor\n1f6b2 bike\n1f6f4 scooter\n1f6f5 scooter\n1f3cd motorcycle\n1f6a8 police light\n1f695 taxi\n1f68f bus stop\n1f6e3 road\n1f6e4 railway\n26fd fuel gas\n1f6a6 traffic\n1f6a5 light\n1f6a7 construction\n2693 anchor\n26f5 sail boat\n1f6a4 boat\n1f6f3 ship\n2708 plane flight\n1f6eb depart plane\n1f6ec arrive plane\n1f4ba seat\n1f681 helicopter\n1f69f suspension\n1f6a0 cable\n1f6a1 tram\n1f6f0 satellite\n1f680 rocket\n1f6f8 ufo\n1f30d earth globe\n1f30e earth\n1f30f earth\n1f5fa map\n1f5fe japan map\n1f9ed compass\n26f0 mountain\n1f3d4 mountain\n1f30b volcano\n1f3d6 beach\n1f3dc desert\n1f3dd island\n1f3de park\n1f3df stadium\n1f3db building\n1f3d7 construction\n1f3d8 house\n1f3e0 house home\n1f3e1 house\n1f3e2 office\n1f3e3 post\n1f3e4 post\n1f3e5 hospital\n1f3e6 bank\n1f3e8 hotel\n1f3e9 love hotel\n1f3ea store\n1f3eb school\n1f3ec store\n1f3ed factory\n1f3ef castle\n1f3f0 castle\n1f492 wedding\n1f5fc tower\n1f5fd statue\n26ea church\n1f54c mosque\n1f54d synagogue\n26e9 shrine\n1f54b kaaba\n26f2 fountain\n26fa tent camp\n1f301 fog\n1f303 night city\n1f3d9 city\n1f304 sunrise\n1f305 sunrise\n1f306 city dusk\n1f307 sunset\n1f309 bridge\n2668 hot springs\n1f3a0 carousel\n1f3a1 ferris\n1f3a2 roller coaster\n1f488 barber\n1f3aa circus\n1f682 train\n1f683 train\n1f684 train\n1f685 train\n1f686 train\n1f687 metro\n1f688 light rail\n1f689 station\n1f68a tram\n1f69d monorail\n1f69e mountain train\n1f3c6 trophy\n1f947 medal gold\n1f948 medal silver\n1f949 medal bronze\n1f3c5 medal\n26bd soccer football\n1f3c0 basketball\n1f3c8 football\n26be baseball\n1f94e softball\n1f3be tennis\n1f3d0 volleyball\n1f3c9 rugby\n1f94f frisbee\n1f3b1 pool billiard\n1f3d3 ping pong\n1f3f8 badminton\n1f94a boxing\n1f94b martial\n1f3af goal\n26f3 golf\n1f3f9 bow\n1f3a3 fishing\n1f94c hockey\n1f3d2 hockey\n1f3d1 field hockey\n1f94d lacrosse\n1f3cf cricket\n1f3d1 hockey\n26f8 ice skate\n1f3bf ski\n1f3c2 snowboard\n1f3cb lift weight\n1f938 cartwheel\n1f93c wrestle\n1f93a fence\n1f93e handball\n1f3cc golf\n1f3c4 surf\n1f3ca swim\n1f93d water polo\n1f6a3 row\n1f3c7 horse race\n1f6b4 bike\n1f6b5 mountain bike\n1f3bd run shirt\n1f3c5 medal\n1f947 medal\n1f3ae game controller\n1f579 joystick\n1f3b0 slot\n1f3b2 dice\n1f9e9 puzzle\n1f3a8 art paint\n1f3ac movie\n1f3a5 camera movie\n1f4fd projector\n1f3ad theater\n1f3a4 mic karaoke\n1f3a7 headphone\n1f3bc music score\n1f3b5 music note\n1f3b6 music\n1f3b9 piano\n1f3b7 sax\n1f3ba trumpet\n1f3bb violin\n1f3b8 guitar\n1f941 drum\n1f4f1 phone mobile\n1f4f2 phone\n1f4de phone\n1f4df pager\n1f4e0 fax\n1f50b battery\n1f50c plug\n1f4bb computer laptop\n1f5a5 computer\n1f5a8 printer\n2328 keyboard\n1f5b1 mouse computer\n1f5b2 trackball\n1f4bd disk\n1f4be floppy\n1f4bf cd\n1f4c0 dvd\n1f4fc tape\n1f4f7 camera photo\n1f4f8 camera flash\n1f4f9 video camera\n1f3a5 movie camera\n1f4fd film\n1f39e film\n1f4de receiver\n1f4fa tv television\n1f4f8 camera\n1f4fb radio\n1f399 mic\n1f39a slider\n1f39b knob\n1f9ed compass\n23f0 alarm clock\n231a watch\n23f1 stopwatch\n23f2 timer\n1f570 clock\n231b hourglass\n23f3 hourglass\n1f4e1 satellite\n1f50d search mag\n1f50e search\n1f56f candle\n1f4a1 bulb idea light\n1f526 flashlight\n1f3ee lantern\n1f4d4 notebook\n1f4d5 book\n1f4d6 book\n1f4d7 book\n1f4d8 book\n1f4d9 book\n1f4da book\n1f4d3 notebook\n1f4d2 ledger\n1f4c3 page\n1f4dc scroll\n1f4c4 page\n1f4f0 newspaper\n1f5de newspaper\n1f4d1 bookmark\n1f516 bookmark\n1f3f7 label\n1f4b0 money\n1f4b4 yen\n1f4b5 dollar money\n1f4b6 euro money\n1f4b7 pound money\n1f4b8 money wings\n1f4b3 credit card\n1f9fe receipt\n1f4b9 chart money\n2709 mail envelope\n1f4e7 email\n1f4e8 email\n1f4e9 email\n1f4e4 outbox\n1f4e5 inbox\n1f4e6 package\n1f4eb mailbox\n1f4ea mailbox\n1f4ec mailbox\n1f4ed mailbox\n1f4ee postbox\n1f5f3 ballot\n270f pencil\n2712 pen\n1f58b pen\n1f58a pen\n1f58c paint\n1f58d crayon\n1f4dd memo note\n1f4bc briefcase\n1f4c1 folder\n1f4c2 folder\n1f5c2 card\n1f5d2 calendar\n1f5d3 calendar\n1f4c5 calendar\n1f4c6 calendar\n1f5d1 trash\n1f512 lock\n1f513 unlock\n1f50f lock\n1f510 lock\n1f511 key\n1f5dd key\n1f528 hammer\n26cf pick\n2692 hammer\n1f6e0 tools\n1f5e1 dagger\n2694 swords\n1f52b gun\n1f3f9 bow\n1f6e1 shield\n1f527 wrench\n1f529 nut bolt\n2699 gear\n1f5dc clamp\n2696 scale\n1f517 link\n26d3 chain\n1f9f0 toolbox\n1f9f1 magnet\n1f9f2 magnet\n1f9ea test tube\n1f9eb petri\n1f9ec dna\n1f52c microscope\n1f52d telescope\n1f4e1 satellite\n1f489 syringe\n1f48a pill\n1f6aa door\n1f6cf bed\n1f6cb couch\n1f6bd toilet\n1f6bf shower\n1f6c1 bath\n1f6c0 bath\n1f9f9 broom\n1f9fa basket\n1f9fb roll\n1f9fc soap\n1f9fd sponge\n1f9ef fire extinguisher\n1f6d2 cart\n1f525 fire\n1f4a7 drop water\n1f30a ocean wave\n1f32c wind\n1f300 cyclone\n1f308 rainbow\n1f302 umbrella\n2602 umbrella\n2614 rain umbrella\n26a1 bolt lightning\n2744 snow\n2603 snowman\n26c4 snowman\n1f525 fire\n1f4a5 boom\n1f4a8 dash\n1f4ab dizzy\n1f4ac chat speech\n1f5e8 speech\n1f4ad thought\n1f4a4 zzz sleep\n1f4a2 anger\n1f4a6 sweat\n1f4a3 bomb\n1f0cf joker\n1f3b4 cards\n1f004 mahjong\n1f3ae game\n1f3af target\n1f3b2 dice\n1f3b3 bowling\n1f9e9 puzzle\n265f chess\n1f3c1 flag checkered\n1f6a9 flag\n1f3f3 flag\n1f3f4 flag\n1f1e6 flag\n2b50 star\n1f31f star\n1f320 star\n2728 sparkle\n26a0 warning\n1f6ab no\n26d4 no entry\n1f4f4 phone off\n1f515 mute bell\n1f514 bell\n1f4e2 mega loud\n1f50a speaker\n1f509 sound\n1f508 sound\n1f507 mute\n1f4e3 mega\n1f4ef horn\n1f514 bell\n1f3b5 music\n1f3b6 music\n1f4f0 news\n1f4f1 phone\n1f4bb laptop\n1f4fa tv\n1f4fc vhs\n1f4fd film\n1f3ac movie\n1f39e film\n1f4f7 camera\n1f4f8 camera\n1f4f9 video\n1f3a5 camera\n1f4de phone\n1f4df pager\n1f4e0 fax";
    }
}
