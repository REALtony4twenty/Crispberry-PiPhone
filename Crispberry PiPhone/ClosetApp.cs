using System;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    internal static class ClosetApp
    {
        private static Session _live;
        private static Material _eyeMat;
        private static bool _passportLogged;
        private static readonly Dictionary<int, Sprite> _passportIcons = new Dictionary<int, Sprite>();

        internal static void Register()
        {
            PiPhoneApi.RegisterApp(new PiPhoneApp
            {
                Id = BuiltinApps.ClosetId,
                DisplayName = "Closet",
                IconGlyph = "W",
                IconBackground = new Color(0.48f, 0.32f, 0.62f, 1f),
                SortOrder = 24,
                ShowOnHome = true,
                Immersive = true,
                OnOpen = host => { _live = new Session(host); _live.Build(); },
                OnClose = () =>
                {
                    if (_live != null)
                        _live.Cleanup();
                    _live = null;
                },
                OnOrientation = () => { if (_live != null) _live.Build(); }
            });
        }

        internal static bool TryGoBack()
        {
            return ClosetSkinSliders.Hide();
        }

        private sealed class Session
        {
            private const float PreviewAspect = 9f / 16f;
            private const int RtWidth = 360;
            private const int RtHeight = 640;

            private readonly IPiPhoneHost _host;
            private Customization.Type _type = Customization.Type.Skin;
            private Camera _cam;
            private RenderTexture _rt;
            private RawImage _preview;
            private float _camLift;
            private float _camOrbit;
            private float _camZoom = 1f;
            private RectTransform _optionContent;
            private ScrollRect _scroll;
            private GridLayoutGroup _optionGrid;
            private readonly Button[] _tabBtns = new Button[8];
            private readonly Customization.Type[] _tabTypes =
            {
                Customization.Type.Skin,
                Customization.Type.Fit,
                Customization.Type.Mouth,
                Customization.Type.Sash,
                Customization.Type.Eyes,
                Customization.Type.Hat,
                Customization.Type.Accessory,
                Customization.Type.Medal
            };
            private readonly string[] _tabLabels = { "Skin", "Outfit", "Mouth", "Sash", "Eyes", "Hat", "Extra", "Medal" };
            private bool _logged;

            public Session(IPiPhoneHost host)
            {
                _host = host;
            }

            public void Cleanup()
            {
                ClosetSkinSliders.Hide();
                DestroyCam();
            }

            public void Build()
            {
                for (int i = _host.Content.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(_host.Content.GetChild(i).gameObject);
                _host.SetTitle("Closet");
                var bodyPad = _host.Content.GetComponent<VerticalLayoutGroup>();
                if (bodyPad != null)
                    bodyPad.padding = new RectOffset(8, 8, 6, 6);
                ClosetCatalog.Ensure();
                EnsurePassportIcons();

                if (_host.IsLandscape)
                    BuildLandscape();
                else
                    BuildPortrait();
                PaintTabs();
                FillOptions();
            }

            private void BuildPortrait()
            {
                var title = PhoneUi.CreateLabel(_host.Content, "Title", "Closet", 18f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Size(title.gameObject, 32f);

                var stage = Pane(_host.Content, "Stage", 1f, 176f);
                var row = PhoneUi.AddHorizontal(stage.gameObject, 8f);
                row.childForceExpandHeight = true;
                row.childForceExpandWidth = false;
                row.padding = new RectOffset(2, 2, 0, 0);

                BuildPreview(stage.transform, true);
                BuildTabColumn(stage.transform, false);

                BuildGrid(_host.Content, 1.25f);
            }

            private void BuildLandscape()
            {
                var rowGo = Pane(_host.Content, "Land", 1f, 160f);
                var row = PhoneUi.AddHorizontal(rowGo.gameObject, 8f);
                row.childForceExpandHeight = true;
                row.childForceExpandWidth = false;
                row.childAlignment = TextAnchor.MiddleLeft;

                BuildPreview(rowGo.transform, false);
                BuildGrid(rowGo.transform, 1f);
                BuildTabColumn(rowGo.transform, true);
            }

            private void BuildPreview(Transform parent, bool stretchWidth)
            {
                var previewWrap = PhoneUi.CreateImage(parent, "Preview", PhoneUi.Rounded(16), new Color(0.05f, 0.06f, 0.07f, 1f));
                var wrapLe = previewWrap.gameObject.GetComponent<LayoutElement>() ?? previewWrap.gameObject.AddComponent<LayoutElement>();
                wrapLe.flexibleHeight = 1f;
                wrapLe.minHeight = 140f;
                if (stretchWidth)
                {
                    wrapLe.flexibleWidth = 1f;
                    wrapLe.minWidth = 140f;
                }
                else
                {
                    wrapLe.flexibleWidth = 0f;
                    wrapLe.minWidth = 118f;
                    wrapLe.preferredWidth = 150f;
                }
                var previewGo = new GameObject("Cam", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                previewGo.transform.SetParent(previewWrap, false);
                PhoneUi.Stretch(previewGo.GetComponent<RectTransform>(), 3f, 3f);
                _preview = previewGo.GetComponent<RawImage>();
                _preview.color = Color.white;
                _preview.raycastTarget = false;
                EnsureCam();
                PhoneUi.FitContained(_preview, PreviewAspect);
                BuildCamHud(previewWrap);
            }

            private void BuildCamHud(RectTransform wrap)
            {
                bool land = _host.IsLandscape;
                float barH = land ? 22f : 34f;
                var bar = new GameObject("CamHud", typeof(RectTransform));
                bar.transform.SetParent(wrap, false);
                PhoneUi.IgnoreLayout(bar);
                PhoneUi.StretchBottom(bar.GetComponent<RectTransform>(), barH);
                var h = PhoneUi.AddHorizontal(bar, land ? 2f : 4f);
                h.padding = land ? new RectOffset(3, 3, 2, 2) : new RectOffset(6, 6, 4, 4);
                h.childAlignment = TextAnchor.MiddleCenter;
                h.childForceExpandWidth = false;
                h.childForceExpandHeight = false;
                CamHudBtn(bar.transform, PhoneIcons.Material("chevron_left"), "<", () => NudgeCam(0f, 0f, -18f));
                CamHudBtn(bar.transform, PhoneIcons.Material("chevron_right"), ">", () => NudgeCam(0f, 0f, 18f));
                CamHudBtn(bar.transform, PhoneIcons.Material("expand_less"), "^", () => NudgeCam(0.14f, 0f, 0f));
                CamHudBtn(bar.transform, PhoneIcons.Material("expand_more"), "v", () => NudgeCam(-0.14f, 0f, 0f));
                CamHudBtn(bar.transform, PhoneIcons.Material("remove"), "-", () => NudgeCam(0f, -0.14f, 0f));
                CamHudBtn(bar.transform, PhoneIcons.Material("add"), "+", () => NudgeCam(0f, 0.14f, 0f));
                CamHudBtn(bar.transform, PhoneIcons.Material("filter_center_focus"), "o", RecenterCam);
            }

            private void CamHudBtn(Transform parent, Sprite icon, string fallback, UnityEngine.Events.UnityAction click)
            {
                float s = _host.IsLandscape ? 18f : 26f;
                Button btn = PhoneUi.CreateIconChip(parent, fallback, icon, click, false, new Vector2(s, s));
                var le = btn.GetComponent<LayoutElement>();
                if (le != null)
                {
                    le.minWidth = s;
                    le.preferredWidth = s;
                    le.flexibleWidth = 0f;
                    le.minHeight = s;
                    le.preferredHeight = s;
                    le.flexibleHeight = 0f;
                }
                var img = btn.GetComponent<Image>();
                if (img != null)
                    img.color = new Color(PhoneUi.SurfaceAlt.r, PhoneUi.SurfaceAlt.g, PhoneUi.SurfaceAlt.b, 0.92f);
            }

            private void NudgeCam(float lift, float zoom, float orbit)
            {
                _camLift = Mathf.Clamp(_camLift + lift, -0.85f, 1.6f);
                _camZoom = Mathf.Clamp(_camZoom + zoom, 0.5f, 2.4f);
                _camOrbit += orbit;
            }

            private void RecenterCam()
            {
                _camLift = 0f;
                _camOrbit = 0f;
                _camZoom = 1f;
            }

            private void BuildTabColumn(Transform parent, bool withTitle)
            {
                var tabs = new GameObject("Tabs", typeof(RectTransform));
                tabs.transform.SetParent(parent, false);
                var tabsLe = tabs.AddComponent<LayoutElement>();
                tabsLe.preferredWidth = 92f;
                tabsLe.minWidth = 84f;
                tabsLe.flexibleWidth = 0f;
                tabsLe.flexibleHeight = 1f;
                var v = PhoneUi.AddVertical(tabs, 4f, new RectOffset(0, 0, 0, 0));
                v.childForceExpandHeight = true;
                v.childForceExpandWidth = true;
                if (withTitle)
                {
                    var title = PhoneUi.CreateLabel(tabs.transform, "Title", "Closet", 16f, FontStyles.Normal, TextAlignmentOptions.Center);
                    PhoneUi.Size(title.gameObject, 24f);
                    var tLe = title.gameObject.GetComponent<LayoutElement>();
                    if (tLe != null)
                        tLe.flexibleHeight = 0f;
                }
                for (int i = 0; i < _tabTypes.Length; i++)
                {
                    int captured = i;
                    _tabBtns[i] = MakeTab(tabs.transform, _tabLabels[i], PassportIcon(_tabTypes[i]), () => PickTab(captured));
                }
                MakeTab(tabs.transform, "Random", null, Randomize);
            }

            private Button MakeTab(Transform parent, string label, Sprite icon, UnityEngine.Events.UnityAction click)
            {
                Button btn = icon != null
                    ? PhoneUi.CreateIconChip(parent, label, icon, click, false, new Vector2(72f, 36f))
                    : PhoneUi.CreateButton(parent, label, click, new Vector2(88f, 28f));
                var le = btn.GetComponent<LayoutElement>();
                if (le != null)
                {
                    le.minWidth = 0f;
                    le.preferredWidth = 0f;
                    le.minHeight = 32f;
                    le.preferredHeight = 36f;
                    le.flexibleWidth = 1f;
                    le.flexibleHeight = 1f;
                }
                Transform art = btn.transform.Find("I");
                if (art != null)
                {
                    var iconImg = art.GetComponent<Image>();
                    if (iconImg != null)
                    {
                        iconImg.type = Image.Type.Simple;
                        iconImg.color = Color.white;
                        iconImg.preserveAspect = true;
                    }
                }
                var tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null)
                    tmp.fontSize = 13f;
                StyleFill(btn, false);
                return btn;
            }

            private static Sprite PassportIcon(Customization.Type type)
            {
                Sprite sprite;
                return _passportIcons.TryGetValue((int)type, out sprite) ? sprite : null;
            }

            private static void EnsurePassportIcons()
            {
                PassportTab[] tabs = null;
                PassportManager manager = PassportManager.instance;
                if (manager != null)
                    tabs = manager.tabs;
                if (tabs == null || tabs.Length == 0)
                    tabs = UnityEngine.Object.FindObjectsByType<PassportTab>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (tabs == null)
                    return;
                for (int i = 0; i < tabs.Length; i++)
                {
                    PassportTab tab = tabs[i];
                    if (tab == null)
                        continue;
                    int key = (int)tab.type;
                    if (_passportIcons.ContainsKey(key))
                        continue;
                    string note;
                    Sprite sprite = PassportTabSprite(tab, out note);
                    if (!_passportLogged)
                        Plugin.LogInfo("Closet tab " + tab.type + " -> " + note);
                    if (sprite != null)
                        _passportIcons[key] = sprite;
                }
                _passportLogged = true;
            }

            private static Sprite PassportTabSprite(PassportTab tab, out string note)
            {
                note = "none";
                RawImage[] raws = tab.GetComponentsInChildren<RawImage>(true);
                for (int i = 0; i < raws.Length; i++)
                {
                    RawImage raw = raws[i];
                    Texture2D tex = raw != null ? raw.texture as Texture2D : null;
                    if (tex == null || ChromeName(tex.name) || tex.width < 8 || tex.height < 8)
                        continue;
                    Sprite made = SpriteFrom(tex, raw.uvRect);
                    if (made == null)
                        continue;
                    note = "raw " + raw.gameObject.name + " " + tex.name + " " + tex.width + "x" + tex.height;
                    return made;
                }

                SpriteRenderer[] renderers = tab.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    SpriteRenderer renderer = renderers[i];
                    if (renderer == null || ChromeSprite(renderer.sprite))
                        continue;
                    note = "renderer " + renderer.gameObject.name + " " + renderer.sprite.name;
                    return renderer.sprite;
                }

                Image[] images = tab.GetComponentsInChildren<Image>(true);
                Sprite named = null;
                string namedNote = null;
                Sprite smallest = null;
                string smallestNote = null;
                float smallestArea = float.MaxValue;
                for (int i = 0; i < images.Length; i++)
                {
                    Image img = images[i];
                    if (img == null || img.gameObject == tab.gameObject)
                        continue;
                    Texture2D matTex = img.material != null ? img.material.mainTexture as Texture2D : null;
                    if (matTex != null && img.material != img.defaultMaterial && !ChromeName(matTex.name) && matTex.width >= 8 && matTex.height >= 8 && matTex.width <= 512)
                    {
                        Sprite fromMat = SpriteFrom(matTex, new Rect(0f, 0f, 1f, 1f));
                        if (fromMat != null)
                        {
                            note = "material " + img.gameObject.name + " " + matTex.name + " " + matTex.width + "x" + matTex.height;
                            return fromMat;
                        }
                    }
                    if (img.sprite == null || img.type == Image.Type.Sliced || ChromeSprite(img.sprite))
                        continue;
                    string n = img.gameObject.name ?? string.Empty;
                    string imgNote = "image " + n + " spr=" + img.sprite.name + " tex=" + (img.sprite.texture != null ? img.sprite.texture.name : "null");
                    if (n.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("glyph", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        named = img.sprite;
                        namedNote = imgNote;
                    }
                    float area = img.sprite.rect.width * img.sprite.rect.height;
                    if (area >= 16f && area < smallestArea)
                    {
                        smallestArea = area;
                        smallest = img.sprite;
                        smallestNote = imgNote;
                    }
                }
                if (named != null)
                {
                    note = namedNote;
                    return named;
                }
                if (smallest != null)
                {
                    note = smallestNote;
                    return smallest;
                }
                if (raws.Length == 0 && images.Length == 0)
                    note = "no graphics";
                else
                    note = "skipped " + raws.Length + " raw, " + images.Length + " image";
                return null;
            }

            private static bool ChromeSprite(Sprite sprite)
            {
                if (sprite == null || sprite.texture == null)
                    return true;
                if (sprite.texture.width <= 8 && sprite.texture.height <= 8)
                    return true;
                return ChromeName(sprite.name) || ChromeName(sprite.texture.name);
            }

            private static bool ChromeName(string name)
            {
                if (string.IsNullOrEmpty(name))
                    return false;
                return name == "UISprite"
                    || name == "Background"
                    || name == "Knob"
                    || name == "UIMask"
                    || name == "InputFieldBackground"
                    || name == "UnityWhite"
                    || name.StartsWith("Unity", StringComparison.Ordinal);
            }

            private static Sprite SpriteFrom(Texture2D tex, Rect uv)
            {
                float x = uv.x * tex.width;
                float y = uv.y * tex.height;
                float w = uv.width * tex.width;
                float h = uv.height * tex.height;
                if (w < 8f || h < 8f)
                {
                    x = 0f;
                    y = 0f;
                    w = tex.width;
                    h = tex.height;
                }
                try
                {
                    Sprite sprite = Sprite.Create(tex, new Rect(x, y, w, h), new Vector2(0.5f, 0.5f), 100f);
                    sprite.hideFlags = HideFlags.HideAndDontSave;
                    sprite.name = tex.name;
                    return sprite;
                }
                catch (Exception ex)
                {
                    Plugin.LogInfo("Closet icon failed: " + ex.Message);
                    return null;
                }
            }

            private void BuildGrid(Transform parent, float flex)
            {
                _scroll = PhoneUi.CreateScrollView(parent, out _optionContent);
                _scroll.movementType = ScrollRect.MovementType.Elastic;
                _scroll.inertia = true;
                _scroll.scrollSensitivity = 14f;
                var sle = _scroll.gameObject.AddComponent<LayoutElement>();
                sle.flexibleHeight = flex;
                sle.flexibleWidth = 1f;
                sle.minHeight = 96f;
                sle.minWidth = 120f;
                sle.preferredHeight = 0f;
                sle.layoutPriority = 2;
                _optionGrid = _optionContent.gameObject.AddComponent<GridLayoutGroup>();
                _optionGrid.spacing = new Vector2(6f, 6f);
                _optionGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                _optionGrid.constraintCount = 4;
                _optionGrid.padding = new RectOffset(4, 4, 4, 4);
                _optionGrid.childAlignment = TextAnchor.UpperLeft;
                PhoneUi.FitVertical(_optionContent.gameObject);
                SizeCells();
            }

            private static RectTransform Pane(Transform parent, string name, float flex, float minHeight)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var le = go.AddComponent<LayoutElement>();
                le.flexibleHeight = flex;
                le.flexibleWidth = 1f;
                le.minHeight = minHeight;
                return go.GetComponent<RectTransform>();
            }

            private void SizeCells()
            {
                if (_optionGrid == null)
                    return;
                float w = PhoneUi.ContentWidth() - 20f;
                if (_host.IsLandscape)
                    w = w - 150f - 92f - 24f;
                if (w < 160f)
                    w = 160f;
                float cell = Mathf.Floor((w - 18f) / 4f);
                if (cell < 52f)
                    cell = 52f;
                if (cell > 92f)
                    cell = 92f;
                _optionGrid.cellSize = new Vector2(cell, cell + 12f);
            }

            private void PickTab(int index)
            {
                _type = _tabTypes[index];
                PaintTabs();
                FillOptions();
            }

            private void PaintTabs()
            {
                for (int i = 0; i < _tabBtns.Length; i++)
                {
                    if (_tabBtns[i] != null)
                        StyleFill(_tabBtns[i], _tabTypes[i] == _type);
                }
            }

            private static void StyleFill(Button btn, bool on)
            {
                if (btn == null)
                    return;
                var img = btn.GetComponent<Image>();
                if (img != null)
                    img.color = on ? PhoneUi.Accent : PhoneUi.SurfaceAlt;
                var tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null)
                    tmp.color = on ? new Color(0.10f, 0.11f, 0.12f, 1f) : PhoneUi.ButtonText;
            }

            private void FillOptions()
            {
                if (_optionContent == null)
                    return;
                ClosetCatalog.Ensure();
                SizeCells();
                for (int i = _optionContent.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(_optionContent.GetChild(i).gameObject);
                CustomizationOption[] list = GetList(_type);
                bool customSkin = _type == Customization.Type.Skin && ClosetSkinSliders.Available;
                if (_optionGrid != null)
                    _optionGrid.enabled = true;
                if ((list == null || list.Length == 0) && !customSkin)
                {
                    var emptyLab = PhoneUi.CreateLabel(_optionContent, "E", "No cosmetics loaded yet. Join a game first.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                    emptyLab.color = PhoneUi.TextDim;
                    PhoneUi.Size(emptyLab.gameObject, 40f);
                    RefreshScroll(40f);
                    return;
                }
                int extra = customSkin ? 1 : 0;
                if (customSkin)
                    AddCustomSkinTile();
                int current = ClosetCatalog.ListIndex(_type, CurrentIndex(_type));
                Material eyeMat = _type == Customization.Type.Eyes ? EyeMaterial() : null;
                int count = list != null ? list.Length : 0;
                for (int i = 0; i < count; i++)
                {
                    CustomizationOption opt = list[i];
                    int index = i;
                    bool locked = opt != null && OptionLocked(opt);
                    bool on = index == current;
                    var tile = PhoneUi.CreateImage(_optionContent, "O", PhoneUi.Rounded(14), on ? PhoneUi.Accent : PhoneUi.SurfaceAlt);
                    if (opt != null && opt.texture != null)
                    {
                        var rawGo = new GameObject("T", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                        rawGo.transform.SetParent(tile, false);
                        PhoneUi.Stretch(rawGo.GetComponent<RectTransform>(), 6f, 18f);
                        var raw = rawGo.GetComponent<RawImage>();
                        raw.texture = opt.texture;
                        raw.raycastTarget = false;
                        if (_type == Customization.Type.Skin)
                            raw.color = opt.color.a > 0.05f ? opt.color : Color.white;
                        else
                            raw.color = Color.white;
                        if (eyeMat != null)
                            raw.material = eyeMat;
                    }
                    else
                    {
                        var swatch = PhoneUi.CreateImage(tile, "S", PhoneUi.Rounded(8), Swatch(opt));
                        PhoneUi.Stretch(swatch, 10f, 20f);
                        swatch.GetComponent<Image>().raycastTarget = false;
                    }
                    string name = OptionName(opt, index, locked);
                    var lab = PhoneUi.CreateLabel(tile, "L", name, 11f, FontStyles.Normal, TextAlignmentOptions.Bottom);
                    PhoneUi.Stretch(lab.rectTransform, 4f, 3f);
                    lab.color = locked
                        ? PhoneUi.TextDim
                        : (on ? new Color(0.10f, 0.11f, 0.12f, 1f) : PhoneUi.ButtonText);
                    var btn = tile.gameObject.AddComponent<Button>();
                    btn.transition = Selectable.Transition.None;
                    btn.targetGraphic = tile.GetComponent<Image>();
                    btn.onClick.AddListener(() => Pick(_type, index, locked));
                }
                int rows = (count + extra + 3) / 4;
                float cellH = _optionGrid != null ? _optionGrid.cellSize.y : 72f;
                float gap = _optionGrid != null ? _optionGrid.spacing.y : 6f;
                RefreshScroll(8f + rows * (cellH + gap));
            }

            private void AddCustomSkinTile()
            {
                IPiPhoneHost host = _host;
                var tile = PhoneUi.CreateImage(_optionContent, "Custom", PhoneUi.Rounded(14), PhoneUi.SurfaceAlt);
                Sprite art = PhoneIcons.Material("color_lens");
                if (art != null)
                {
                    var icon = PhoneUi.CreateImage(tile, "Art", art, Color.white);
                    PhoneUi.Stretch(icon, 4f, 16f);
                    var img = icon.GetComponent<Image>();
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                    img.type = Image.Type.Simple;
                }
                var lab = PhoneUi.CreateLabel(tile, "L", "custom", 11f, FontStyles.Normal, TextAlignmentOptions.Bottom);
                PhoneUi.Stretch(lab.rectTransform, 4f, 3f);
                lab.color = PhoneUi.ButtonText;
                var btn = tile.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = tile.GetComponent<Image>();
                btn.onClick.AddListener(() => ClosetSkinSliders.Open(host));
            }

            private void RefreshScroll(float contentHeight)
            {
                if (_optionContent == null)
                    return;
                _optionContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(40f, contentHeight));
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_optionContent);
                if (_scroll != null)
                {
                    _scroll.verticalNormalizedPosition = 1f;
                    LayoutRebuilder.ForceRebuildLayoutImmediate(_scroll.GetComponent<RectTransform>());
                }
            }

            private void Pick(Customization.Type type, int index, bool locked)
            {
                if (locked)
                {
                    _host.ShowToast("That one's locked.");
                    return;
                }
                CustomizationOption[] list = GetList(type);
                CustomizationOption opt = (list != null && index >= 0 && index < list.Length) ? list[index] : null;
                if (!Apply(type, ClosetCatalog.ApplyIndex(type, index), opt))
                    return;
                FillOptions();
            }

            private void Randomize()
            {
                CharacterCustomization cc = LocalCustomization();
                if (cc == null)
                {
                    _host.ShowToast("Join a game first.");
                    return;
                }
                try
                {
                    cc.RandomizeCosmetics();
                    FillOptions();
                    _host.ShowToast("Shuffled.");
                }
                catch (Exception ex)
                {
                    LogOnce("Closet randomize failed: " + ex.Message);
                    _host.ShowToast("Couldn't randomize.");
                }
            }

            private bool Apply(Customization.Type type, int index, CustomizationOption option)
            {
                try
                {
                    if (PhotonNetwork.LocalPlayer == null)
                    {
                        _host.ShowToast("Join a game first.");
                        return false;
                    }
                    if (type == Customization.Type.Skin)
                    {
                        CharacterCustomization.SetCharacterSkinColor(index);
                        ClosetSkinSliders.ApplyPresetSkin(option, index);
                    }
                    else if (type == Customization.Type.Eyes)
                        CharacterCustomization.SetCharacterEyes(index);
                    else if (type == Customization.Type.Mouth)
                        CharacterCustomization.SetCharacterMouth(index);
                    else if (type == Customization.Type.Accessory)
                        CharacterCustomization.SetCharacterAccessory(index);
                    else if (type == Customization.Type.Fit)
                        CharacterCustomization.SetCharacterOutfit(index);
                    else if (type == Customization.Type.Hat)
                        CharacterCustomization.SetCharacterHat(index);
                    else if (type == Customization.Type.Sash)
                    {
                        CharacterCustomization.SetCharacterSash(index);
                        ClosetSkinSliders.ApplyPresetSash(index, option);
                    }
                    else if (type == Customization.Type.Medal)
                        CharacterCustomization.SetCharacterMedal(index);
                    return true;
                }
                catch (Exception ex)
                {
                    LogOnce("Closet apply failed: " + ex.Message);
                    _host.ShowToast("Couldn't change that.");
                    return false;
                }
            }

            private static CharacterCustomization LocalCustomization()
            {
                Character ch = Character.localCharacter;
                if (ch == null || ch.refs == null)
                    return null;
                return ch.refs.customization;
            }

            private int CurrentIndex(Customization.Type type)
            {
                try
                {
                    CharacterCustomizationData d = CharacterCustomization.GetCustomizationData(PhotonNetwork.LocalPlayer);
                    if (d == null)
                        return 0;
                    if (type == Customization.Type.Skin) return d.currentSkin;
                    if (type == Customization.Type.Eyes) return d.currentEyes;
                    if (type == Customization.Type.Mouth) return d.currentMouth;
                    if (type == Customization.Type.Accessory) return d.currentAccessory;
                    if (type == Customization.Type.Fit) return d.currentOutfit;
                    if (type == Customization.Type.Hat) return d.currentHat;
                    if (type == Customization.Type.Sash) return d.currentSash;
                    if (type == Customization.Type.Medal) return d.currentMedal;
                }
                catch (Exception ex)
                {
                    LogOnce("Closet current index failed: " + ex.Message);
                }
                return 0;
            }

            private CustomizationOption[] GetList(Customization.Type type)
            {
                try
                {
                    Customization catalog = Customization.Instance;
                    if (catalog == null)
                        return null;
                    return catalog.GetList(type);
                }
                catch (Exception ex)
                {
                    LogOnce("Closet catalog failed: " + ex.Message);
                    return null;
                }
            }

            private static bool OptionLocked(CustomizationOption opt)
            {
                try
                {
                    return opt.IsLocked;
                }
                catch
                {
                    return false;
                }
            }

            private static string OptionName(CustomizationOption opt, int index, bool locked)
            {
                string n;
                if (opt == null)
                    n = "#" + (index + 1);
                else if (opt.isBlank)
                    n = "None";
                else if (!string.IsNullOrEmpty(opt.name))
                    n = ClosetCatalog.PrettyName(opt.name);
                else
                    n = "#" + (index + 1);
                if (n.Length > 14)
                    n = n.Substring(0, 14);
                if (locked)
                    n = "x " + n;
                return n;
            }

            private static Color Swatch(CustomizationOption opt)
            {
                if (opt == null)
                    return PhoneUi.Surface;
                if (opt.color.a > 0.05f)
                    return opt.color;
                return PhoneUi.Surface;
            }

            private static Material EyeMaterial()
            {
                if (_eyeMat != null)
                    return _eyeMat;
                try
                {
                    PassportButton[] live = UnityEngine.Object.FindObjectsByType<PassportButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    _eyeMat = FirstEyeMat(live);
                    if (_eyeMat == null)
                    {
                        PassportManager pm = UnityEngine.Object.FindFirstObjectByType<PassportManager>(FindObjectsInactive.Include);
                        if (pm != null)
                            _eyeMat = FirstEyeMat(pm.GetComponentsInChildren<PassportButton>(true));
                    }
                    if (_eyeMat == null)
                        _eyeMat = FirstEyeMat(Resources.FindObjectsOfTypeAll<PassportButton>());
                }
                catch (Exception ex)
                {
                    Plugin.LogError("Closet eye preview: " + ex.Message);
                }
                return _eyeMat;
            }

            private static Material FirstEyeMat(PassportButton[] buttons)
            {
                if (buttons == null)
                    return null;
                for (int i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i] != null && buttons[i].eyeMaterial != null)
                        return buttons[i].eyeMaterial;
                }
                return null;
            }

            private void EnsureCam()
            {
                DestroyCam();
                _rt = new RenderTexture(RtWidth, RtHeight, 16, RenderTextureFormat.ARGB32);
                _rt.antiAliasing = 1;
                _rt.Create();
                var go = new GameObject("PiP_ClosetCam");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _cam = go.AddComponent<Camera>();
                _cam.enabled = true;
                _cam.allowMSAA = false;
                _cam.allowHDR = false;
                _cam.depth = -82;
                _cam.nearClipPlane = 0.12f;
                _cam.farClipPlane = 2000f;
                _cam.targetTexture = _rt;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 1f);
                var follow = go.AddComponent<ClosetFollow>();
                follow.Session = this;
                if (_preview != null)
                    _preview.texture = _rt;
            }

            private void DestroyCam()
            {
                if (_cam != null)
                {
                    UnityEngine.Object.Destroy(_cam.gameObject);
                    _cam = null;
                }
                if (_rt != null)
                {
                    _rt.Release();
                    UnityEngine.Object.Destroy(_rt);
                    _rt = null;
                }
            }

            internal void PlaceCam(Camera cam)
            {
                Character local = Character.localCharacter;
                Camera main = Camera.main;
                if (local != null && local.refs != null && local.refs.head != null)
                {
                    Transform head = local.refs.head.transform;
                    bool bust = _host.IsLandscape || _type == Customization.Type.Fit || _type == Customization.Type.Hat || _type == Customization.Type.Sash || _type == Customization.Type.Medal;
                    float dist = (bust ? 2.35f : 1.5f) / _camZoom;
                    Vector3 look = head.position + Vector3.up * ((bust ? -0.12f : 0f) + _camLift);
                    Vector3 flat = head.forward;
                    flat.y = 0f;
                    if (flat.sqrMagnitude < 0.0001f)
                        flat = Vector3.forward;
                    flat.Normalize();
                    Vector3 orbitFwd = Quaternion.AngleAxis(_camOrbit, Vector3.up) * flat;
                    cam.transform.position = look + orbitFwd * dist;
                    cam.transform.LookAt(look);
                    cam.fieldOfView = bust ? 42f : 48f;
                }
                else if (main != null)
                {
                    cam.transform.position = main.transform.position + main.transform.forward * 0.28f;
                    cam.transform.rotation = main.transform.rotation;
                    cam.fieldOfView = 48f;
                }
                if (main != null)
                {
                    cam.cullingMask = main.cullingMask;
                    cam.farClipPlane = main.farClipPlane;
                }
                cam.targetTexture = _rt;
                if (_rt != null)
                    cam.aspect = (float)_rt.width / _rt.height;
            }

            private void LogOnce(string message)
            {
                if (_logged)
                    return;
                _logged = true;
                Plugin.LogError(message);
            }
        }

        private sealed class ClosetFollow : MonoBehaviour
        {
            public Session Session;
            private bool _logged;

            private void LateUpdate()
            {
                try
                {
                    Camera cam = GetComponent<Camera>();
                    if (cam == null || Session == null)
                        return;
                    Session.PlaceCam(cam);
                }
                catch (Exception ex)
                {
                    if (_logged)
                        return;
                    _logged = true;
                    Plugin.LogError("Closet camera failed: " + ex.Message);
                }
            }
        }
    }
}
