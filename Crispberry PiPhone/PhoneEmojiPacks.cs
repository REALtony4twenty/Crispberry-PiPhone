using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace Crispberry_PiPhone
{
    internal static partial class PhoneEmoji
    {
        private struct PackInfo
        {
            public int ModTab;
            public List<int> Also;
        }

        private static List<string> _extraTabs;
        private static Dictionary<string, PackInfo> _packInfo;
        private static Dictionary<string, string> _packWords;
        private static List<string> _sourceIds;
        private static List<string> _sourceLabels;
        private static List<StickerGroup> _stickerPacks;
        private static HashSet<int> _borrowed;
        private const int MaxEdge = 128;

        private sealed class StickerGroup
        {
            public string Id;
            public string Label;
            public readonly List<Sprite> Art = new List<Sprite>();
        }

        private static int ExtraTabCount
        {
            get { return _extraTabs == null ? 0 : _extraTabs.Count; }
        }

        public static string CategoryTitle(int category)
        {
            int at = category - 11;
            if (_extraTabs == null || at < 0 || at >= _extraTabs.Count)
                return string.Empty;
            return _extraTabs[at];
        }

        public static int PackCount
        {
            get { return _sourceIds == null ? 0 : _sourceIds.Count; }
        }

        public static string PackIdAt(int index)
        {
            if (_sourceIds == null || index < 0 || index >= _sourceIds.Count)
                return string.Empty;
            return _sourceIds[index];
        }

        public static string PackLabelAt(int index)
        {
            if (_sourceLabels == null || index < 0 || index >= _sourceLabels.Count)
                return string.Empty;
            return _sourceLabels[index];
        }

        public static int StickerPackCount
        {
            get { return _stickerPacks == null ? 0 : _stickerPacks.Count; }
        }

        public static int StickerCount
        {
            get
            {
                if (_stickerPacks == null)
                    return 0;
                int n = 0;
                for (int i = 0; i < _stickerPacks.Count; i++)
                    n += _stickerPacks[i].Art.Count;
                return n;
            }
        }

        public static string StickerPackLabel(int pack)
        {
            if (_stickerPacks == null || pack < 0 || pack >= _stickerPacks.Count)
                return string.Empty;
            return _stickerPacks[pack].Label ?? string.Empty;
        }

        public static int StickerCountIn(int pack)
        {
            if (_stickerPacks == null || pack < 0 || pack >= _stickerPacks.Count)
                return 0;
            return _stickerPacks[pack].Art.Count;
        }

        public static Sprite StickerSprite(int pack, int index)
        {
            if (_stickerPacks == null || pack < 0 || pack >= _stickerPacks.Count)
                return null;
            List<Sprite> art = _stickerPacks[pack].Art;
            if (index < 0 || index >= art.Count)
                return null;
            return art[index];
        }

        public static byte[] StickerPng(int pack, int index)
        {
            Sprite sprite = StickerSprite(pack, index);
            if (sprite == null || sprite.texture == null)
                return null;
            return PngOfSprite(sprite);
        }

        public static bool CategoryVisible(int category)
        {
            if (category < 2)
                return true;
            if (category >= 11)
                return category - 11 < ExtraTabCount;
            if (PhoneTheme.EmojiBuiltIn)
                return true;
            if (_packInfo == null)
                return false;
            foreach (KeyValuePair<string, PackInfo> pair in _packInfo)
            {
                PackInfo info = pair.Value;
                if (info.Also == null)
                    continue;
                for (int i = 0; i < info.Also.Count; i++)
                {
                    if (info.Also[i] == category)
                        return true;
                }
            }
            return false;
        }

        private static void ResetPackTables()
        {
            _extraTabs = null;
            _packInfo = null;
            _packWords = null;
            if (_sourceIds != null)
                _sourceIds.Clear();
            if (_sourceLabels != null)
                _sourceLabels.Clear();
            if (_stickerPacks != null)
            {
                for (int i = 0; i < _stickerPacks.Count; i++)
                {
                    List<Sprite> art = _stickerPacks[i].Art;
                    for (int s = 0; s < art.Count; s++)
                        DestroySprite(art[s]);
                }
                _stickerPacks = null;
            }
            if (_borrowed != null)
                _borrowed.Clear();
        }

        private static void DestroySprite(Sprite sprite)
        {
            if (sprite == null)
                return;
            Texture2D tex = sprite.texture;
            if (tex != null && !BorrowedTexture(tex))
                UnityEngine.Object.Destroy(tex);
            UnityEngine.Object.Destroy(sprite);
        }

        private static bool BorrowedTexture(Texture tex)
        {
            return tex != null && _borrowed != null && _borrowed.Contains(tex.GetInstanceID());
        }

        public static bool IsPack(string name)
        {
            return _packInfo != null && !string.IsNullOrEmpty(name) && _packInfo.ContainsKey(name);
        }

        /// <summary>-1 when this is a built-in emoji. 1 when the pack emoji is in the tab. 0 when it is a pack emoji in a different tab.</summary>
        private static int PackInCategory(string name, int category)
        {
            if (_packInfo == null || string.IsNullOrEmpty(name))
                return -1;
            PackInfo info;
            if (!_packInfo.TryGetValue(name, out info))
                return -1;
            if (info.ModTab == category)
                return 1;
            if (info.Also != null)
            {
                for (int i = 0; i < info.Also.Count; i++)
                {
                    if (info.Also[i] == category)
                        return 1;
                }
            }
            return 0;
        }

        private static string PackWords(string name)
        {
            if (_packWords == null || string.IsNullOrEmpty(name))
                return null;
            string words;
            return _packWords.TryGetValue(name, out words) ? words : null;
        }

        private static void LoadPacks()
        {
            if (_sourceIds == null)
                _sourceIds = new List<string>();
            else
                _sourceIds.Clear();
            if (_sourceLabels == null)
                _sourceLabels = new List<string>();
            else
                _sourceLabels.Clear();
            string root = Paths.PluginPath;
            if (string.IsNullOrEmpty(root))
                root = Path.Combine(Paths.BepInExRootPath ?? string.Empty, "plugins");
            if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
            {
                var dirs = new List<string>();
                WalkEmojiDirs(root, dirs);
                dirs.Sort(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < dirs.Count; i++)
                    LoadPackDir(dirs[i]);
            }
            LoadPeak();
        }

        private static void WalkEmojiDirs(string dir, List<string> into)
        {
            string[] subs;
            try
            {
                subs = Directory.GetDirectories(dir);
            }
            catch
            {
                return;
            }
            for (int i = 0; i < subs.Length; i++)
            {
                if (string.Equals(Path.GetFileName(subs[i]), "Emojis", StringComparison.OrdinalIgnoreCase))
                    into.Add(subs[i]);
                WalkEmojiDirs(subs[i], into);
            }
        }

        private static void LoadPackDir(string dir)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(dir);
            }
            catch
            {
                return;
            }
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            string label = ModLabel(dir);
            string id = PackId(dir);
            bool emojisOn = PhoneTheme.EmojiPackOn(id);
            bool stickersOn = PhoneTheme.StickerPackOn(id);
            Dictionary<string, List<string>> wanted = emojisOn ? ReadPackJson(dir) : null;
            int modTab = -1;
            int loaded = 0;
            int stickers = 0;
            bool listed = false;
            for (int i = 0; i < files.Length; i++)
            {
                string ext = Path.GetExtension(files[i]);
                if (!IsImageExt(ext))
                    continue;
                string stem = Path.GetFileNameWithoutExtension(files[i]);
                if (!StemOk(stem))
                    continue;
                int w;
                int h;
                if (!TryImageSize(files[i], out w, out h))
                    continue;
                if (w > MaxEdge || h > MaxEdge)
                {
                    Plugin.LogInfo("Emoji '" + stem + "' is over 128 and was skipped.");
                    continue;
                }
                listed = true;
                bool square = w == MaxEdge && h == MaxEdge;
                bool wantEmoji = emojisOn;
                bool wantSticker = stickersOn && square;
                if (!wantEmoji && !wantSticker)
                    continue;
                Texture2D tex = LoadPackTexture(files[i], stem);
                if (tex == null)
                    continue;
                if (tex.width > MaxEdge || tex.height > MaxEdge)
                {
                    UnityEngine.Object.Destroy(tex);
                    Plugin.LogInfo("Emoji '" + stem + "' is over 128 and was skipped.");
                    continue;
                }
                Sprite sprite = SpriteFrom(tex, stem);
                if (sprite == null)
                {
                    UnityEngine.Object.Destroy(tex);
                    continue;
                }
                bool keptEmoji = false;
                if (wantEmoji)
                {
                    if (modTab < 0)
                        modTab = TabFor(label);
                    List<int> also = ExtraTabs(wanted, stem, modTab);
                    keptEmoji = RememberPack(stem, sprite, modTab, also);
                    if (keptEmoji)
                        loaded++;
                }
                if (wantSticker && keptEmoji)
                {
                    Sprite art = SpriteFrom(tex, stem);
                    if (art != null)
                    {
                        RememberSticker(id, label, art);
                        stickers++;
                    }
                }
                else if (wantSticker && !wantEmoji)
                {
                    RememberSticker(id, label, sprite);
                    stickers++;
                }
            }
            if (listed)
                RememberSource(id, label);
            if (loaded > 0)
                Plugin.LogInfo("Emoji pack '" + label + "' loaded " + loaded + (loaded == 1 ? " picture." : " pictures."));
            if (stickers > 0)
                Plugin.LogInfo("Emoji pack '" + label + "' loaded " + stickers + (stickers == 1 ? " sticker." : " stickers."));
        }

        private static bool TryImageSize(string path, out int w, out int h)
        {
            w = 0;
            h = 0;
            try
            {
                using (FileStream fs = File.OpenRead(path))
                {
                    byte[] buf = new byte[24];
                    int n = fs.Read(buf, 0, 24);
                    if (n >= 24 && buf[0] == 0x89 && buf[1] == 0x50 && buf[2] == 0x4E && buf[3] == 0x47)
                    {
                        w = (buf[16] << 24) | (buf[17] << 16) | (buf[18] << 8) | buf[19];
                        h = (buf[20] << 24) | (buf[21] << 16) | (buf[22] << 8) | buf[23];
                        return w > 0 && h > 0;
                    }
                    if (n >= 2 && buf[0] == 0xFF && buf[1] == 0xD8)
                        return TryJpegSize(fs, out w, out h);
                }
            }
            catch
            {
                return false;
            }
            return false;
        }

        private static bool TryJpegSize(FileStream fs, out int w, out int h)
        {
            w = 0;
            h = 0;
            fs.Position = 2;
            int guard = 0;
            while (guard < 80 && fs.Position + 4 < fs.Length)
            {
                guard++;
                int b = fs.ReadByte();
                if (b != 0xFF)
                    continue;
                int marker = fs.ReadByte();
                while (marker == 0xFF)
                    marker = fs.ReadByte();
                if (marker < 0 || marker == 0xD9 || marker == 0xDA)
                    break;
                int lenHi = fs.ReadByte();
                int lenLo = fs.ReadByte();
                if (lenHi < 0 || lenLo < 0)
                    break;
                int len = (lenHi << 8) | lenLo;
                if (len < 2)
                    break;
                bool sof = (marker >= 0xC0 && marker <= 0xC3) || (marker >= 0xC5 && marker <= 0xC7) || (marker >= 0xC9 && marker <= 0xCB) || (marker >= 0xCD && marker <= 0xCF);
                if (sof)
                {
                    byte[] dim = new byte[5];
                    if (fs.Read(dim, 0, 5) < 5)
                        break;
                    h = (dim[1] << 8) | dim[2];
                    w = (dim[3] << 8) | dim[4];
                    return w > 0 && h > 0;
                }
                fs.Seek(len - 2, SeekOrigin.Current);
            }
            return false;
        }

        private static void RememberSticker(string id, string label, Sprite sprite)
        {
            if (sprite == null || string.IsNullOrEmpty(id))
                return;
            if (_stickerPacks == null)
                _stickerPacks = new List<StickerGroup>();
            StickerGroup group = null;
            for (int i = 0; i < _stickerPacks.Count; i++)
            {
                if (string.Equals(_stickerPacks[i].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    group = _stickerPacks[i];
                    break;
                }
            }
            if (group == null)
            {
                group = new StickerGroup();
                group.Id = id;
                group.Label = string.IsNullOrEmpty(label) ? id : label;
                _stickerPacks.Add(group);
            }
            group.Art.Add(sprite);
        }

        private static Sprite SpriteFrom(Texture2D tex, string stem)
        {
            if (tex == null)
                return null;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.HideAndDontSave;
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            sprite.name = stem;
            return sprite;
        }

        private static string PackId(string emojiDir)
        {
            string root = Paths.PluginPath;
            if (string.IsNullOrEmpty(root))
                root = Path.Combine(Paths.BepInExRootPath ?? string.Empty, "plugins");
            string full = Path.GetFullPath(emojiDir);
            string baseDir = string.IsNullOrEmpty(root) ? string.Empty : Path.GetFullPath(root);
            string rel = full;
            if (!string.IsNullOrEmpty(baseDir) && full.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
                rel = full.Substring(baseDir.Length);
            rel = rel.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return rel.Replace('\\', '/');
        }

        private static void RememberSource(string id, string label)
        {
            if (string.IsNullOrEmpty(id))
                return;
            if (_sourceIds == null)
                _sourceIds = new List<string>();
            if (_sourceLabels == null)
                _sourceLabels = new List<string>();
            for (int i = 0; i < _sourceIds.Count; i++)
            {
                if (string.Equals(_sourceIds[i], id, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            string shown = string.IsNullOrEmpty(label) ? id : label;
            for (int i = 0; i < _sourceLabels.Count; i++)
            {
                if (string.Equals(_sourceLabels[i], shown, StringComparison.OrdinalIgnoreCase))
                {
                    string folder = id;
                    int slash = folder.LastIndexOf('/');
                    if (slash > 0)
                        folder = folder.Substring(0, slash);
                    slash = folder.LastIndexOf('/');
                    if (slash >= 0 && slash < folder.Length - 1)
                        folder = folder.Substring(slash + 1);
                    shown = shown + " (" + folder + ")";
                    break;
                }
            }
            _sourceIds.Add(id);
            _sourceLabels.Add(shown);
        }

        private static List<int> ExtraTabs(Dictionary<string, List<string>> wanted, string stem, int modTab)
        {
            List<string> words;
            if (wanted == null || !wanted.TryGetValue(stem, out words) || words == null)
                return null;
            List<int> also = null;
            for (int i = 0; i < words.Count; i++)
            {
                string word = words[i] == null ? string.Empty : words[i].Trim();
                if (word.Length == 0)
                    continue;
                int idx = CategoryFromWord(word);
                if (idx < 0)
                    idx = TabFor(word);
                if (idx == modTab || idx < 2)
                    continue;
                if (also == null)
                    also = new List<int>();
                if (!also.Contains(idx))
                    also.Add(idx);
            }
            return also;
        }

        private static bool RememberPack(string stem, Sprite sprite, int modTab, List<int> also)
        {
            string name = stem.ToLowerInvariant();
            string sequence = ":" + stem + ":";
            if (NameToSeq.ContainsKey(name) || SeqToName.ContainsKey(sequence))
            {
                Plugin.LogInfo("Emoji '" + stem + "' is already loaded.");
                DestroySprite(sprite);
                return false;
            }
            SeqToName[sequence] = name;
            NameToSeq[name] = sequence;
            Order.Add(name);
            NotePrefix(sequence);
            if (sequence.Length > _maxLen)
                _maxLen = sequence.Length;
            Sprites[name] = sprite;
            if (_packInfo == null)
                _packInfo = new Dictionary<string, PackInfo>(StringComparer.OrdinalIgnoreCase);
            PackInfo info;
            info.ModTab = modTab;
            info.Also = also;
            _packInfo[name] = info;
            if (_packWords == null)
                _packWords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _packWords[name] = name.Replace('_', ' ').Replace('-', ' ') + " " + name;
            return true;
        }

        private static Texture2D LoadPackTexture(string path, string stem)
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception ex)
            {
                Plugin.LogError("Emoji '" + stem + "': " + ex.Message);
                return null;
            }
            if (bytes == null || bytes.Length < 32 || bytes.Length > 8 * 1024 * 1024)
                return null;
            Texture2D tex = PhoneImages.LoadTexture(bytes);
            if (tex == null)
            {
                Plugin.LogError("Emoji '" + stem + "' did not load.");
                return null;
            }
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }

        private static string ModLabel(string emojiDir)
        {
            DirectoryInfo parent = Directory.GetParent(emojiDir);
            while (parent != null)
            {
                string name = parent.Name;
                if (!name.Equals("plugins", StringComparison.OrdinalIgnoreCase)
                    && !name.Equals("Emojis", StringComparison.OrdinalIgnoreCase)
                    && !name.Equals("BepInEx", StringComparison.OrdinalIgnoreCase))
                    return PrettyMod(name);
                parent = parent.Parent;
            }
            return "Custom";
        }

        private static string PrettyMod(string folder)
        {
            int dash = folder.IndexOf('-');
            string name = dash >= 0 && dash < folder.Length - 1 ? folder.Substring(dash + 1) : folder;
            name = name.Replace('_', ' ').Trim();
            return name.Length == 0 ? "Custom" : name;
        }

        private static int TabFor(string label)
        {
            if (string.IsNullOrEmpty(label))
                label = "Custom";
            if (_extraTabs == null)
                _extraTabs = new List<string>();
            for (int i = 0; i < _extraTabs.Count; i++)
            {
                if (string.Equals(_extraTabs[i], label, StringComparison.OrdinalIgnoreCase))
                    return 11 + i;
            }
            _extraTabs.Add(label);
            return 10 + _extraTabs.Count;
        }

        private static int CategoryFromWord(string word)
        {
            if (string.IsNullOrEmpty(word))
                return -1;
            word = word.Trim().ToLowerInvariant();
            if (word == "smile" || word == "smiley" || word == "smileys" || word == "face" || word == "faces" || word == "emoji_smile")
                return 2;
            if (word == "people" || word == "person" || word == "persons" || word == "emoji_people")
                return 3;
            if (word == "nature" || word == "animal" || word == "animals" || word == "emoji_nature")
                return 4;
            if (word == "food" || word == "drink" || word == "emoji_food")
                return 5;
            if (word == "travel" || word == "place" || word == "places" || word == "emoji_travel")
                return 6;
            if (word == "play" || word == "activity" || word == "sport" || word == "game" || word == "games" || word == "emoji_play")
                return 7;
            if (word == "things" || word == "thing" || word == "object" || word == "objects" || word == "emoji_things")
                return 8;
            if (word == "signs" || word == "sign" || word == "symbol" || word == "symbols" || word == "emoji_signs")
                return 9;
            if (word == "flags" || word == "flag" || word == "emoji_flags")
                return 10;
            return -1;
        }

        private static bool IsImageExt(string ext)
        {
            return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
        }

        private static bool StemOk(string stem)
        {
            if (string.IsNullOrEmpty(stem) || stem.Length > 48)
                return false;
            for (int i = 0; i < stem.Length; i++)
            {
                char c = stem[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '+' || c == '-';
                if (!ok)
                    return false;
            }
            return true;
        }

        private static Dictionary<string, List<string>> ReadPackJson(string emojiDir)
        {
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            string[] files;
            try
            {
                files = Directory.GetFiles(emojiDir, "*.json");
            }
            catch
            {
                return map;
            }
            for (int f = 0; f < files.Length; f++)
            {
                string text;
                try
                {
                    text = File.ReadAllText(files[f]);
                }
                catch (Exception ex)
                {
                    Plugin.LogError("Emoji json '" + Path.GetFileName(files[f]) + "': " + ex.Message);
                    continue;
                }
                if (string.IsNullOrEmpty(text))
                    continue;
                int i = text[0] == '\uFEFF' ? 1 : 0;
                try
                {
                    SkipWs(text, ref i);
                    if (i >= text.Length)
                        continue;
                    if (text[i] == '[')
                        ReadCategoryArray(text, ref i, map);
                    else if (text[i] == '{')
                        ReadCategoryObject(text, ref i, map);
                }
                catch (Exception ex)
                {
                    Plugin.LogError("Emoji json '" + Path.GetFileName(files[f]) + "': " + ex.Message);
                }
            }
            return map;
        }

        private static void ReadCategoryArray(string s, ref int i, Dictionary<string, List<string>> map)
        {
            i++;
            while (i < s.Length)
            {
                int guard = i;
                SkipWs(s, ref i);
                if (i >= s.Length)
                    return;
                if (s[i] == ']')
                {
                    i++;
                    return;
                }
                if (s[i] == ',')
                    i++;
                else if (s[i] == '{')
                    ReadCategoryObject(s, ref i, map);
                else
                    SkipValue(s, ref i);
                if (i == guard)
                    i++;
            }
        }

        private static void ReadCategoryObject(string s, ref int i, Dictionary<string, List<string>> map)
        {
            i++;
            string category = null;
            List<string> emojis = null;
            while (i < s.Length)
            {
                int guard = i;
                SkipWs(s, ref i);
                if (i >= s.Length)
                    return;
                if (s[i] == '}')
                {
                    i++;
                    break;
                }
                if (s[i] == ',')
                {
                    i++;
                    if (i == guard)
                        i++;
                    continue;
                }
                string key = ReadString(s, ref i);
                if (key == null)
                {
                    SkipValue(s, ref i);
                    if (i == guard)
                        i++;
                    continue;
                }
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ':')
                    i++;
                SkipWs(s, ref i);
                if (key.Equals("category", StringComparison.OrdinalIgnoreCase))
                {
                    category = ReadString(s, ref i);
                    if (category == null)
                        SkipValue(s, ref i);
                }
                else if (key.Equals("emojis", StringComparison.OrdinalIgnoreCase))
                    emojis = ReadEmojiList(s, ref i);
                else
                    SkipValue(s, ref i);
                if (i == guard)
                    i++;
            }
            if (string.IsNullOrEmpty(category) || emojis == null)
                return;
            for (int n = 0; n < emojis.Count; n++)
                AddWanted(map, emojis[n], category);
        }

        private static void AddWanted(Dictionary<string, List<string>> map, string raw, string category)
        {
            string stem = StemOf(raw);
            if (!StemOk(stem))
                return;
            List<string> have;
            if (!map.TryGetValue(stem, out have))
            {
                have = new List<string>();
                map[stem] = have;
            }
            have.Add(category);
        }

        private static string StemOf(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            name = name.Trim();
            if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                name = Path.GetFileNameWithoutExtension(name);
            return name;
        }

        private static List<string> ReadEmojiList(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length)
                return null;
            if (s[i] == '"')
            {
                string raw = ReadString(s, ref i);
                var list = new List<string>();
                if (string.IsNullOrEmpty(raw))
                    return list;
                string[] parts = raw.Split(',');
                for (int p = 0; p < parts.Length; p++)
                {
                    string bit = parts[p].Trim();
                    if (bit.Length > 0)
                        list.Add(bit);
                }
                return list;
            }
            if (s[i] == '[')
            {
                i++;
                var list = new List<string>();
                while (i < s.Length)
                {
                    int guard = i;
                    SkipWs(s, ref i);
                    if (i >= s.Length)
                        break;
                    if (s[i] == ']')
                    {
                        i++;
                        break;
                    }
                    if (s[i] == ',')
                        i++;
                    else if (s[i] == '"')
                    {
                        string bit = ReadString(s, ref i);
                        if (!string.IsNullOrEmpty(bit))
                            list.Add(bit.Trim());
                    }
                    else
                        SkipValue(s, ref i);
                    if (i == guard)
                        i++;
                }
                return list;
            }
            SkipValue(s, ref i);
            return null;
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c != ' ' && c != '\t' && c != '\r' && c != '\n')
                    return;
                i++;
            }
        }

        private static string ReadString(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length || s[i] != '"')
                return null;
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"')
                    return sb.ToString();
                if (c != '\\' || i >= s.Length)
                {
                    sb.Append(c);
                    continue;
                }
                char e = s[i++];
                if (e == 'n')
                    sb.Append('\n');
                else if (e == 't')
                    sb.Append('\t');
                else if (e == 'u' && i + 4 <= s.Length)
                {
                    int cp;
                    if (int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out cp))
                        sb.Append((char)cp);
                    i += 4;
                }
                else
                    sb.Append(e);
            }
            return sb.ToString();
        }

        private static void SkipValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length)
                return;
            char c = s[i];
            if (c == '"')
            {
                ReadString(s, ref i);
                return;
            }
            if (c == '{')
            {
                SkipObject(s, ref i);
                return;
            }
            if (c == '[')
            {
                SkipArray(s, ref i);
                return;
            }
            while (i < s.Length && s[i] != ',' && s[i] != ']' && s[i] != '}')
                i++;
        }

        private static void SkipObject(string s, ref int i)
        {
            i++;
            while (i < s.Length)
            {
                int guard = i;
                SkipWs(s, ref i);
                if (i >= s.Length)
                    return;
                if (s[i] == '}')
                {
                    i++;
                    return;
                }
                if (s[i] == ',')
                {
                    i++;
                    continue;
                }
                if (s[i] == '"')
                    ReadString(s, ref i);
                else
                    i++;
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ':')
                    i++;
                SkipValue(s, ref i);
                if (i == guard)
                    i++;
            }
        }

        private static void SkipArray(string s, ref int i)
        {
            i++;
            while (i < s.Length)
            {
                int guard = i;
                SkipWs(s, ref i);
                if (i >= s.Length)
                    return;
                if (s[i] == ']')
                {
                    i++;
                    return;
                }
                if (s[i] == ',')
                    i++;
                else
                    SkipValue(s, ref i);
                if (i == guard)
                    i++;
            }
        }
    }
}
