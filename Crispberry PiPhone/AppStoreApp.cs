using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    internal static class AppStoreApp
    {
        private static Session _live;

        internal static void Register()
        {
            PiPhoneApi.RegisterApp(new PiPhoneApp
            {
                Id = BuiltinApps.StoreId,
                DisplayName = "Apps",
                IconGlyph = "P",
                IconBackground = new Color(0.12f, 0.62f, 0.38f, 1f),
                SortOrder = 8,
                ShowOnHome = true,
                Preinstalled = true,
                Sticky = true,
                ListedInStore = false,
                OnOpen = host => { _live = new Session(host); _live.ShowList(); },
                OnClose = () => { _live = null; },
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
            private string _page = "list";
            private string _filter = string.Empty;
            private string _category = string.Empty;
            private string _detailId;
            private readonly List<CatChip> _chips = new List<CatChip>();

            public Session(IPiPhoneHost host)
            {
                _host = host;
            }

            public bool GoBack()
            {
                if (_page == "list")
                    return false;
                ShowList();
                return true;
            }

            public void Relayout()
            {
                if (_page == "detail")
                    ShowDetail(_detailId);
                else
                    ShowList();
            }

            public void ShowList()
            {
                _page = "list";
                _detailId = null;
                _chips.Clear();
                Clear();
                _host.SetTitle("Apps");

                var search = PhoneUi.CreateInput(_host.Content, "Search apps");
                search.text = _filter ?? string.Empty;
                search.onValueChanged.AddListener(v =>
                {
                    _filter = v ?? string.Empty;
                    FillGrid();
                });

                var catScroll = PhoneUi.CreateScrollView(_host.Content, out RectTransform catContent);
                catScroll.horizontal = true;
                catScroll.vertical = false;
                catScroll.movementType = ScrollRect.MovementType.Clamped;
                PhoneUi.Size(catScroll.gameObject, 44f);
                catContent.anchorMin = new Vector2(0f, 0f);
                catContent.anchorMax = new Vector2(0f, 1f);
                catContent.pivot = new Vector2(0f, 0.5f);
                catContent.sizeDelta = new Vector2(0f, 0f);
                var catRow = PhoneUi.AddHorizontal(catContent.gameObject, 8f);
                catRow.childForceExpandWidth = false;
                catRow.childForceExpandHeight = false;
                catRow.childControlWidth = true;
                catRow.childControlHeight = true;
                catRow.childAlignment = TextAnchor.MiddleLeft;
                catRow.padding = new RectOffset(4, 12, 4, 4);
                var catFit = catContent.gameObject.AddComponent<ContentSizeFitter>();
                catFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                catFit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                CategoryChip(catContent, string.Empty, "All");
                PiPhoneCategory[] categories = PiPhoneApi.GetCategories();
                for (int i = 0; i < categories.Length; i++)
                {
                    PiPhoneCategory cat = categories[i];
                    if (cat == null || string.IsNullOrEmpty(cat.Id))
                        continue;
                    if (!CategoryHasApps(cat.Id))
                        continue;
                    CategoryChip(catContent, cat.Id, cat.DisplayName);
                }

                var scroll = PhoneUi.CreateScrollView(_host.Content, out RectTransform content);
                var le = scroll.GetComponent<LayoutElement>() ?? scroll.gameObject.AddComponent<LayoutElement>();
                le.flexibleHeight = 1f;
                le.minHeight = 220f;
                int cols = _host.IsLandscape ? 6 : 4;
                float cell = Mathf.Floor((PhoneUi.ContentWidth() - 28f) / cols);
                if (cell < 64f)
                    cell = 64f;
                if (cell > 96f)
                    cell = 96f;
                var grid = content.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(cell, cell + 22f);
                grid.spacing = new Vector2(8f, 8f);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = cols;
                grid.padding = new RectOffset(4, 4, 4, 12);
                grid.childAlignment = TextAnchor.UpperCenter;
                PhoneUi.FitVertical(content.gameObject);
                _rows = content;
                FillGrid();
            }

            private void CategoryChip(Transform parent, string id, string label)
            {
                bool on = string.Equals(_category ?? string.Empty, id ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                string captured = id ?? string.Empty;
                Button btn = PhoneUi.CreateButton(parent, label, () => SelectCategory(captured), new Vector2(72f, 32f));
                var img = btn.GetComponent<Image>();
                if (img != null)
                    img.color = on ? PhoneUi.Accent : PhoneUi.SurfaceAlt;
                var tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null)
                {
                    tmp.fontSize = 14f;
                    tmp.color = on ? new Color(0.10f, 0.11f, 0.12f, 1f) : PhoneUi.ButtonText;
#pragma warning disable CS0618
                    tmp.enableWordWrapping = false;
#pragma warning restore CS0618
                    tmp.overflowMode = TextOverflowModes.Overflow;
                    float textW = tmp.GetPreferredValues(label, 800f, 32f).x;
                    float w = Mathf.Ceil(textW + 28f);
                    if (w < 64f)
                        w = 64f;
                    PhoneUi.ApplySize(btn.GetComponent<RectTransform>(), new Vector2(w, 32f));
                    var le = btn.GetComponent<LayoutElement>() ?? btn.gameObject.AddComponent<LayoutElement>();
                    le.minWidth = w;
                    le.preferredWidth = w;
                    le.flexibleWidth = 0f;
                    le.minHeight = 32f;
                    le.preferredHeight = 32f;
                    le.flexibleHeight = 0f;
                }
                _chips.Add(new CatChip
                {
                    Id = captured,
                    Image = img,
                    Label = tmp
                });
            }

            private void SelectCategory(string id)
            {
                _category = id ?? string.Empty;
                for (int i = 0; i < _chips.Count; i++)
                {
                    CatChip chip = _chips[i];
                    if (chip == null)
                        continue;
                    bool on = string.Equals(_category, chip.Id ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                    if (chip.Image != null)
                        chip.Image.color = on ? PhoneUi.Accent : PhoneUi.SurfaceAlt;
                    if (chip.Label != null)
                        chip.Label.color = on ? new Color(0.10f, 0.11f, 0.12f, 1f) : PhoneUi.ButtonText;
                }
                FillGrid();
            }

            private static bool CategoryHasApps(string categoryId)
            {
                PiPhoneApp[] apps = PiPhoneApi.GetStoreApps();
                for (int i = 0; i < apps.Length; i++)
                {
                    PiPhoneApp app = apps[i];
                    if (app != null && string.Equals(app.Category, categoryId, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }

            private sealed class CatChip
            {
                public string Id;
                public Image Image;
                public TextMeshProUGUI Label;
            }

            private RectTransform _rows;

            private void FillGrid()
            {
                if (_rows == null)
                    return;
                for (int i = _rows.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(_rows.GetChild(i).gameObject);

                PiPhoneApp[] apps = PiPhoneApi.GetStoreApps();
                string q = (_filter ?? string.Empty).Trim();
                int shown = 0;
                for (int i = 0; i < apps.Length; i++)
                {
                    PiPhoneApp app = apps[i];
                    if (app == null)
                        continue;
                    if (!string.IsNullOrEmpty(_category) && !string.Equals(app.Category, _category, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!string.IsNullOrEmpty(q) && (app.DisplayName ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0
                        && (app.Id ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0
                        && (app.Description ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    AddTile(app);
                    shown++;
                }
                if (shown == 0)
                {
                    var empty = PhoneUi.CreateLabel(_rows, "Empty", string.IsNullOrEmpty(q) ? "No apps in this category." : "No matching apps.", 14f, FontStyles.Normal, TextAlignmentOptions.Center);
                    empty.color = PhoneUi.TextDim;
                    PhoneUi.Size(empty.gameObject, 36f);
                }
            }

            private void AddTile(PiPhoneApp app)
            {
                string id = app.Id;
                var tile = new GameObject("App", typeof(RectTransform));
                tile.transform.SetParent(_rows, false);
                var v = PhoneUi.AddVertical(tile, 4f, new RectOffset(0, 0, 2, 0));
                v.childAlignment = TextAnchor.UpperCenter;
                v.childForceExpandWidth = false;
                v.childControlWidth = false;
                var icon = PhoneIcons.CreateView(tile.transform, app, 52f, false);
                var btn = icon.gameObject.AddComponent<Button>();
                btn.targetGraphic = icon.GetComponent<Image>();
                btn.onClick.AddListener(() => ShowDetail(id));
                var name = PhoneUi.CreateLabel(tile.transform, "N", app.DisplayName ?? app.Id, 12f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Size(name.gameObject, 18f, 72f);
                name.overflowMode = TextOverflowModes.Ellipsis;
            }

            private void ShowDetail(string id)
            {
                PiPhoneApp app;
                if (string.IsNullOrEmpty(id) || !PiPhoneApi.TryGetApp(id, out app) || app == null)
                {
                    ShowList();
                    return;
                }
                _page = "detail";
                _detailId = id;
                Clear();
                _host.SetTitle(app.DisplayName ?? "App");
                PhoneUi.MaterialChip(_host.Content, "arrow_back", "Back", ShowList, new Vector2(36f, 32f));

                var pageScroll = PhoneUi.CreateScrollView(_host.Content, out var page);
                var pageLe = pageScroll.gameObject.GetComponent<LayoutElement>() ?? pageScroll.gameObject.AddComponent<LayoutElement>();
                pageLe.flexibleWidth = 1f;
                pageLe.flexibleHeight = 1f;
                pageLe.minHeight = 160f;
                pageLe.minWidth = 0f;
                PhoneUi.AddVertical(page.gameObject, 8f, new RectOffset(0, 0, 0, 12));
                var pageFit = page.gameObject.AddComponent<ContentSizeFitter>();
                pageFit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                pageFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var head = new GameObject("Head", typeof(RectTransform));
                head.transform.SetParent(page, false);
                PhoneUi.Size(head, 72f);
                var row = PhoneUi.AddHorizontal(head, 10f);
                row.childAlignment = TextAnchor.MiddleLeft;
                row.childForceExpandWidth = false;
                row.padding = new RectOffset(4, 4, 4, 4);
                PhoneIcons.CreateView(head.transform, app, 56f, false);
                var textCol = new GameObject("T", typeof(RectTransform));
                textCol.transform.SetParent(head.transform, false);
                textCol.AddComponent<LayoutElement>().flexibleWidth = 1f;
                PhoneUi.AddVertical(textCol, 2f, new RectOffset(0, 0, 8, 0));
                var name = PhoneUi.CreateLabel(textCol.transform, "N", app.DisplayName ?? app.Id, 18f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                name.overflowMode = TextOverflowModes.Ellipsis;
                PhoneUi.Size(name.gameObject, 24f);
                bool installed = PiPhoneApi.IsInstalled(app.Id);
                var sub = PhoneUi.CreateLabel(textCol.transform, "S", Publisher(app) + (installed ? "  ·  Installed" : "  ·  Free"), 13f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                sub.color = PhoneUi.TextDim;
                PhoneUi.Size(sub.gameObject, 18f);

                var actions = new GameObject("A", typeof(RectTransform));
                actions.transform.SetParent(head.transform, false);
                var actionRow = PhoneUi.AddHorizontal(actions, 6f);
                actionRow.childForceExpandWidth = false;
                actionRow.childForceExpandHeight = false;
                actionRow.childAlignment = TextAnchor.MiddleRight;
                var actionLe = actions.AddComponent<LayoutElement>();
                actionLe.flexibleWidth = 0f;
                actionLe.minHeight = 40f;
                actionLe.preferredHeight = 40f;
                int actionCount = installed && PiPhoneApi.CanUninstall(app.Id) ? 2 : 1;
                float actionW = actionCount * 36f + (actionCount - 1) * 6f;
                actionLe.minWidth = actionW;
                actionLe.preferredWidth = actionW;
                if (installed)
                {
                    if (PiPhoneApi.CanUninstall(app.Id))
                        PhoneUi.MaterialChip(actions.transform, "delete", "Uninstall", () =>
                        {
                            PiPhoneApi.Uninstall(app.Id);
                            ShowDetail(id);
                        }, new Vector2(36f, 32f));
                    PhoneUi.MaterialChip(actions.transform, "open_in_new", "Open", () => PhoneMenu.OpenApp(app.Id), new Vector2(36f, 32f));
                }
                else
                {
                    PhoneUi.MaterialChip(actions.transform, "download", "Install", () =>
                    {
                        PiPhoneApi.Install(app.Id);
                        _host.ShowToast("Installed. Open it from All apps.");
                        ShowDetail(id);
                    }, new Vector2(36f, 32f));
                }

                Sprite[] shots = app.Screenshots;
                if (shots != null && shots.Length > 0)
                {
                    var gallery = PhoneUi.CreateHorizontalScroll(page, out var strip);
                    PhoneUi.Size(gallery.gameObject, 150f);
                    var shotRow = PhoneUi.AddHorizontal(strip.gameObject, 8f);
                    shotRow.childForceExpandWidth = false;
                    shotRow.childControlWidth = true;
                    shotRow.childAlignment = TextAnchor.MiddleLeft;
                    shotRow.padding = new RectOffset(4, 4, 4, 4);
                    for (int i = 0; i < shots.Length; i++)
                    {
                        if (shots[i] == null)
                            continue;
                        var frame = PhoneUi.CreateImage(strip, "Shot", PhoneUi.Rounded(12), PhoneUi.SurfaceAlt);
                        PhoneUi.Size(frame.gameObject, 120f, 140f);
                        var art = PhoneUi.CreateImage(frame, "Art", shots[i], Color.white);
                        PhoneUi.Stretch(art, 4f, 4f);
                        var artImg = art.GetComponent<Image>();
                        artImg.preserveAspect = true;
                        artImg.raycastTarget = false;
                        artImg.type = Image.Type.Simple;
                    }
                }

                string body = string.IsNullOrEmpty(app.Description) ? "No description yet." : app.Description;
                var desc = PhoneUi.CreateLabel(page, "Desc", body, 15f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
                PhoneUi.Wrap(desc);
                desc.color = PhoneUi.Text;
                var descLe = desc.gameObject.GetComponent<LayoutElement>() ?? desc.gameObject.AddComponent<LayoutElement>();
                descLe.minWidth = 0f;
                descLe.preferredWidth = 0f;
                descLe.flexibleWidth = 1f;
                descLe.minHeight = 40f;
                var descFit = desc.gameObject.AddComponent<ContentSizeFitter>();
                descFit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                descFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            private static string Publisher(PiPhoneApp app)
            {
                if (app == null || string.IsNullOrEmpty(app.Id))
                    return "App";
                if (app.Id.StartsWith("pip.", StringComparison.OrdinalIgnoreCase))
                    return "PiPhone";
                int dot = app.Id.IndexOf('.');
                if (dot <= 0)
                    return "Other mods";
                return app.Id.Substring(0, dot);
            }

            private void Clear()
            {
                _rows = null;
                for (int i = _host.Content.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(_host.Content.GetChild(i).gameObject);
            }
        }
    }
}
