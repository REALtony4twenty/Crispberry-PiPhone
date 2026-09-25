using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    internal static class StackerApp
    {
        private static Session _live;

        internal static void Register()
        {
            PiPhoneApi.RegisterApp(new PiPhoneApp
            {
                Id = BuiltinApps.TetrisId,
                DisplayName = "Stacker",
                IconGlyph = "T",
                IconBackground = new Color(0.18f, 0.72f, 0.82f, 1f),
                SortOrder = 74,
                ShowOnHome = true,
                OnOpen = host => { _live = new Session(host); _live.BuildMenu(); },
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
            private const int Cols = 10;
            private const int Rows = 16;
            private readonly IPiPhoneHost _host;
            private string _page = "menu";
            private readonly int[] _board = new int[Cols * Rows];
            private int _kind;
            private int _rot;
            private int _px;
            private int _py;
            private int _score;
            private int _lines;
            private int _level = 1;
            private bool _dead;
            private float _fall;
            private float _repeat;
            private Image[] _cells;
            private Image[] _nextCells;
            private TextMeshProUGUI _scoreLabel;
            private readonly int[] _bag = new int[7];
            private int _bagLeft;
            private int _nextKind;
            private Transform _overParent;

            private static readonly Color[] Palette =
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

            public Session(IPiPhoneHost host)
            {
                _host = host;
            }

            public bool GoBack()
            {
                if (_page == "menu")
                    return false;
                BuildMenu();
                return true;
            }

            public void Relayout()
            {
                if (_page == "play")
                {
                    BuildPlayUi();
                    Draw();
                }
                else
                    BuildMenu();
            }

            public void BuildMenu()
            {
                _page = "menu";
                PhoneGames.Clear(_host);
                _host.SetTitle(PhoneLang.T("app.pip.tetris", "Stacker"));
                PhoneUi.MaterialChip(_host.Content, "play", "Play", StartGame, new Vector2(40f, 40f));
                var high = PhoneUi.CreateLabel(_host.Content, "High", "High score  " + PhoneTheme.HighTetris, 16f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Size(high.gameObject, 28f);
                var hint = PhoneUi.CreateLabel(_host.Content, "Hint", "WASD or arrows. W/Up rotates. S/Down drops. Space hard-drops.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 48f);
            }

            private void StartGame()
            {
                _page = "play";
                for (int i = 0; i < _board.Length; i++)
                    _board[i] = 0;
                _score = 0;
                _lines = 0;
                _level = 1;
                _dead = false;
                _fall = 0f;
                _bagLeft = 0;
                _nextKind = NextKind();
                Spawn();
                BuildPlayUi();
                Draw();
                _host.StartHostCoroutine(Run());
            }

            private void BuildPlayUi()
            {
                PhoneGames.Clear(_host);
                _host.SetTitle(PhoneLang.T("app.pip.tetris", "Stacker"));
                Transform left;
                Transform center;
                Transform right;
                PhoneGames.PlayLayout(_host, out left, out center, out right);
                _overParent = left;
                _scoreLabel = PhoneGames.HudBar(left, ScoreText(), BuildMenu);
                if (_dead)
                    PhoneGames.OverRow(left, StartGame, BuildMenu);
                float cell = PhoneGames.FitCell(Cols, Rows, 1f, _host.IsLandscape ? 0f : 40f);
                if (_host.IsLandscape)
                    cell = Mathf.Min(cell, 15f);
                GameObject board = PhoneGames.Board(center, Cols, Rows, new Vector2(cell, cell), 1f, out _cells, out _, false);
                if (_host.IsLandscape)
                    _nextCells = PhoneGames.OverlayNext(board.transform, 8f, true);
                else if (_scoreLabel != null)
                {
                    _nextCells = PhoneGames.OverlayNext(_scoreLabel.transform.parent, 8f, false);
                    PhoneUi.Size(_scoreLabel.transform.parent.gameObject, 48f);
                }
                var cluster = new GameObject("Controls", typeof(RectTransform));
                cluster.transform.SetParent(right, false);
                var clusterLayout = PhoneUi.AddHorizontal(cluster, 4f);
                clusterLayout.childForceExpandWidth = false;
                clusterLayout.childForceExpandHeight = false;
                clusterLayout.childAlignment = TextAnchor.MiddleCenter;
                var clusterLe = cluster.AddComponent<LayoutElement>();
                clusterLe.flexibleWidth = 0f;
                clusterLe.flexibleHeight = 0f;
                Vector2 pad = PhoneUi.Landscape ? new Vector2(40f, 36f) : new Vector2(48f, 40f);
                PhoneUi.MaterialChip(cluster.transform, "cached", "Rotate", Rotate, pad);
                PhoneGames.Dpad(cluster.transform, () => TryMove(-1, 0), () => TryMove(1, 0), Rotate, () => TryMove(0, -1));
                PhoneUi.MaterialChip(cluster.transform, "keyboard_double_arrow_down", "Drop", HardDrop, pad);
            }

            private string ScoreText()
            {
                if (_dead)
                    return PhoneLang.T("game_over", "Game over") + "  " + _score + "    " + PhoneLang.T("best", "Best") + " " + PhoneTheme.HighTetris;
                return "Lv " + _level + "   " + _score + "   " + _lines + "    " + PhoneLang.T("best", "Best") + " " + PhoneTheme.HighTetris;
            }

            private IEnumerator Run()
            {
                while (_page == "play" && !_dead)
                {
                    if (PhoneKeys.Down(PhoneKeys.TetrisRotate) || PhoneKeys.Down(PhoneKeys.GameUp) || PhoneGames.Down(KeyCode.UpArrow))
                        Rotate();
                    if (PhoneKeys.Down(PhoneKeys.TetrisDrop) || PhoneGames.Down(KeyCode.Space))
                        HardDrop();
                    float x = PhoneGames.HoldX();
                    if (PhoneKeys.Down(PhoneKeys.GameLeft) || PhoneGames.Down(KeyCode.LeftArrow))
                    {
                        TryMove(-1, 0);
                        _repeat = 0.32f;
                    }
                    else if (PhoneKeys.Down(PhoneKeys.GameRight) || PhoneGames.Down(KeyCode.RightArrow))
                    {
                        TryMove(1, 0);
                        _repeat = 0.32f;
                    }
                    else if (Mathf.Abs(x) > 0.1f)
                    {
                        _repeat -= Time.unscaledDeltaTime;
                        if (_repeat <= 0f)
                        {
                            TryMove(x < 0f ? -1 : 1, 0);
                            _repeat = 0.14f;
                        }
                    }
                    bool soft = PhoneKeys.Held(PhoneKeys.GameDown) || PhoneGames.Held(KeyCode.DownArrow);
                    float step = Mathf.Max(0.08f, 0.52f - (_level - 1) * 0.045f);
                    if (soft)
                        step *= 0.35f;
                    _fall += Time.unscaledDeltaTime;
                    if (_fall >= step)
                    {
                        _fall = 0f;
                        if (!TryMove(0, -1))
                            Lock();
                    }
                    yield return null;
                }
            }

            private int NextKind()
            {
                if (_bagLeft <= 0)
                {
                    for (int i = 0; i < 7; i++)
                        _bag[i] = i;
                    for (int i = 6; i > 0; i--)
                    {
                        int j = Random.Range(0, i + 1);
                        int t = _bag[i];
                        _bag[i] = _bag[j];
                        _bag[j] = t;
                    }
                    _bagLeft = 7;
                }
                _bagLeft--;
                return _bag[_bagLeft];
            }

            private void Spawn()
            {
                _kind = _nextKind;
                _nextKind = NextKind();
                _rot = 0;
                _px = 3;
                _py = Rows - 2;
                if (Hits(_px, _py, _rot))
                {
                    _dead = true;
                    PhoneGames.Remember(ref PhoneTheme.HighTetris, _score, _host);
                    if (_scoreLabel != null)
                        _scoreLabel.text = "Game over  " + _score + "    Best " + PhoneTheme.HighTetris;
                    PhoneGames.OverRow(_overParent != null ? _overParent : _host.Content, StartGame, BuildMenu);
                }
            }

            private void Rotate()
            {
                if (_dead)
                    return;
                int next = (_rot + 1) & 3;
                if (!Hits(_px, _py, next))
                    _rot = next;
                else if (!Hits(_px - 1, _py, next))
                {
                    _px--;
                    _rot = next;
                }
                else if (!Hits(_px + 1, _py, next))
                {
                    _px++;
                    _rot = next;
                }
                Draw();
            }

            private bool TryMove(int dx, int dy)
            {
                if (_dead)
                    return false;
                if (Hits(_px + dx, _py + dy, _rot))
                    return false;
                _px += dx;
                _py += dy;
                Draw();
                return true;
            }

            private void HardDrop()
            {
                if (_dead)
                    return;
                while (TryMove(0, -1))
                    _score++;
                Lock();
            }

            private void Lock()
            {
                Vector2Int[] cells = Shape(_kind, _rot);
                for (int i = 0; i < 4; i++)
                {
                    int x = _px + cells[i].x;
                    int y = _py + cells[i].y;
                    if (x >= 0 && y >= 0 && x < Cols && y < Rows)
                        _board[y * Cols + x] = _kind + 1;
                }
                int cleared = ClearLines();
                if (cleared > 0)
                {
                    _lines += cleared;
                    int[] pts = { 0, 40, 100, 300, 1200 };
                    _score += pts[cleared] * _level;
                    PhoneSounds.PlayTone(880, 0.08f);
                    int nextLevel = 1 + _lines / 10;
                    if (nextLevel > _level)
                    {
                        _level = nextLevel;
                        AddGarbage(Mathf.Min(3, 1 + (_level - 1) % 3));
                        _host.ShowToast("Level " + _level);
                    }
                }
                Spawn();
                Draw();
            }

            private void AddGarbage(int rows)
            {
                if (rows <= 0)
                    return;
                for (int r = 0; r < rows; r++)
                {
                    for (int y = Rows - 1; y > 0; y--)
                    {
                        for (int x = 0; x < Cols; x++)
                            _board[y * Cols + x] = _board[(y - 1) * Cols + x];
                    }
                    int hole = Random.Range(0, Cols);
                    int hole2 = _level >= 4 ? Random.Range(0, Cols) : hole;
                    for (int x = 0; x < Cols; x++)
                        _board[x] = (x == hole || x == hole2) ? 0 : 8;
                }
            }

            private int ClearLines()
            {
                int cleared = 0;
                for (int y = 0; y < Rows; y++)
                {
                    bool full = true;
                    for (int x = 0; x < Cols; x++)
                    {
                        if (_board[y * Cols + x] == 0)
                        {
                            full = false;
                            break;
                        }
                    }
                    if (!full)
                        continue;
                    cleared++;
                    for (int yy = y; yy < Rows - 1; yy++)
                    {
                        for (int x = 0; x < Cols; x++)
                            _board[yy * Cols + x] = _board[(yy + 1) * Cols + x];
                    }
                    for (int x = 0; x < Cols; x++)
                        _board[(Rows - 1) * Cols + x] = 0;
                    y--;
                }
                return cleared;
            }

            private bool Hits(int px, int py, int rot)
            {
                Vector2Int[] cells = Shape(_kind, rot);
                for (int i = 0; i < 4; i++)
                {
                    int x = px + cells[i].x;
                    int y = py + cells[i].y;
                    if (x < 0 || x >= Cols || y < 0)
                        return true;
                    if (y >= Rows)
                        continue;
                    if (_board[y * Cols + x] != 0)
                        return true;
                }
                return false;
            }

            private void Draw()
            {
                if (_scoreLabel != null && !_dead)
                    _scoreLabel.text = ScoreText();
                if (_cells == null)
                    return;
                var show = (int[])_board.Clone();
                if (!_dead)
                {
                    Vector2Int[] cells = Shape(_kind, _rot);
                    for (int i = 0; i < 4; i++)
                    {
                        int x = _px + cells[i].x;
                        int y = _py + cells[i].y;
                        if (x >= 0 && y >= 0 && x < Cols && y < Rows)
                            show[y * Cols + x] = _kind + 1;
                    }
                }
                for (int y = 0; y < Rows; y++)
                {
                    for (int x = 0; x < Cols; x++)
                    {
                        int i = (Rows - 1 - y) * Cols + x;
                        int v = show[y * Cols + x];
                        if (_cells[i] != null)
                            _cells[i].color = (v >= 0 && v < Palette.Length) ? Palette[v] : Palette[0];
                    }
                }
                DrawNext();
            }

            private void DrawNext()
            {
                if (_nextCells == null)
                    return;
                for (int i = 0; i < _nextCells.Length; i++)
                {
                    if (_nextCells[i] != null)
                        _nextCells[i].color = Palette[0];
                }
                Vector2Int[] cells = Shape(_nextKind, 0);
                int minX = 4;
                int minY = 4;
                int maxX = -1;
                int maxY = -1;
                for (int i = 0; i < 4; i++)
                {
                    if (cells[i].x < minX) minX = cells[i].x;
                    if (cells[i].y < minY) minY = cells[i].y;
                    if (cells[i].x > maxX) maxX = cells[i].x;
                    if (cells[i].y > maxY) maxY = cells[i].y;
                }
                int ox = (4 - (maxX - minX + 1)) / 2 - minX;
                int oy = (4 - (maxY - minY + 1)) / 2 - minY;
                int color = _nextKind + 1;
                for (int i = 0; i < 4; i++)
                {
                    int x = cells[i].x + ox;
                    int y = cells[i].y + oy;
                    if (x < 0 || y < 0 || x >= 4 || y >= 4)
                        continue;
                    int idx = (3 - y) * 4 + x;
                    if (idx >= 0 && idx < _nextCells.Length && _nextCells[idx] != null)
                        _nextCells[idx].color = Palette[color];
                }
            }

            private static Vector2Int[] Shape(int kind, int rot)
            {
                var c = new Vector2Int[4];
                int k = ((kind % 7) + 7) % 7;
                int r = rot & 3;
                if (k == 0)
                {
                    if ((r & 1) == 0) { c[0] = new Vector2Int(0, 1); c[1] = new Vector2Int(1, 1); c[2] = new Vector2Int(2, 1); c[3] = new Vector2Int(3, 1); }
                    else { c[0] = new Vector2Int(2, 0); c[1] = new Vector2Int(2, 1); c[2] = new Vector2Int(2, 2); c[3] = new Vector2Int(2, 3); }
                }
                else if (k == 1)
                {
                    c[0] = new Vector2Int(1, 0); c[1] = new Vector2Int(2, 0); c[2] = new Vector2Int(1, 1); c[3] = new Vector2Int(2, 1);
                }
                else if (k == 2)
                {
                    if (r == 0) { c[0] = new Vector2Int(1, 0); c[1] = new Vector2Int(0, 1); c[2] = new Vector2Int(1, 1); c[3] = new Vector2Int(2, 1); }
                    else if (r == 1) { c[0] = new Vector2Int(1, 0); c[1] = new Vector2Int(1, 1); c[2] = new Vector2Int(2, 1); c[3] = new Vector2Int(1, 2); }
                    else if (r == 2) { c[0] = new Vector2Int(0, 1); c[1] = new Vector2Int(1, 1); c[2] = new Vector2Int(2, 1); c[3] = new Vector2Int(1, 2); }
                    else { c[0] = new Vector2Int(1, 0); c[1] = new Vector2Int(0, 1); c[2] = new Vector2Int(1, 1); c[3] = new Vector2Int(1, 2); }
                }
                else if (k == 3)
                {
                    if ((r & 1) == 0) { c[0] = new Vector2Int(1, 0); c[1] = new Vector2Int(2, 0); c[2] = new Vector2Int(0, 1); c[3] = new Vector2Int(1, 1); }
                    else { c[0] = new Vector2Int(1, 0); c[1] = new Vector2Int(1, 1); c[2] = new Vector2Int(2, 1); c[3] = new Vector2Int(2, 2); }
                }
                else if (k == 4)
                {
                    if ((r & 1) == 0) { c[0] = new Vector2Int(0, 0); c[1] = new Vector2Int(1, 0); c[2] = new Vector2Int(1, 1); c[3] = new Vector2Int(2, 1); }
                    else { c[0] = new Vector2Int(2, 0); c[1] = new Vector2Int(1, 1); c[2] = new Vector2Int(2, 1); c[3] = new Vector2Int(1, 2); }
                }
                else if (k == 5)
                {
                    if (r == 0) { c[0] = new Vector2Int(0, 0); c[1] = new Vector2Int(0, 1); c[2] = new Vector2Int(1, 1); c[3] = new Vector2Int(2, 1); }
                    else if (r == 1) { c[0] = new Vector2Int(1, 0); c[1] = new Vector2Int(2, 0); c[2] = new Vector2Int(1, 1); c[3] = new Vector2Int(1, 2); }
                    else if (r == 2) { c[0] = new Vector2Int(0, 1); c[1] = new Vector2Int(1, 1); c[2] = new Vector2Int(2, 1); c[3] = new Vector2Int(2, 2); }
                    else { c[0] = new Vector2Int(1, 0); c[1] = new Vector2Int(1, 1); c[2] = new Vector2Int(0, 2); c[3] = new Vector2Int(1, 2); }
                }
                else
                {
                    if (r == 0) { c[0] = new Vector2Int(2, 0); c[1] = new Vector2Int(0, 1); c[2] = new Vector2Int(1, 1); c[3] = new Vector2Int(2, 1); }
                    else if (r == 1) { c[0] = new Vector2Int(1, 0); c[1] = new Vector2Int(1, 1); c[2] = new Vector2Int(1, 2); c[3] = new Vector2Int(2, 2); }
                    else if (r == 2) { c[0] = new Vector2Int(0, 1); c[1] = new Vector2Int(1, 1); c[2] = new Vector2Int(2, 1); c[3] = new Vector2Int(0, 2); }
                    else { c[0] = new Vector2Int(0, 0); c[1] = new Vector2Int(1, 0); c[2] = new Vector2Int(1, 1); c[3] = new Vector2Int(1, 2); }
                }
                return c;
            }
        }
    }
}
