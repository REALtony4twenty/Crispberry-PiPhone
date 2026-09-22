using System;
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

        public static void WarmOnePage()
        {
            Ensure();
            if (_warmDone || Order.Count == 0)
            {
                _warmDone = true;
                return;
            }
            int end = _warmed + WarmPage;
            if (end > Order.Count)
                end = Order.Count;
            for (int i = _warmed; i < end; i++)
                SpriteFor(Sequence(Order[i]));
            _warmed = end;
            if (_warmed >= Order.Count)
                _warmDone = true;
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
    }
}
