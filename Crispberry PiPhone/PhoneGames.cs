using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    internal static class PhoneGames
    {
        internal static bool TryGoBack()
        {
            return Game2048App.TryGoBack()
                || MinesweeperApp.TryGoBack()
                || SimonApp.TryGoBack()
                || TetrisApp.TryGoBack()
                || BreakoutApp.TryGoBack()
                || Connect4App.TryGoBack()
                || SudokuApp.TryGoBack()
                || SolitaireApp.TryGoBack();
        }

        public static bool Down(KeyCode key)
        {
            return key != KeyCode.None && Input.GetKeyDown(key);
        }

        public static bool Held(KeyCode key)
        {
            return key != KeyCode.None && Input.GetKey(key);
        }

        public static bool DirHeld()
        {
            return PhoneKeys.Held(PhoneKeys.GameUp) || Held(KeyCode.UpArrow)
                || PhoneKeys.Held(PhoneKeys.GameDown) || Held(KeyCode.DownArrow)
                || PhoneKeys.Held(PhoneKeys.GameLeft) || Held(KeyCode.LeftArrow)
                || PhoneKeys.Held(PhoneKeys.GameRight) || Held(KeyCode.RightArrow);
        }

        public static bool DirDown(out Vector2Int dir)
        {
            dir = Vector2Int.zero;
            if (PhoneKeys.Down(PhoneKeys.GameUp) || Down(KeyCode.UpArrow))
            {
                dir = Vector2Int.up;
                return true;
            }
            if (PhoneKeys.Down(PhoneKeys.GameDown) || Down(KeyCode.DownArrow))
            {
                dir = Vector2Int.down;
                return true;
            }
            if (PhoneKeys.Down(PhoneKeys.GameLeft) || Down(KeyCode.LeftArrow))
            {
                dir = Vector2Int.left;
                return true;
            }
            if (PhoneKeys.Down(PhoneKeys.GameRight) || Down(KeyCode.RightArrow))
            {
                dir = Vector2Int.right;
                return true;
            }
            return false;
        }

        public static float HoldX()
        {
            float x = 0f;
            if (PhoneKeys.Held(PhoneKeys.GameLeft) || Held(KeyCode.LeftArrow))
                x -= 1f;
            if (PhoneKeys.Held(PhoneKeys.GameRight) || Held(KeyCode.RightArrow))
                x += 1f;
            return x;
        }

        public static void Remember(ref int best, int score, IPiPhoneHost host)
        {
            if (score > best)
            {
                best = score;
                PhoneTheme.Commit();
            }
        }

        public static void OverRow(Transform parent, UnityAction again, UnityAction menu)
        {
            var row = new GameObject("Again", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            if (PhoneUi.Landscape)
            {
                PhoneUi.Size(row, 88f);
                PhoneUi.AddVertical(row, 6f, new RectOffset(0, 0, 0, 0));
                var v = row.GetComponent<VerticalLayoutGroup>();
                v.childForceExpandWidth = true;
                v.childForceExpandHeight = false;
                v.childAlignment = TextAnchor.UpperCenter;
                PhoneUi.MaterialChip(row.transform, "replay", "Play Again", again, new Vector2(36f, 32f));
                PhoneUi.MaterialChip(row.transform, "menu", "Menu", menu, new Vector2(36f, 32f));
                return;
            }
            PhoneUi.Size(row, 44f);
            PhoneUi.AddHorizontal(row, 8f);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            PhoneUi.MaterialChip(row.transform, "replay", "Play Again", again, new Vector2(36f, 32f));
            PhoneUi.MaterialChip(row.transform, "menu", "Menu", menu, new Vector2(36f, 32f));
        }

        public static void PlayMenu(Transform parent, UnityAction quit, bool over, UnityAction again, UnityAction menu)
        {
            if (quit != null)
                PhoneUi.MaterialChip(parent, "logout", "Quit", quit, new Vector2(36f, 32f));
            if (over && again != null && menu != null)
                OverRow(parent, again, menu);
        }

        public static void Dpad(Transform parent, UnityAction left, UnityAction right, UnityAction up, UnityAction down)
        {
            float w = PhoneUi.Landscape ? 40f : 48f;
            float h = PhoneUi.Landscape ? 36f : 40f;
            if (PhoneUi.Landscape)
            {
                var col = new GameObject("Pad", typeof(RectTransform));
                col.transform.SetParent(parent, false);
                PhoneUi.AddVertical(col, 6f, new RectOffset(8, 8, 4, 4));
                var v = col.GetComponent<VerticalLayoutGroup>();
                v.childForceExpandWidth = false;
                v.childForceExpandHeight = false;
                v.childAlignment = TextAnchor.MiddleCenter;
                if (up != null)
                    PhoneUi.CreateIconChip(col.transform, "^", PhoneIcons.Material("expand_less"), up, false, new Vector2(w, h));
                var mid = new GameObject("Mid", typeof(RectTransform));
                mid.transform.SetParent(col.transform, false);
                PhoneUi.Size(mid, h);
                PhoneUi.AddHorizontal(mid, 6f);
                var mh = mid.GetComponent<HorizontalLayoutGroup>();
                mh.childForceExpandWidth = false;
                mh.childAlignment = TextAnchor.MiddleCenter;
                if (left != null)
                    PhoneUi.CreateIconChip(mid.transform, "<", PhoneIcons.Material("chevron_left"), left, false, new Vector2(w, h));
                if (down != null)
                    PhoneUi.CreateIconChip(mid.transform, "v", PhoneIcons.Material("expand_more"), down, false, new Vector2(w, h));
                if (right != null)
                    PhoneUi.CreateIconChip(mid.transform, ">", PhoneIcons.Material("chevron_right"), right, false, new Vector2(w, h));
                return;
            }
            var row = new GameObject("Pad", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            PhoneUi.Size(row, 40f);
            PhoneUi.AddHorizontal(row, 4f);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            if (left != null)
                PhoneUi.CreateIconChip(row.transform, "<", PhoneIcons.Material("chevron_left"), left, false, new Vector2(w, h));
            if (down != null)
                PhoneUi.CreateIconChip(row.transform, "v", PhoneIcons.Material("expand_more"), down, false, new Vector2(w, h));
            if (up != null)
                PhoneUi.CreateIconChip(row.transform, "^", PhoneIcons.Material("expand_less"), up, false, new Vector2(w, h));
            if (right != null)
                PhoneUi.CreateIconChip(row.transform, ">", PhoneIcons.Material("chevron_right"), right, false, new Vector2(w, h));
        }

        public static TextMeshProUGUI HudBar(Transform parent, string text, UnityAction quit)
        {
            var row = new GameObject("Hud", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            if (PhoneUi.Landscape)
            {
                PhoneUi.Size(row, quit != null ? 108f : 72f);
                PhoneUi.AddVertical(row, 6f, new RectOffset(0, 0, 0, 0));
                var v = row.GetComponent<VerticalLayoutGroup>();
                v.childAlignment = TextAnchor.UpperCenter;
                v.childForceExpandWidth = true;
                v.childForceExpandHeight = false;
                var lab = PhoneUi.CreateLabel(row.transform, "Score", text ?? string.Empty, 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Wrap(lab);
                lab.overflowMode = TextOverflowModes.Ellipsis;
                PhoneUi.Size(lab.gameObject, 64f);
                if (quit != null)
                    PhoneUi.MaterialChip(row.transform, "logout", "Quit", quit, new Vector2(36f, 32f));
                return lab;
            }
            PhoneUi.Size(row, 36f);
            PhoneUi.AddHorizontal(row, 8f);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childForceExpandWidth = true;
            var port = PhoneUi.CreateLabel(row.transform, "Score", text ?? string.Empty, 15f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            port.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            if (quit != null)
                PhoneUi.MaterialChip(row.transform, "logout", "Quit", quit, new Vector2(36f, 32f));
            return port;
        }

        /// <summary>
        /// Portrait: score/quit on top, board fills the middle, pad on the bottom.
        /// Landscape: extras left, board center, controls right.
        /// </summary>
        public static void PlayLayout(IPiPhoneHost host, out Transform left, out Transform center, out Transform right)
        {
            left = center = right = host != null ? host.Content : null;
            if (host == null || host.Content == null)
                return;
            if (!host.IsLandscape)
            {
                left = PlayStrip(host.Content, "Hud", TextAnchor.UpperCenter, 0f);
                center = PlayStage(host.Content);
                right = PlayStrip(host.Content, "Controls", TextAnchor.MiddleCenter, 0f);
                return;
            }
            var row = new GameObject("PlayLand", typeof(RectTransform));
            row.transform.SetParent(host.Content, false);
            var le = row.AddComponent<LayoutElement>();
            le.flexibleHeight = 1f;
            le.minHeight = 160f;
            var h = PhoneUi.AddHorizontal(row, 8f);
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.padding = new RectOffset(4, 4, 0, 0);
            left = PlayPane(row.transform, "Left", 136f, 0f, TextAnchor.UpperCenter);
            center = PlayPane(row.transform, "Center", 220f, 1f, TextAnchor.MiddleCenter);
            right = PlayPane(row.transform, "Right", 148f, 0f, TextAnchor.UpperCenter);
            var centerLayout = center.GetComponent<VerticalLayoutGroup>();
            if (centerLayout != null)
                centerLayout.childForceExpandWidth = false;
        }

        public static float FitCell(int cols, int rows, float gap)
        {
            return FitCell(cols, rows, gap, 0f);
        }

        public static float FitCell(int cols, int rows, float gap, float extraChrome)
        {
            if (cols < 1)
                cols = 1;
            if (rows < 1)
                rows = 1;
            bool land = PhoneUi.Landscape;
            float w = land ? 240f : 360f;
            float h = (land ? 250f : 420f) - extraChrome;
            if (h < 120f)
                h = 120f;
            float cw = (w - (cols + 1) * gap) / cols;
            float ch = (h - (rows + 1) * gap) / rows;
            return Mathf.Clamp(Mathf.Floor(Mathf.Min(cw, ch)), 10f, 76f);
        }

        public static void Flush()
        {
            Canvas.ForceUpdateCanvases();
        }

        public static TextMeshProUGUI Stat(Transform parent, string name, string text)
        {
            var lab = PhoneUi.CreateLabel(parent, name, text, PhoneUi.Landscape ? 13f : 16f, FontStyles.Normal, TextAlignmentOptions.Center);
            PhoneUi.Wrap(lab);
            PhoneUi.Size(lab.gameObject, PhoneUi.Landscape ? 56f : 24f);
            return lab;
        }

        private static Transform PlayStrip(Transform parent, string name, TextAnchor align, float minHeight)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.flexibleHeight = 0f;
            le.minHeight = minHeight;
            PhoneUi.AddVertical(go, 6f, new RectOffset(0, 0, 0, 0));
            var v = go.GetComponent<VerticalLayoutGroup>();
            v.childAlignment = align;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.childControlWidth = true;
            return go.transform;
        }

        private static Transform PlayStage(Transform parent)
        {
            var go = new GameObject("Stage", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.flexibleHeight = 1f;
            le.minHeight = 140f;
            le.preferredHeight = 0f;
            go.AddComponent<RectMask2D>();
            PhoneUi.AddVertical(go, 4f, new RectOffset(0, 0, 0, 0));
            var v = go.GetComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childForceExpandWidth = false;
            v.childForceExpandHeight = false;
            v.childControlWidth = true;
            return go.transform;
        }

        private static Transform PlayPane(Transform parent, string name, float minWidth, float flex, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = minWidth;
            le.preferredWidth = minWidth;
            le.flexibleWidth = flex;
            le.flexibleHeight = 1f;
            go.AddComponent<RectMask2D>();
            PhoneUi.AddVertical(go, 6f, new RectOffset(2, 2, 2, 2));
            var v = go.GetComponent<VerticalLayoutGroup>();
            v.childAlignment = align;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.childControlWidth = true;
            return go.transform;
        }

        public static Image[] OverlayNext(Transform well, float cell)
        {
            return OverlayNext(well, cell, true);
        }

        public static Image[] OverlayNext(Transform well, float cell, bool overlay)
        {
            if (well == null)
                return new Image[0];
            float box = cell * 4f + 10f;
            var wrap = PhoneUi.CreateImage(well, "Next", PhoneUi.White(), new Color(0.06f, 0.07f, 0.09f, 0.92f));
            if (overlay)
            {
                PhoneUi.IgnoreLayout(wrap.gameObject);
                wrap.anchorMin = wrap.anchorMax = wrap.pivot = new Vector2(1f, 1f);
                wrap.sizeDelta = new Vector2(box, box);
                wrap.anchoredPosition = new Vector2(-6f, -6f);
            }
            else
            {
                var le = wrap.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = box;
                le.preferredHeight = box;
                le.minHeight = box;
            }
            var grid = wrap.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.cellSize = new Vector2(cell, cell);
            grid.spacing = new Vector2(1f, 1f);
            grid.padding = new RectOffset(4, 4, 4, 4);
            grid.childAlignment = TextAnchor.MiddleCenter;
            var cells = new Image[16];
            for (int i = 0; i < 16; i++)
            {
                var tile = PhoneUi.CreateImage(wrap, "N", PhoneUi.White(), new Color(0.08f, 0.09f, 0.1f, 1f));
                cells[i] = tile.GetComponent<Image>();
                cells[i].raycastTarget = false;
            }
            return cells;
        }

        public static GameObject Board(Transform parent, int cols, int rows, Vector2 cell, float gap, out Image[] cells, out TextMeshProUGUI[] labels)
        {
            return Board(parent, cols, rows, cell, gap, out cells, out labels, true);
        }

        public static GameObject Board(Transform parent, int cols, int rows, Vector2 cell, float gap, out Image[] cells, out TextMeshProUGUI[] labels, bool flex)
        {
            var board = new GameObject("Board", typeof(RectTransform));
            board.transform.SetParent(parent, false);
            var le = board.AddComponent<LayoutElement>();
            le.flexibleHeight = flex ? 1f : 0f;
            le.minHeight = flex ? 80f : rows * (cell.y + gap) + 16f;
            le.preferredHeight = flex ? 0f : le.minHeight;
            le.preferredWidth = cols * (cell.x + gap) + 8f;
            le.flexibleWidth = 0f;
            var grid = board.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = cols;
            grid.spacing = new Vector2(gap, gap);
            grid.cellSize = cell;
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.padding = new RectOffset(0, 0, 0, 0);
            cells = new Image[cols * rows];
            labels = new TextMeshProUGUI[cols * rows];
            for (int i = 0; i < cells.Length; i++)
            {
                var cellGo = PhoneUi.CreateImage(board.transform, "C", PhoneUi.White(), new Color(0.08f, 0.09f, 0.1f, 1f));
                cells[i] = cellGo.GetComponent<Image>();
                var tmp = PhoneUi.CreateLabel(cellGo, "T", string.Empty, Mathf.Clamp(cell.y * 0.42f, 11f, 22f), FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Stretch(tmp.rectTransform, 1f, 1f);
                labels[i] = tmp;
            }
            return board;
        }

        public static void Clear(IPiPhoneHost host)
        {
            if (host == null || host.Content == null)
                return;
            for (int i = host.Content.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(host.Content.GetChild(i).gameObject);
        }

        public static Color Tile(int value)
        {
            switch (value)
            {
                case 2: return new Color(0.93f, 0.89f, 0.85f, 1f);
                case 4: return new Color(0.93f, 0.88f, 0.78f, 1f);
                case 8: return new Color(0.95f, 0.69f, 0.47f, 1f);
                case 16: return new Color(0.96f, 0.58f, 0.39f, 1f);
                case 32: return new Color(0.96f, 0.48f, 0.37f, 1f);
                case 64: return new Color(0.96f, 0.37f, 0.23f, 1f);
                case 128: return new Color(0.93f, 0.81f, 0.45f, 1f);
                case 256: return new Color(0.93f, 0.80f, 0.38f, 1f);
                case 512: return new Color(0.93f, 0.78f, 0.31f, 1f);
                case 1024: return new Color(0.93f, 0.77f, 0.25f, 1f);
                case 2048: return new Color(0.93f, 0.75f, 0.18f, 1f);
                default: return value > 2048 ? new Color(0.24f, 0.22f, 0.20f, 1f) : new Color(0.80f, 0.75f, 0.70f, 1f);
            }
        }

        public static Color Ink(int value)
        {
            return value <= 4 ? new Color(0.22f, 0.18f, 0.16f, 1f) : Color.white;
        }
    }
}
