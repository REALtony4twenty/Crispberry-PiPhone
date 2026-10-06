using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// What a watcher draws for a built-in game. The caster sends a short
    /// description. This PC builds the board from art it already has.
    /// </summary>
    internal static class PhoneCastBoards
    {
        private static readonly string[] Suits = { "spades", "hearts", "diamonds", "clubs" };
        private static readonly string[] Ranks = { "A", "02", "03", "04", "05", "06", "07", "08", "09", "10", "J", "Q", "K" };
        private static readonly Color[] StackerColors =
        {
            new Color(0.08f, 0.09f, 0.1f, 1f),
            new Color(0.22f, 0.80f, 0.88f, 1f),
            new Color(0.95f, 0.82f, 0.22f, 1f),
            new Color(0.72f, 0.38f, 0.88f, 1f),
            new Color(0.28f, 0.78f, 0.38f, 1f),
            new Color(0.88f, 0.28f, 0.28f, 1f),
            new Color(0.28f, 0.42f, 0.88f, 1f),
            new Color(0.95f, 0.58f, 0.18f, 1f),
            new Color(0.42f, 0.44f, 0.48f, 1f)
        };
        private static readonly Color[] EchoColors =
        {
            new Color(0.18f, 0.72f, 0.38f, 1f),
            new Color(0.86f, 0.24f, 0.24f, 1f),
            new Color(0.95f, 0.78f, 0.20f, 1f),
            new Color(0.22f, 0.48f, 0.92f, 1f)
        };

        private static string _kind = string.Empty;
        private static Image[] _cells;
        private static TextMeshProUGUI[] _labels;
        private static TextMeshProUGUI _status;
        private static RectTransform _board;
        private static RectTransform _paddle;
        private static RectTransform _ball;

        internal static bool Supports(string appId)
        {
            return Kind(appId).Length > 0;
        }

        internal static string Describe(string appId)
        {
            if (appId == BuiltinApps.SnakeId)
                return SnakeApp.CastState();
            if (appId == BuiltinApps.SolitaireId)
                return SolitaireApp.CastState();
            if (appId == BuiltinApps.Game2048Id)
                return Game2048App.CastState();
            if (appId == BuiltinApps.MinesId)
                return MinesApp.CastState();
            if (appId == BuiltinApps.TetrisId)
                return StackerApp.CastState();
            if (appId == BuiltinApps.Connect4Id)
                return FourAcrossApp.CastState();
            if (appId == BuiltinApps.SudokuId)
                return SudokuApp.CastState();
            if (appId == BuiltinApps.SimonId)
                return EchoApp.CastState();
            if (appId == BuiltinApps.BreakoutId)
                return BrickBreakApp.CastState();
            return string.Empty;
        }

        internal static void Paint(RectTransform screen, string appId, string blob, bool land)
        {
            _kind = Kind(appId);
            _cells = null;
            _labels = null;
            _board = null;
            _paddle = null;
            _ball = null;
            _status = null;
            if (string.IsNullOrEmpty(blob) || !blob.StartsWith("play"))
            {
                Face(screen, appId);
                return;
            }
            _status = PhoneUi.CreateLabel(screen, "Status", "", 16f, FontStyles.Bold, TextAlignmentOptions.Top);
            PhoneUi.StretchTop(_status.rectTransform, 28f);
            _status.color = Color.white;
            if (_kind == "snake")
                BuildGrid(screen, 12, 16, land, false);
            else if (_kind == "2048")
                BuildGrid(screen, 4, 4, land, true);
            else if (_kind == "mines")
                BuildGrid(screen, 8, 10, land, true);
            else if (_kind == "stacker")
                BuildGrid(screen, 10, 16, land, false);
            else if (_kind == "four")
                BuildGrid(screen, 7, 6, land, false);
            else if (_kind == "sudoku")
                BuildGrid(screen, 9, 9, land, true);
            else if (_kind == "echo")
                BuildEcho(screen);
            else if (_kind == "brick")
                BuildBrick(screen);
            else if (_kind == "solitaire")
                BuildSolitaire(screen);
            Retint(blob);
        }

        internal static void Retint(string blob)
        {
            if (string.IsNullOrEmpty(_kind) || string.IsNullOrEmpty(blob) || !blob.StartsWith("play"))
                return;
            string[] p = blob.Split('|');
            if (_kind == "snake")
                TintSnake(p);
            else if (_kind == "2048")
                Tint2048(p);
            else if (_kind == "mines")
                TintMines(p);
            else if (_kind == "stacker")
                TintStacker(p);
            else if (_kind == "four")
                TintFour(p);
            else if (_kind == "sudoku")
                TintSudoku(p);
            else if (_kind == "echo")
                TintEcho(p);
            else if (_kind == "brick")
                TintBrick(p);
            else if (_kind == "solitaire")
                TintSolitaire(p);
        }

        private static string Kind(string appId)
        {
            if (appId == BuiltinApps.SnakeId)
                return "snake";
            if (appId == BuiltinApps.SolitaireId)
                return "solitaire";
            if (appId == BuiltinApps.Game2048Id)
                return "2048";
            if (appId == BuiltinApps.MinesId)
                return "mines";
            if (appId == BuiltinApps.TetrisId)
                return "stacker";
            if (appId == BuiltinApps.Connect4Id)
                return "four";
            if (appId == BuiltinApps.SudokuId)
                return "sudoku";
            if (appId == BuiltinApps.SimonId)
                return "echo";
            if (appId == BuiltinApps.BreakoutId)
                return "brick";
            return string.Empty;
        }

        private static void Face(RectTransform screen, string appId)
        {
            PiPhoneApp app;
            PiPhoneApi.TryGetApp(appId, out app);
            if (app == null)
            {
                var missing = PhoneUi.CreateLabel(screen, "Note", "Not on this cast", 20f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Stretch(missing.rectTransform, 24f, 48f);
                missing.color = PhoneUi.TextDim;
                return;
            }
            var icon = PhoneIcons.CreateView(screen, app, 96f, false);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.62f);
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = Vector2.zero;
            var name = PhoneUi.CreateLabel(screen, "Name", app.DisplayName, 22f, FontStyles.Bold, TextAlignmentOptions.Center);
            name.rectTransform.anchorMin = new Vector2(0f, 0.38f);
            name.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            name.rectTransform.offsetMin = Vector2.zero;
            name.rectTransform.offsetMax = Vector2.zero;
            name.color = Color.white;
        }

        private static void BuildGrid(RectTransform screen, int cols, int rows, bool land, bool labels)
        {
            float w = land ? 800f : 380f;
            float h = land ? 340f : 720f;
            float cell = Mathf.Min((w - (cols - 1) * 3f) / cols, (h - (rows - 1) * 3f) / rows);
            var go = new GameObject("Board", typeof(RectTransform));
            go.transform.SetParent(screen, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.46f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(cols * cell + (cols - 1) * 3f, rows * cell + (rows - 1) * 3f);
            var grid = go.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(cell, cell);
            grid.spacing = new Vector2(3f, 3f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = cols;
            grid.childAlignment = TextAnchor.MiddleCenter;
            _cells = new Image[cols * rows];
            _labels = labels ? new TextMeshProUGUI[cols * rows] : null;
            for (int i = 0; i < _cells.Length; i++)
            {
                var tile = PhoneUi.CreateImage(go.transform, "C", _kind == "four" ? PhoneUi.Circle() : PhoneUi.Rounded(4), new Color(0.1f, 0.11f, 0.13f, 1f));
                Image img = tile.GetComponent<Image>();
                img.raycastTarget = false;
                _cells[i] = img;
                if (labels)
                {
                    float font = cell >= 36f ? 20f : 12f;
                    TextMeshProUGUI text = PhoneUi.CreateLabel(tile, "N", "", font, FontStyles.Bold, TextAlignmentOptions.Center);
                    PhoneUi.Stretch(text.rectTransform, 0f, 0f);
                    text.color = Color.white;
                    _labels[i] = text;
                }
            }
        }

        private static void TintSnake(string[] p)
        {
            if (_cells == null || p.Length < 4)
                return;
            SetStatus(p[2] == "1" ? "Game over  " + p[1] : "Score  " + p[1]);
            string cells = p[3];
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] == null)
                    continue;
                char c = i < cells.Length ? cells[i] : '.';
                if (c == 'H')
                    _cells[i].color = new Color(0.42f, 0.92f, 0.48f, 1f);
                else if (c == 's')
                    _cells[i].color = new Color(0.22f, 0.72f, 0.34f, 1f);
                else if (c == 'f')
                    _cells[i].color = new Color(0.92f, 0.42f, 0.28f, 1f);
                else
                    _cells[i].color = new Color(0.08f, 0.09f, 0.1f, 1f);
            }
        }

        private static void Tint2048(string[] p)
        {
            if (_cells == null || p.Length < 4)
                return;
            SetStatus(p[2] == "1" ? "Game over  " + p[1] : "Score  " + p[1]);
            string[] vals = p[3].Split(',');
            for (int i = 0; i < _cells.Length; i++)
            {
                int v = i < vals.Length ? Int(vals[i]) : 0;
                if (_cells[i] != null)
                    _cells[i].color = v < 0 ? new Color(0.18f, 0.16f, 0.14f, 1f) : (v == 0 ? new Color(0.80f, 0.75f, 0.70f, 0.35f) : PhoneGames.Tile(v));
                if (_labels != null && i < _labels.Length && _labels[i] != null)
                {
                    _labels[i].text = v == 0 ? string.Empty : (v < 0 ? "X" : v.ToString());
                    _labels[i].color = v < 0 ? new Color(0.85f, 0.55f, 0.35f, 1f) : PhoneGames.Ink(v);
                }
            }
        }

        private static void TintMines(string[] p)
        {
            if (_cells == null || p.Length < 3)
                return;
            SetStatus(p[1] == "1" ? "Game over" : "Mines");
            string cells = p[2];
            for (int i = 0; i < _cells.Length; i++)
            {
                char c = i < cells.Length ? cells[i] : '.';
                if (_cells[i] == null)
                    continue;
                if (c == '*')
                    _cells[i].color = new Color(0.72f, 0.22f, 0.18f, 1f);
                else if (c == 'F')
                    _cells[i].color = new Color(0.72f, 0.55f, 0.18f, 1f);
                else if (c == '.')
                    _cells[i].color = new Color(0.28f, 0.32f, 0.36f, 1f);
                else
                    _cells[i].color = new Color(0.16f, 0.18f, 0.20f, 1f);
                if (_labels != null && i < _labels.Length && _labels[i] != null)
                {
                    if (c == '*' || c == 'F')
                        _labels[i].text = c == '*' ? "*" : "F";
                    else if (c >= '1' && c <= '8')
                        _labels[i].text = c.ToString();
                    else
                        _labels[i].text = string.Empty;
                    _labels[i].color = Color.white;
                }
            }
        }

        private static void TintStacker(string[] p)
        {
            if (_cells == null || p.Length < 4)
                return;
            SetStatus(p[2] == "1" ? "Game over  " + p[1] : "Score  " + p[1]);
            string cells = p[3];
            for (int i = 0; i < _cells.Length; i++)
            {
                int v = 0;
                if (i < cells.Length && cells[i] >= '0' && cells[i] <= '8')
                    v = cells[i] - '0';
                if (_cells[i] != null)
                    _cells[i].color = StackerColors[v];
            }
        }

        private static void TintFour(string[] p)
        {
            if (_cells == null || p.Length < 2)
                return;
            SetStatus("Four Across");
            string board = p[1];
            bool[] win = FourAcrossApp.WinningMask(board);
            int n = 0;
            for (int r = 5; r >= 0; r--)
            {
                for (int c = 0; c < 7; c++)
                {
                    if (n >= _cells.Length)
                        return;
                    int i = r * 7 + c;
                    char ch = i < board.Length ? board[i] : '0';
                    if (_cells[n] != null)
                    {
                        Color color = PhoneUi.SurfaceAlt;
                        if (ch == '1')
                            color = PhoneUi.HangRed;
                        else if (ch == '2')
                            color = new Color(0.95f, 0.82f, 0.18f, 1f);
                        if (win != null && i < win.Length && win[i])
                            color = Color.Lerp(color, Color.white, 0.72f);
                        _cells[n].color = color;
                    }
                    n++;
                }
            }
        }

        private static void TintSudoku(string[] p)
        {
            if (_cells == null || p.Length < 3)
                return;
            SetStatus("Sudoku");
            string vals = p[1];
            string given = p[2];
            var grid = new int[81];
            for (int i = 0; i < 81; i++)
                grid[i] = i < vals.Length && vals[i] >= '0' && vals[i] <= '9' ? vals[i] - '0' : 0;
            for (int i = 0; i < _cells.Length && i < 81; i++)
            {
                int r = i / 9;
                int c = i % 9;
                bool band = ((r / 3) + (c / 3)) % 2 == 0;
                Color bg = band ? new Color(0.12f, 0.14f, 0.18f, 1f) : new Color(0.08f, 0.09f, 0.11f, 1f);
                if (grid[i] != 0 && SudokuConflict(i, grid[i], grid))
                    bg = new Color(0.55f, 0.18f, 0.18f, 1f);
                if (_cells[i] != null)
                    _cells[i].color = bg;
                if (_labels != null && _labels[i] != null)
                {
                    _labels[i].text = grid[i] == 0 ? string.Empty : grid[i].ToString();
                    bool locked = i < given.Length && given[i] == '1';
                    _labels[i].color = locked ? Color.white : new Color(0.55f, 0.82f, 1f, 1f);
                }
            }
        }

        private static bool SudokuConflict(int i, int n, int[] grid)
        {
            int r = i / 9;
            int c = i % 9;
            for (int k = 0; k < 9; k++)
            {
                if (grid[r * 9 + k] == n && k != c)
                    return true;
                if (grid[k * 9 + c] == n && k != r)
                    return true;
            }
            int br = (r / 3) * 3;
            int bc = (c / 3) * 3;
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    int j = (br + y) * 9 + (bc + x);
                    if (j != i && grid[j] == n)
                        return true;
                }
            }
            return false;
        }

        private static void BuildEcho(RectTransform screen)
        {
            BuildGrid(screen, 2, 2, false, false);
        }

        private static void TintEcho(string[] p)
        {
            if (_cells == null)
                return;
            string count = p.Length > 1 ? p[1] : "0";
            SetStatus(p.Length > 2 && p[2] == "1" ? "Wrong  " + count : "Echo  " + count);
            for (int i = 0; i < _cells.Length && i < EchoColors.Length; i++)
            {
                if (_cells[i] != null)
                {
                    _cells[i].sprite = PhoneUi.Rounded(24);
                    _cells[i].color = EchoColors[i];
                }
            }
        }

        private static void BuildBrick(RectTransform screen)
        {
            var field = new GameObject("Field", typeof(RectTransform));
            field.transform.SetParent(screen, false);
            _board = field.GetComponent<RectTransform>();
            PhoneUi.Stretch(_board, 16f, 36f);
            _cells = new Image[7 * 8];
            for (int i = 0; i < _cells.Length; i++)
            {
                int col = i % 7;
                int row = i / 7;
                var brick = PhoneUi.CreateImage(_board, "B", PhoneUi.Rounded(4), Color.white);
                brick.anchorMin = new Vector2(0.04f + col * 0.132f, 0.97f - (row + 1) * 0.055f);
                brick.anchorMax = new Vector2(0.04f + (col + 1) * 0.132f - 0.008f, 0.97f - row * 0.055f);
                brick.offsetMin = Vector2.zero;
                brick.offsetMax = Vector2.zero;
                Image img = brick.GetComponent<Image>();
                img.raycastTarget = false;
                _cells[i] = img;
            }
            var paddle = PhoneUi.CreateImage(_board, "Paddle", PhoneUi.Rounded(6), Color.white);
            _paddle = paddle;
            var ball = PhoneUi.CreateImage(_board, "Ball", PhoneUi.Circle(), Color.white);
            _ball = ball;
            ball.GetComponent<Image>().raycastTarget = false;
            paddle.GetComponent<Image>().raycastTarget = false;
        }

        private static void TintBrick(string[] p)
        {
            if (_cells == null || p.Length < 11)
                return;
            SetStatus(p[4] == "1" ? "Game over  " + p[1] : "Lv " + p[3] + "   " + p[2] + "   " + p[1]);
            string hp = p[5];
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] == null)
                    continue;
                char c = i < hp.Length ? hp[i] : '0';
                int n = c >= '0' && c <= '9' ? c - '0' : 0;
                _cells[i].enabled = n != 0;
                if (n >= 3)
                    _cells[i].color = new Color(0.72f, 0.32f, 0.82f, 1f);
                else if (n == 2)
                    _cells[i].color = new Color(0.95f, 0.55f, 0.18f, 1f);
                else if (n == 1)
                    _cells[i].color = BrickRowColor(i / 7);
            }
            PlaceBar(_paddle, Int(p[6]) / 1000f, Int(p[7]) / 1000f, Int(p[10]) / 1000f, 0.02f);
            PlaceBar(_ball, Int(p[8]) / 1000f, Int(p[9]) / 1000f, 0.035f, 0.025f);
        }

        private static Color BrickRowColor(int row)
        {
            switch (row % 5)
            {
                case 0: return new Color(0.86f, 0.24f, 0.24f, 1f);
                case 1: return new Color(0.95f, 0.55f, 0.18f, 1f);
                case 2: return new Color(0.95f, 0.82f, 0.22f, 1f);
                case 3: return new Color(0.28f, 0.72f, 0.38f, 1f);
                default: return new Color(0.22f, 0.55f, 0.92f, 1f);
            }
        }

        private static void PlaceBar(RectTransform rt, float x, float y, float w, float h)
        {
            if (rt == null)
                return;
            float hw = w * 0.5f;
            float hh = h * 0.5f;
            rt.anchorMin = new Vector2(Mathf.Clamp01(x - hw), Mathf.Clamp01(y - hh));
            rt.anchorMax = new Vector2(Mathf.Clamp01(x + hw), Mathf.Clamp01(y + hh));
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void BuildSolitaire(RectTransform screen)
        {
            var go = new GameObject("Cards", typeof(RectTransform));
            go.transform.SetParent(screen, false);
            _board = go.GetComponent<RectTransform>();
            PhoneUi.Stretch(_board, 8f, 32f);
        }

        private static void TintSolitaire(string[] p)
        {
            if (_board == null || p.Length < 6)
                return;
            for (int i = _board.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(_board.GetChild(i).gameObject);
            SetStatus(p[1] == "1" ? "You win" : "Solitaire");
            float h = 64f;
            Card(_board, p[2] != "0" ? -2 : -1, 8f, 4f, h);
            string[] waste = Split(p[3], '.');
            int top = waste.Length > 0 ? Int(waste[waste.Length - 1]) : -1;
            if (p[3].Length == 0)
                top = -1;
            Card(_board, top, 58f, 4f, h);
            string[] found = Split(p[4], '/');
            for (int f = 0; f < 4; f++)
            {
                string pile = f < found.Length ? found[f] : string.Empty;
                string[] cards = Split(pile, '.');
                int show = -1;
                if (pile.Length > 0 && cards.Length > 0)
                    show = Int(cards[cards.Length - 1]);
                Card(_board, show, 168f + f * 52f, 4f, h);
            }
            string[] tabs = Split(p[5], '/');
            for (int c = 0; c < 7; c++)
            {
                string pile = c < tabs.Length ? tabs[c] : string.Empty;
                string[] cards = Split(pile, '.');
                if (pile.Length == 0)
                {
                    Card(_board, -1, 8f + c * 52f, 78f, h);
                    continue;
                }
                for (int k = 0; k < cards.Length; k++)
                {
                    bool down = cards[k] == "d";
                    int id = down ? -2 : Int(cards[k]);
                    Card(_board, id, 8f + c * 52f, 78f + k * 18f, h);
                }
            }
        }

        private static void Card(Transform parent, int card, float x, float y, float height)
        {
            string art = "card_empty";
            if (card == -2)
                art = "card_back";
            else if (card >= 0)
            {
                int suit = card / 13;
                int rank = card % 13;
                if (suit >= 0 && suit < 4 && rank >= 0 && rank < 13)
                    art = "card_" + Suits[suit] + "_" + Ranks[rank];
            }
            Sprite sprite = PhoneIcons.Card(art);
            var rt = PhoneUi.CreateImage(parent, "Card", sprite != null ? sprite : PhoneUi.White(), Color.white);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(height * 0.72f, height);
            rt.anchoredPosition = new Vector2(x, -y);
            Image img = rt.GetComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.type = Image.Type.Simple;
        }

        private static void SetStatus(string text)
        {
            if (_status != null)
                _status.text = text ?? string.Empty;
        }

        private static int Int(string text)
        {
            int n;
            if (int.TryParse(text, out n))
                return n;
            return 0;
        }

        private static string[] Split(string text, char mark)
        {
            if (string.IsNullOrEmpty(text))
                return new string[0];
            return text.Split(mark);
        }
    }
}
