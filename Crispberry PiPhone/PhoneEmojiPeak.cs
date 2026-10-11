using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Zorro.Core;

namespace Crispberry_PiPhone
{
    internal static partial class PhoneEmoji
    {
        private const string PeakId = "peak";
        private const string PeakLabel = "PEAK";
        private static HashSet<int> _peakTextures;
        private static string _groupId = PeakId;
        private static string _groupLabel = PeakLabel;
        private static bool _bundleDone;
        private static bool _looksDone;
        private static bool _hudDone;
        private static int _seenBundles;

        private static void LoadPeak()
        {
            RememberSource(PeakId, PeakLabel);
            bool emojisOn = PhoneTheme.EmojiPackOn(PeakId);
            bool stickersOn = PhoneTheme.StickerPackOn(PeakId);
            if (!emojisOn && !stickersOn)
                return;
            _bundleDone = false;
            _looksDone = false;
            _hudDone = false;
            _seenBundles = 0;
            _groupId = PeakId;
            _groupLabel = PeakLabel;
            if (_peakTextures == null)
                _peakTextures = new HashSet<int>();
            else
                _peakTextures.Clear();
            int emojis = 0;
            int stickers = 0;
            int tab = -1;
            try
            {
                AddPeakItems(emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
                _bundleDone = AddPeakBundles(emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
                AddPeakBadges(emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
                AddPeakLooks(emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
                AddPeakEmotes(emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
                AddPeakReticles(emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
                AddPeakNamed(emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
                if (Customization.Instance != null)
                    _looksDone = true;
                if (GUIManager.instance != null)
                    _hudDone = true;
            }
            catch (Exception ex)
            {
                Plugin.LogError("PEAK pictures failed: " + ex.Message);
            }
            if (emojis > 0 || stickers > 0)
                Plugin.LogInfo("PEAK pictures: " + emojis + " emojis, " + stickers + " stickers.");
        }

        private static void TryPeakScene()
        {
            if (_bundleDone && _looksDone && _hudDone)
                return;
            bool emojisOn = PhoneTheme.EmojiPackOn(PeakId);
            bool stickersOn = PhoneTheme.StickerPackOn(PeakId);
            if (!emojisOn && !stickersOn)
            {
                _bundleDone = true;
                _looksDone = true;
                _hudDone = true;
                return;
            }
            if (_peakTextures == null)
                _peakTextures = new HashSet<int>();
            int emojis = 0;
            int stickers = 0;
            int tab = 0;
            try
            {
                if (!_bundleDone)
                    _bundleDone = AddPeakBundles(emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
                if (!_looksDone)
                {
                    AddPeakLooks(emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
                    if (Customization.Instance != null)
                        _looksDone = true;
                }
                if (!_hudDone && GUIManager.instance != null)
                {
                    AddPeakBadges(emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
                    AddPeakEmotes(emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
                    AddPeakReticles(emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
                    AddPeakNamed(emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
                    _hudDone = true;
                }
            }
            catch (Exception ex)
            {
                Plugin.LogError("PEAK pictures failed: " + ex.Message);
            }
            if (emojis > 0 || stickers > 0)
            {
                SortCatalog();
                _revision++;
                Plugin.LogInfo("PEAK pictures: " + emojis + " more emojis, " + stickers + " more stickers.");
            }
        }

        private static void UseGroup(string id, string label)
        {
            _groupId = id;
            _groupLabel = label;
        }

        private static void AddPeakItems(bool emojisOn, bool stickersOn, ref int emojis, ref int stickers, ref int tab)
        {
            ItemDatabase db;
            try
            {
                db = SingletonAsset<ItemDatabase>.Instance;
            }
            catch
            {
                return;
            }
            if (db == null)
                return;
            UseGroup(PeakId, PeakLabel);
            var seen = new HashSet<int>();
            if (db.itemLookup != null)
            {
                foreach (KeyValuePair<ushort, Item> pair in db.itemLookup)
                    ConsiderPeakItem(pair.Value, seen, emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            }
            if (db.Objects != null)
            {
                for (int i = 0; i < db.Objects.Count; i++)
                    ConsiderPeakItem(db.Objects[i], seen, emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            }
        }

        private static void ConsiderPeakItem(Item item, HashSet<int> seen, bool emojisOn, bool stickersOn, ref int emojis, ref int stickers, ref int tab)
        {
            if (item == null || !seen.Add(item.GetInstanceID()) || item.UIData == null)
                return;
            Texture2D icon;
            try
            {
                icon = item.UIData.GetIcon();
            }
            catch
            {
                return;
            }
            string raw = item.gameObject != null ? item.gameObject.name : item.name;
            KeepPeak(icon, raw, emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
        }

        private static void AddPeakBadges(bool emojisOn, bool stickersOn, ref int emojis, ref int stickers, ref int tab)
        {
            UseGroup("peak-badges", "Badges");
            GUIManager gui = GUIManager.instance;
            if (gui != null)
                KeepBadgeManager(gui.mainBadgeManager, emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            BadgeManager[] managers = Resources.FindObjectsOfTypeAll<BadgeManager>();
            for (int m = 0; m < managers.Length; m++)
            {
                BadgeManager mgr = managers[m];
                if (mgr == null || mgr.badgeData == null)
                    continue;
                for (int i = 0; i < mgr.badgeData.Length; i++)
                    KeepBadge(mgr.badgeData[i], emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            }
            BadgeData[] datas = Resources.FindObjectsOfTypeAll<BadgeData>();
            for (int i = 0; i < datas.Length; i++)
                KeepBadge(datas[i], emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
        }

        private static void KeepBadgeManager(BadgeManager mgr, bool emojisOn, bool stickersOn, ref int emojis, ref int stickers, ref int tab)
        {
            if (mgr == null)
                return;
            if (mgr.badgeData != null)
            {
                for (int i = 0; i < mgr.badgeData.Length; i++)
                    KeepBadge(mgr.badgeData[i], emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            }
            if (mgr.badges == null)
                return;
            for (int i = 0; i < mgr.badges.Length; i++)
            {
                if (mgr.badges[i] != null)
                    KeepBadge(mgr.badges[i].data, emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            }
        }

        private static void KeepBadge(BadgeData badge, bool emojisOn, bool stickersOn, ref int emojis, ref int stickers, ref int tab)
        {
            if (badge == null || badge.icon == null)
                return;
            string raw = badge.displayName;
            if (string.IsNullOrEmpty(raw))
                raw = badge.icon.name;
            if (string.IsNullOrEmpty(raw) || raw.IndexOf("badge", StringComparison.OrdinalIgnoreCase) < 0)
                raw = "badge_" + raw;
            KeepPeak(badge.icon as Texture2D, raw, emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
        }

        private static void AddPeakLooks(bool emojisOn, bool stickersOn, ref int emojis, ref int stickers, ref int tab)
        {
            Customization live = Customization.Instance;
            if (live != null)
                KeepLooks(live, emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            Customization[] found = Resources.FindObjectsOfTypeAll<Customization>();
            for (int i = 0; i < found.Length; i++)
            {
                Customization custom = found[i];
                if (custom == null || custom == live)
                    continue;
                KeepLooks(custom, emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            }
        }

        private static void KeepLooks(Customization custom, bool emojisOn, bool stickersOn, ref int emojis, ref int stickers, ref int tab)
        {
            UseGroup("peak-hats", "Hats");
            KeepOptions(custom.hats, emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            KeepOption(custom.goatHat, emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            KeepOption(custom.crownHat, emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            UseGroup("peak-fits", "Outfits");
            KeepOptions(custom.fits, emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
        }

        private static bool AddPeakBundles(bool emojisOn, bool stickersOn, ref int emojis, ref int stickers, ref int tab)
        {
            var bundles = new List<AssetBundle>();
            var seen = new HashSet<int>();
            try
            {
                foreach (AssetBundle bundle in AssetBundle.GetAllLoadedAssetBundles())
                {
                    if (bundle != null && seen.Add(bundle.GetInstanceID()))
                        bundles.Add(bundle);
                }
            }
            catch (Exception ex)
            {
                Plugin.LogError("PEAK files failed: " + ex.Message);
            }
            AssetBundle[] extra = Resources.FindObjectsOfTypeAll<AssetBundle>();
            for (int i = 0; i < extra.Length; i++)
            {
                AssetBundle bundle = extra[i];
                if (bundle != null && seen.Add(bundle.GetInstanceID()))
                    bundles.Add(bundle);
            }
            if (bundles.Count == 0 || bundles.Count == _seenBundles)
                return false;
            _seenBundles = bundles.Count;
            int mostNames = 0;
            int opened = 0;
            for (int b = 0; b < bundles.Count; b++)
            {
                AssetBundle bundle = bundles[b];
                try
                {
                    opened += KeepBundleAssets(bundle.LoadAllAssets<BadgeData>(), emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
                    opened += KeepBundleAssets(bundle.LoadAllAssets<EmoteWheelData>(), emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
                }
                catch (Exception ex)
                {
                    Plugin.LogError("PEAK files failed: " + ex.Message);
                }
                string[] names = null;
                try
                {
                    names = bundle.GetAllAssetNames();
                }
                catch
                {
                    continue;
                }
                if (names == null || names.Length == 0)
                    continue;
                if (names.Length > mostNames)
                    mostNames = names.Length;
                for (int n = 0; n < names.Length; n++)
                {
                    if (!WantPeakFile(names[n]))
                        continue;
                    UnityEngine.Object asset = null;
                    try
                    {
                        asset = bundle.LoadAsset(names[n]);
                    }
                    catch
                    {
                        continue;
                    }
                    int before = emojis + stickers;
                    KeepBundleAsset(asset, emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
                    if (emojis + stickers > before)
                        opened++;
                }
            }
            if (opened == 0 && mostNames < 500)
                return false;
            Plugin.LogInfo("PEAK files: " + opened + " pictures from the game data.");
            return true;
        }

        private static int KeepBundleAssets(BadgeData[] assets, bool emojisOn, bool stickersOn, ref int emojis, ref int stickers, ref int tab)
        {
            if (assets == null || assets.Length == 0)
                return 0;
            UseGroup("peak-badges", "Badges");
            int opened = 0;
            for (int i = 0; i < assets.Length; i++)
            {
                int before = emojis + stickers;
                KeepBadge(assets[i], emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
                if (emojis + stickers > before)
                    opened++;
            }
            return opened;
        }

        private static int KeepBundleAssets(EmoteWheelData[] assets, bool emojisOn, bool stickersOn, ref int emojis, ref int stickers, ref int tab)
        {
            if (assets == null || assets.Length == 0)
                return 0;
            UseGroup("peak-emotes", "Emotes");
            int opened = 0;
            for (int i = 0; i < assets.Length; i++)
            {
                int before = emojis + stickers;
                KeepEmote(assets[i], emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
                if (emojis + stickers > before)
                    opened++;
            }
            return opened;
        }

        private static void KeepBundleAsset(UnityEngine.Object asset, bool emojisOn, bool stickersOn, ref int emojis, ref int stickers, ref int tab)
        {
            if (asset == null)
                return;
            BadgeData badge = asset as BadgeData;
            if (badge != null)
            {
                UseGroup("peak-badges", "Badges");
                KeepBadge(badge, emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
                return;
            }
            EmoteWheelData emote = asset as EmoteWheelData;
            if (emote != null)
            {
                UseGroup("peak-emotes", "Emotes");
                KeepEmote(emote, emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
                return;
            }
            Sprite sprite = asset as Sprite;
            if (sprite != null && sprite.texture != null)
            {
                UseGroup(PeakGroupId(sprite.name), PeakGroupLabel(sprite.name));
                KeepPeak(sprite.texture, sprite.rect, sprite.name, emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
                return;
            }
            Texture2D tex = asset as Texture2D;
            if (tex == null)
                return;
            UseGroup(PeakGroupId(tex.name), PeakGroupLabel(tex.name));
            KeepPeak(tex, tex.name, emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
        }

        private static bool WantPeakFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            string file = path;
            int slash = path.LastIndexOf('/');
            if (slash < 0)
                slash = path.LastIndexOf('\\');
            if (slash >= 0 && slash < path.Length - 1)
                file = path.Substring(slash + 1);
            int dot = file.LastIndexOf('.');
            string ext = dot >= 0 ? file.Substring(dot) : string.Empty;
            string stem = dot > 0 ? file.Substring(0, dot) : file;
            if (ext == ".unity" || ext == ".prefab" || ext == ".fbx" || ext == ".controller" || ext == ".anim"
                || ext == ".mat" || ext == ".shader" || ext == ".wav" || ext == ".mp3" || ext == ".ogg"
                || ext == ".spriteatlas")
                return false;
            if (PeakNamed(stem))
                return true;
            if (stem.StartsWith("Emote_", StringComparison.OrdinalIgnoreCase))
                return true;
            if (ext == ".asset" && (stem.IndexOf("badge", StringComparison.OrdinalIgnoreCase) >= 0
                || stem.IndexOf("emote", StringComparison.OrdinalIgnoreCase) >= 0))
                return true;
            return path.IndexOf("/emote", StringComparison.OrdinalIgnoreCase) >= 0
                && (ext == ".asset" || ext == ".png" || ext == ".jpg");
        }

        private static void KeepOptions(CustomizationOption[] options, bool emojisOn, bool stickersOn, ref int emojis, ref int stickers, ref int tab)
        {
            if (options == null)
                return;
            for (int i = 0; i < options.Length; i++)
                KeepOption(options[i], emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
        }

        private static void KeepOption(CustomizationOption opt, bool emojisOn, bool stickersOn, ref int emojis, ref int stickers, ref int tab)
        {
            if (opt == null || opt.isBlank || opt.texture == null)
                return;
            KeepPeak(opt.texture as Texture2D, opt.texture.name, emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
        }

        private static void AddPeakEmotes(bool emojisOn, bool stickersOn, ref int emojis, ref int stickers, ref int tab)
        {
            UseGroup("peak-emotes", "Emotes");
            GUIManager gui = GUIManager.instance;
            if (gui != null && gui.emoteWheel != null)
            {
                EmoteWheel wheel = gui.emoteWheel.GetComponent<EmoteWheel>();
                if (wheel == null)
                    wheel = gui.emoteWheel.GetComponentInChildren<EmoteWheel>(true);
                if (wheel != null && wheel.data != null)
                {
                    for (int i = 0; i < wheel.data.Length; i++)
                        KeepEmote(wheel.data[i], emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
                }
            }
            EmoteWheelData[] datas = Resources.FindObjectsOfTypeAll<EmoteWheelData>();
            for (int i = 0; i < datas.Length; i++)
                KeepEmote(datas[i], emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
        }

        private static void KeepEmote(EmoteWheelData data, bool emojisOn, bool stickersOn, ref int emojis, ref int stickers, ref int tab)
        {
            if (data == null || data.emoteSprite == null || data.emoteSprite.texture == null)
                return;
            string raw = string.IsNullOrEmpty(data.emoteName) ? data.emoteSprite.name : "emote_" + data.emoteName;
            KeepPeak(data.emoteSprite.texture, data.emoteSprite.rect, raw, emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
        }

        private static void AddPeakReticles(bool emojisOn, bool stickersOn, ref int emojis, ref int stickers, ref int tab)
        {
            UseGroup("peak-icons", "Icons");
            GUIManager gui = GUIManager.instance;
            if (gui == null)
                return;
            KeepReticle(gui.reticleDefault, "reticle_default", emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            KeepReticle(gui.reticleX, "reticle_x", emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            KeepReticle(gui.reticleClimb, "reticle_climb", emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            KeepReticle(gui.reticleClimbJump, "reticle_climb_jump", emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            KeepReticle(gui.reticleThrow, "reticle_throw", emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            KeepReticle(gui.reticleReach, "reticle_reach", emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            KeepReticle(gui.reticleGrasp, "reticle_grasp", emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            KeepReticle(gui.reticleSpike, "reticle_spike", emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            KeepReticle(gui.reticleRope, "reticle_rope", emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            KeepReticle(gui.reticleClimbTry, "reticle_climb_try", emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            KeepReticle(gui.reticleVine, "reticle_vine", emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            KeepReticle(gui.reticleBoost, "reticle_boost", emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            KeepReticle(gui.reticleShoot, "reticle_shoot", emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            KeepReticle(gui.reticleKick, "reticle_kick", emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            KeepReticle(gui.reticleLadder, "reticle_ladder", emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
        }

        private static void KeepReticle(GameObject go, string raw, bool emojisOn, bool stickersOn, ref int emojis, ref int stickers, ref int tab)
        {
            if (go == null)
                return;
            Image image = go.GetComponent<Image>();
            if (image == null || image.sprite == null || image.sprite.texture == null)
                return;
            KeepPeak(image.sprite.texture, image.sprite.rect, raw, emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
        }

        private static void AddPeakNamed(bool emojisOn, bool stickersOn, ref int emojis, ref int stickers, ref int tab)
        {
            Texture2D[] all = Resources.FindObjectsOfTypeAll<Texture2D>();
            for (int i = 0; i < all.Length; i++)
            {
                Texture2D tex = all[i];
                if (tex == null || !PeakNamed(tex.name))
                    continue;
                UseGroup(PeakGroupId(tex.name), PeakGroupLabel(tex.name));
                KeepPeak(tex, tex.name, emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
            }
        }

        private static string PeakGroupId(string name)
        {
            if (name.StartsWith("Badge_", StringComparison.OrdinalIgnoreCase))
                return "peak-badges";
            if (name.StartsWith("Hat_Icon_", StringComparison.OrdinalIgnoreCase))
                return "peak-hats";
            if (name.StartsWith("Fit_Icon_", StringComparison.OrdinalIgnoreCase))
                return "peak-fits";
            if (name.StartsWith("Emote_", StringComparison.OrdinalIgnoreCase))
                return "peak-emotes";
            if (name.StartsWith("IC_", StringComparison.OrdinalIgnoreCase))
                return "peak-status";
            if (name.StartsWith("ScoutCampfire_", StringComparison.OrdinalIgnoreCase))
                return "peak-icons";
            return "peak-icons";
        }

        private static string PeakGroupLabel(string name)
        {
            if (name.StartsWith("Badge_", StringComparison.OrdinalIgnoreCase))
                return "Badges";
            if (name.StartsWith("Hat_Icon_", StringComparison.OrdinalIgnoreCase))
                return "Hats";
            if (name.StartsWith("Fit_Icon_", StringComparison.OrdinalIgnoreCase))
                return "Outfits";
            if (name.StartsWith("Emote_", StringComparison.OrdinalIgnoreCase))
                return "Emotes";
            if (name.StartsWith("IC_", StringComparison.OrdinalIgnoreCase))
                return "Status";
            return "Icons";
        }

        private static bool PeakNamed(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            if (name.StartsWith("Badge_", StringComparison.OrdinalIgnoreCase))
                return true;
            if (name.StartsWith("Hat_Icon_", StringComparison.OrdinalIgnoreCase))
                return true;
            if (name.StartsWith("Fit_Icon_", StringComparison.OrdinalIgnoreCase))
                return true;
            if (name.StartsWith("IC_", StringComparison.OrdinalIgnoreCase))
                return true;
            if (name.StartsWith("Reticle_", StringComparison.OrdinalIgnoreCase))
                return true;
            if (name.StartsWith("AudioIcon", StringComparison.OrdinalIgnoreCase))
                return true;
            if (name.StartsWith("ScoutCampfire_", StringComparison.OrdinalIgnoreCase))
                return true;
            return name.Equals("ScoutIcon", StringComparison.OrdinalIgnoreCase)
                || name.Equals("PeakIcon", StringComparison.OrdinalIgnoreCase)
                || name.Equals("MuteIcon", StringComparison.OrdinalIgnoreCase)
                || name.Equals("BiometricIcon", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Camera Icon", StringComparison.OrdinalIgnoreCase);
        }

        private static void KeepPeak(Texture2D tex, string raw, bool emojisOn, bool stickersOn, ref int emojis, ref int stickers, ref int tab)
        {
            if (tex == null)
                return;
            KeepPeak(tex, new Rect(0f, 0f, tex.width, tex.height), raw, emojisOn, stickersOn, ref emojis, ref stickers, ref tab);
        }

        private static void KeepPeak(Texture2D tex, Rect rect, string raw, bool emojisOn, bool stickersOn, ref int emojis, ref int stickers, ref int tab)
        {
            if (tex == null || rect.width < 16f || rect.height < 16f)
                return;
            if (tex.width > 2048 || tex.height > 2048 || tex.width < 16 || tex.height < 16)
                return;
            string stem = Slug(raw);
            if (!StemOk(stem) || NameToSeq.ContainsKey(stem))
                return;
            bool whole = rect.width >= tex.width - 0.5f && rect.height >= tex.height - 0.5f;
            if (whole && !_peakTextures.Add(tex.GetInstanceID()))
                return;
            Borrow(tex);
            Sprite sprite = BorrowSprite(tex, rect, stem);
            if (sprite == null)
                return;
            bool keptEmoji = false;
            if (emojisOn)
            {
                tab = TabFor(_groupLabel);
                keptEmoji = RememberPack(stem, sprite, tab, null);
                if (keptEmoji)
                    emojis++;
            }
            if (stickersOn && keptEmoji)
            {
                Sprite art = BorrowSprite(tex, rect, stem);
                if (art != null)
                {
                    RememberSticker(_groupId, _groupLabel, art);
                    stickers++;
                }
            }
            else if (stickersOn && !emojisOn)
            {
                RememberSticker(_groupId, _groupLabel, sprite);
                stickers++;
            }
            else if (!emojisOn)
                DestroySprite(sprite);
        }

        private static void Borrow(Texture2D tex)
        {
            if (tex == null)
                return;
            if (_borrowed == null)
                _borrowed = new HashSet<int>();
            _borrowed.Add(tex.GetInstanceID());
        }

        private static Sprite BorrowSprite(Texture2D tex, Rect rect, string stem)
        {
            if (tex == null || rect.width < 1f || rect.height < 1f)
                return null;
            Sprite sprite = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            sprite.name = stem;
            return sprite;
        }

        private static string Slug(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;
            int clone = raw.IndexOf("(Clone)", StringComparison.Ordinal);
            if (clone >= 0)
                raw = raw.Substring(0, clone);
            var sb = new StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '+' || c == '-';
                if (ok)
                    sb.Append(c);
            }
            if (sb.Length == 0 || sb.Length > 48)
                return string.Empty;
            return sb.ToString();
        }

        private static byte[] PngOfSprite(Sprite sprite)
        {
            if (sprite == null)
                return null;
            Texture2D tex = sprite.texture;
            if (tex == null)
                return null;
            Rect area = sprite.rect;
            try
            {
                area = sprite.textureRect;
            }
            catch
            {
            }
            int x = Mathf.Clamp(Mathf.FloorToInt(area.x), 0, Mathf.Max(0, tex.width - 1));
            int y = Mathf.Clamp(Mathf.FloorToInt(area.y), 0, Mathf.Max(0, tex.height - 1));
            int w = Mathf.Clamp(Mathf.RoundToInt(area.width), 1, tex.width - x);
            int h = Mathf.Clamp(Mathf.RoundToInt(area.height), 1, tex.height - y);
            Texture2D full = tex.isReadable ? tex : ReadableCopy(tex);
            if (full == null)
                return null;
            Color[] pixels;
            try
            {
                pixels = full.GetPixels(x, y, w, h);
            }
            catch
            {
                if (full != tex)
                    UnityEngine.Object.Destroy(full);
                return null;
            }
            var crop = new Texture2D(w, h, TextureFormat.RGBA32, false);
            crop.SetPixels(pixels);
            crop.Apply();
            byte[] png = PhoneImages.EncodePng(crop);
            UnityEngine.Object.Destroy(crop);
            if (full != tex)
                UnityEngine.Object.Destroy(full);
            return png;
        }

        private static Texture2D ReadableCopy(Texture2D tex)
        {
            RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0f, 0f, tex.width, tex.height), 0, 0);
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }

        private static byte[] PngOf(Texture2D tex)
        {
            if (tex == null)
                return null;
            if (tex.isReadable)
                return PhoneImages.EncodePng(tex);
            RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0f, 0f, tex.width, tex.height), 0, 0);
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            byte[] png = PhoneImages.EncodePng(copy);
            UnityEngine.Object.Destroy(copy);
            return png;
        }
    }
}
