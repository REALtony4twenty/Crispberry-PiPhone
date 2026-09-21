using System;
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
                if (_page == "list")
                    ShowList();
            }

            public void ShowList()
            {
                _page = "list";
                Clear();
                _host.SetTitle("Apps");

                var hint = PhoneUi.CreateLabel(_host.Content, "Hint", "Install apps onto the phone. Core apps stay. Other built-ins and mods can be uninstalled.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 48f);

                var search = PhoneUi.CreateInput(_host.Content, "Search apps");
                search.onValueChanged.AddListener(v =>
                {
                    _filter = v ?? string.Empty;
                    FillRows();
                });

                var scroll = PhoneUi.CreateScrollView(_host.Content, out RectTransform content);
                var le = scroll.GetComponent<LayoutElement>() ?? scroll.gameObject.AddComponent<LayoutElement>();
                le.flexibleHeight = 1f;
                le.minHeight = 280f;
                if (_host.IsLandscape)
                {
                    var grid = content.gameObject.AddComponent<GridLayoutGroup>();
                    grid.cellSize = new Vector2(390f, 64f);
                    grid.spacing = new Vector2(8f, 8f);
                    grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                    grid.constraintCount = 2;
                    grid.padding = new RectOffset(4, 4, 4, 12);
                    grid.childAlignment = TextAnchor.UpperLeft;
                }
                else
                    PhoneUi.AddVertical(content.gameObject, 8f, new RectOffset(4, 4, 4, 12));
                PhoneUi.FitVertical(content.gameObject);
                _rows = content;
                FillRows();
            }

            private RectTransform _rows;

            private void FillRows()
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
                    if (!string.IsNullOrEmpty(q) && (app.DisplayName ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0
                        && (app.Id ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    AddRow(app);
                    shown++;
                }
                if (shown == 0)
                {
                    var empty = PhoneUi.CreateLabel(_rows, "Empty", string.IsNullOrEmpty(q) ? "No apps in the store yet." : "No matching apps.", 14f, FontStyles.Normal, TextAlignmentOptions.Center);
                    empty.color = PhoneUi.TextDim;
                    PhoneUi.Size(empty.gameObject, 36f);
                }
            }

            private void AddRow(PiPhoneApp app)
            {
                bool installed = PiPhoneApi.IsInstalled(app.Id);
                var row = PhoneUi.CreateImage(_rows, "Row", PhoneUi.Rounded(14), PhoneUi.SurfaceAlt);
                PhoneUi.Size(row.gameObject, 64f);
                PhoneUi.AddHorizontal(row.gameObject, 10f);
                var h = row.GetComponent<HorizontalLayoutGroup>();
                h.padding = new RectOffset(10, 10, 8, 8);
                h.childAlignment = TextAnchor.MiddleLeft;
                h.childForceExpandWidth = false;
                h.childControlWidth = false;

                PhoneIcons.CreateView(row.transform, app, 44f, false);

                var textCol = new GameObject("T", typeof(RectTransform));
                textCol.transform.SetParent(row, false);
                var tle = textCol.AddComponent<LayoutElement>();
                tle.flexibleWidth = 1f;
                tle.minWidth = 80f;
                PhoneUi.AddVertical(textCol, 0f, new RectOffset(0, 0, 0, 0));
                var name = PhoneUi.CreateLabel(textCol.transform, "N", app.DisplayName ?? app.Id, 15f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                PhoneUi.Size(name.gameObject, 22f);
                var sub = PhoneUi.CreateLabel(textCol.transform, "S", Publisher(app) + (installed ? "  ·  Installed" : "  ·  Free"), 12f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                sub.color = PhoneUi.TextDim;
                PhoneUi.Size(sub.gameObject, 18f);

                if (installed)
                {
                    if (PiPhoneApi.CanUninstall(app.Id))
                        PhoneUi.CreateButton(row.transform, "Uninstall", () =>
                        {
                            PiPhoneApi.Uninstall(app.Id);
                            ShowList();
                        }, new Vector2(96f, 36f));
                    PhoneUi.CreateButton(row.transform, "Open", () => PhoneMenu.OpenApp(app.Id), new Vector2(72f, 36f));
                }
                else
                {
                    PhoneUi.CreateButton(row.transform, "Install", () =>
                    {
                        PiPhoneApi.Install(app.Id);
                        _host.ShowToast("Installed. Open it from All apps.");
                        ShowList();
                    }, new Vector2(88f, 36f));
                }
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
