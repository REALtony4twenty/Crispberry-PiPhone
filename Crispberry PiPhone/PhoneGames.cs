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
                || MinesApp.TryGoBack()
                || EchoApp.TryGoBack()
                || StackerApp.TryGoBack()
                || BrickBreakApp.TryGoBack()
                || FourAcrossApp.TryGoBack()
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
                    PhoneUi.CreateIconChip(col.transform, "^", PhoneIcons.Material("keyboard_arrow_up"), up, false, new Vector2(w, h));
                var mid = new GameObject("Mid", typeof(RectTransform));
                mid.transform.SetParent(col.transform, false);
                PhoneUi.Size(mid, h);
                PhoneUi.AddHorizontal(mid, 6f);
                var mh = mid.GetComponent<HorizontalLayoutGroup>();
                mh.childForceExpandWidth = false;
                mh.childAlignment = TextAnchor.MiddleCenter;
                if (left != null)
                    PhoneUi.CreateIconChip(mid.transform, "<", PhoneIcons.Material("keyboard_arrow_left"), left, false, new Vector2(w, h));
                if (down != null)
                    PhoneUi.CreateIconChip(mid.transform, "v", PhoneIcons.Material("keyboard_arrow_down"), down, false, new Vector2(w, h));
                if (right != null)
                    PhoneUi.CreateIconChip(mid.transform, ">", PhoneIcons.Material("keyboard_arrow_right"), right, false, new Vector2(w, h));
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
                PhoneUi.CreateIconChip(row.transform, "<", PhoneIcons.Material("keyboard_arrow_left"), left, false, new Vector2(w, h));
            if (down != null)
                PhoneUi.CreateIconChip(row.transform, "v", PhoneIcons.Material("keyboard_arrow_down"), down, false, new Vector2(w, h));
            if (up != null)
                PhoneUi.CreateIconChip(row.transform, "^", PhoneIcons.Material("keyboard_arrow_up"), up, false, new Vector2(w, h));
            if (right != null)
                PhoneUi.CreateIconChip(row.transform, ">", PhoneIcons.Material("keyboard_arrow_right"), right, false, new Vector2(w, h));
        }

        public static TextMeshProUGUI HudBar(Transform parent, string text, UnityAction quit)
        {
            return HudBar(parent, text, quit, null, null);
        }

        public static TextMeshProUGUI HudBar(Transform parent, string text, UnityAction quit, UnityAction again, string againName)
        {
            var row = new GameObject("Hud", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            Vector2 chip = new Vector2(36f, 32f);
            if (PhoneUi.Landscape)
            {
                PhoneUi.Size(row, again != null ? 186f : (quit != null ? 146f : 110f));
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
                    PhoneUi.MaterialChip(row.transform, "logout", "Quit", quit, chip);
                if (again != null)
                    PhoneUi.MaterialChip(row.transform, "replay", string.IsNullOrEmpty(againName) ? "Play Again" : againName, again, chip);
                Button sound = PhoneUi.MaterialChip(row.transform, "instant_mix", "Sound", ToggleGameSound, chip);
                PhoneUi.SetTooltip(sound.gameObject, "Sound");
                return lab;
            }
            PhoneUi.Size(row, 36f);
            PhoneUi.AddHorizontal(row, 8f);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childForceExpandWidth = true;
            var port = PhoneUi.CreateLabel(row.transform, "Score", text ?? string.Empty, 15f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            port.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            if (again != null)
                PhoneUi.MaterialChip(row.transform, "replay", string.IsNullOrEmpty(againName) ? "Play Again" : againName, again, chip);
            if (quit != null)
                PhoneUi.MaterialChip(row.transform, "logout", "Quit", quit, chip);
            Button soundBtn = PhoneUi.MaterialChip(row.transform, "instant_mix", "Sound", ToggleGameSound, chip);
            PhoneUi.SetTooltip(soundBtn.gameObject, "Sound");
            return port;
        }

        private static float _soundPreviewAt;

        public static void ToggleGameSound()
        {
            IPiPhoneHost host = PhoneMenu.InstanceHost;
            if (host == null || host.Content == null)
                return;
            Transform existing = host.Content.Find("GameSound");
            if (existing != null)
            {
                UnityEngine.Object.Destroy(existing.gameObject);
                return;
            }
            PiPhoneApp app = PhoneMenu.OpenApp();
            if (app == null)
                return;
            string appId = app.Id;
            var root = new GameObject("GameSound", typeof(RectTransform));
            root.transform.SetParent(host.Content, false);
            root.transform.SetAsLastSibling();
            PhoneUi.IgnoreLayout(root);
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(8f, 8f);
            rt.offsetMax = new Vector2(-8f, -8f);
            var card = PhoneUi.CreateImage(root.transform, "Card", PhoneUi.Rounded(16), PhoneUi.Surface);
            card.anchorMin = Vector2.zero;
            card.anchorMax = Vector2.one;
            card.offsetMin = Vector2.zero;
            card.offsetMax = Vector2.zero;
            PhoneUi.SetClickable(card.gameObject, true);
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 10);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var head = new GameObject("Head", typeof(RectTransform));
            head.transform.SetParent(card, false);
            PhoneUi.Size(head, 36f);
            var headLayout = PhoneUi.AddHorizontal(head, 6f);
            headLayout.childAlignment = TextAnchor.MiddleLeft;
            headLayout.childForceExpandWidth = false;
            var title = PhoneUi.CreateLabel(head.transform, "T", "Sounds", 15f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            PhoneUi.MaterialChip(head.transform, "close", "Close", ToggleGameSound, new Vector2(32f, 28f));
            ScrollRect scroll = PhoneUi.CreateScrollView(card, out RectTransform list);
            var scrollLe = scroll.gameObject.AddComponent<LayoutElement>();
            scrollLe.flexibleHeight = 1f;
            scrollLe.minHeight = 80f;
            PhoneUi.AddVertical(list.gameObject, 4f, new RectOffset(0, 0, 0, 4));
            PhoneUi.FitVertical(list.gameObject);
            PhoneSfx.Cue[] cues = PhoneSfx.Cues(appId);
            for (int i = 0; i < cues.Length; i++)
            {
                string cueKey = cues[i].Key;
                string cueLabel = cues[i].Label;
                bool tone = cues[i].Tone;
                int hz = cues[i].Hz;
                float sec = cues[i].Seconds;
                PhoneUi.CreateSliderRow(list, cueLabel, 0f, 1f, PhoneTheme.GameCueVolume(appId, cueKey), v =>
                {
                    PhoneTheme.SetGameCueVolume(appId, cueKey, v);
                    if (Time.unscaledTime < _soundPreviewAt)
                        return;
                    _soundPreviewAt = Time.unscaledTime + 0.14f;
                    if (tone)
                        PhoneSounds.PlayTone(hz, sec, cueKey);
                    else
                        PhoneSfx.Play(cueKey);
                });
            }
        }

        public static void HidePane(Transform pane)
        {
            if (pane == null)
                return;
            LayoutElement le = pane.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.minWidth = 0f;
                le.preferredWidth = 0f;
                le.flexibleWidth = 0f;
                le.minHeight = 0f;
                le.preferredHeight = 0f;
                le.flexibleHeight = 0f;
            }
            pane.gameObject.SetActive(false);
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
                SeatPlay(host, false);
                left = PlayStrip(host.Content, "Hud", TextAnchor.UpperCenter, 0f);
                center = PlayStage(host.Content);
                right = PlayStrip(host.Content, "Controls", TextAnchor.MiddleCenter, 0f);
                return;
            }
            SeatPlay(host, true);
            var shell = new GameObject("PlayLand", typeof(RectTransform));
            shell.transform.SetParent(host.Content, false);
            var le = shell.AddComponent<LayoutElement>();
            le.flexibleHeight = 1f;
            le.flexibleWidth = 1f;
            le.minHeight = 160f;
            var rowGo = new GameObject("Row", typeof(RectTransform));
            rowGo.transform.SetParent(shell.transform, false);
            PhoneUi.Stretch(rowGo.GetComponent<RectTransform>(), 0f, 0f);
            var h = PhoneUi.AddHorizontal(rowGo, 6f);
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            h.childAlignment = TextAnchor.LowerCenter;
            h.padding = new RectOffset(2, 2, 2, 0);
            left = PlayPane(rowGo.transform, "Left", 136f, 0f, TextAnchor.UpperCenter);
            center = PlayPane(rowGo.transform, "Center", 220f, 1f, TextAnchor.LowerCenter);
            right = PlayPane(rowGo.transform, "Right", 148f, 0f, TextAnchor.UpperCenter);
            var centerLayout = center.GetComponent<VerticalLayoutGroup>();
            if (centerLayout != null)
            {
                centerLayout.childForceExpandWidth = false;
                centerLayout.padding = new RectOffset(2, 2, 2, 0);
            }
        }

        private static void SeatPlay(IPiPhoneHost host, bool land)
        {
            if (host == null || host.Content == null)
                return;
            var layout = host.Content.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
                return;
            if (!land)
            {
                layout.padding = new RectOffset(12, 12, 10, 10);
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childForceExpandHeight = false;
                return;
            }
            layout.padding = new RectOffset(8, 8, 4, 8);
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childForceExpandHeight = true;
        }

        public static float FitCell(int cols, int rows, float gap)
        {
            return FitCell(cols, rows, gap, 0f);
        }

        internal const float LandscapeBoardH = 340f;

        public static float FitCell(int cols, int rows, float gap, float extraChrome)
        {
            if (cols < 1)
                cols = 1;
            if (rows < 1)
                rows = 1;
            bool land = PhoneUi.Landscape;
            float w = land ? 480f : 360f;
            float h = (land ? LandscapeBoardH : 420f) - extraChrome;
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
            float boardH = rows * cell.y + Mathf.Max(0, rows - 1) * gap;
            le.flexibleHeight = flex ? 1f : 0f;
            le.minHeight = flex ? 80f : boardH;
            le.preferredHeight = flex ? 0f : boardH;
            le.preferredWidth = cols * (cell.x + gap) + 8f;
            le.flexibleWidth = 0f;
            var grid = board.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = cols;
            grid.spacing = new Vector2(gap, gap);
            grid.cellSize = cell;
            grid.childAlignment = TextAnchor.LowerCenter;
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
