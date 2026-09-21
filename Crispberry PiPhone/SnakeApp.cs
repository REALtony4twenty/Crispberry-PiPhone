using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    internal static class SnakeApp
    {
        internal static void Register()
        {
            PiPhoneApi.RegisterApp(new PiPhoneApp
            {
                Id = BuiltinApps.SnakeId,
                DisplayName = "Snake",
                IconGlyph = "S",
                IconBackground = new Color(0.18f, 0.62f, 0.28f, 1f),
                SortOrder = 70,
                ShowOnHome = true,
                OnOpen = host => { _live = new Session(host); _live.BuildMenu(); },
                OnClose = () => { _live = null; },
                OnOrientation = () => { if (_live != null) _live.Relayout(); }
            });
        }

        private static Session _live;

        internal static bool TryGoBack()
        {
            if (_live == null)
                return false;
            return _live.GoBack();
        }

        private sealed class Session
        {
            private const int Cols = 12;
            private const int Rows = 16;
            private readonly IPiPhoneHost _host;
            private string _page = "menu";
            private readonly List<Vector2Int> _snake = new List<Vector2Int>();
            private Vector2Int _dir = Vector2Int.up;
            private Vector2Int _pending = Vector2Int.up;
            private Vector2Int _food;
            private float _tick;
            private bool _dead;
            private int _score;
            private int _level = 1;
            private Image[] _cells;
            private TextMeshProUGUI _scoreLabel;
            private Transform _overParent;

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
                else if (_page == "menu")
                    BuildMenu();
                else if (_page == "controls")
                    BuildControls();
            }

            public void BuildMenu()
            {
                _page = "menu";
                Clear();
                _host.SetTitle(PhoneLang.T("app.pip.snake", "Snake"));
                PhoneUi.CreateButton(_host.Content, PhoneLang.T("play", "Play"), StartGame, new Vector2(220f, 48f));
                PhoneUi.CreateButton(_host.Content, PhoneLang.T("controls", "Controls"), BuildControls, new Vector2(220f, 44f));
                var high = PhoneUi.CreateLabel(_host.Content, "High", "High score  " + PhoneTheme.SnakeHigh, 16f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Size(high.gameObject, 28f);
                var hint = PhoneUi.CreateLabel(_host.Content, "Hint", "Eat the dots. Fill the whole board to reach the next level. Each level is faster.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 40f);
            }

            private void BuildControls()
            {
                _page = "controls";
                Clear();
                _host.SetTitle("Snake controls");
                PhoneUi.CreateButton(_host.Content, "Back", BuildMenu, new Vector2(120f, 36f));
                Bind("Up", PhoneKeys.GameUp);
                Bind("Down", PhoneKeys.GameDown);
                Bind("Left", PhoneKeys.GameLeft);
                Bind("Right", PhoneKeys.GameRight);
                var hint = PhoneUi.CreateLabel(_host.Content, "Hint", Plugin.CapturingHotkey ? "Press a key..." : "Shared with the other arcade games. Arrow keys always work. Full list in Settings → Controls.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Size(hint.gameObject, 36f);
            }

            private void Bind(string name, string id)
            {
                PhoneUi.CreateButton(_host.Content, name + "  " + PhoneKeys.Format(id), () =>
                {
                    PhoneKeys.BeginCapture(id, () =>
                    {
                        if (_live != null)
                            BuildControls();
                    });
                    _host.ShowToast("Press a key. Esc cancels.");
                    BuildControls();
                }, new Vector2(260f, 40f));
            }

            private void StartGame()
            {
                _page = "play";
                _dead = false;
                _score = 0;
                _level = 1;
                _dir = Vector2Int.up;
                _pending = Vector2Int.up;
                ResetSnake();
                PlaceFood();
                BuildPlayUi();
                Draw();
                _host.StartHostCoroutine(Run());
            }

            private void ResetSnake()
            {
                _snake.Clear();
                _snake.Add(new Vector2Int(Cols / 2, Rows / 2));
                _snake.Add(new Vector2Int(Cols / 2, Rows / 2 - 1));
                _dir = Vector2Int.up;
                _pending = Vector2Int.up;
            }

            private void BuildPlayUi()
            {
                Clear();
                _host.SetTitle(PhoneLang.T("app.pip.snake", "Snake"));
                Transform left;
                Transform center;
                Transform right;
                PhoneGames.PlayLayout(_host, out left, out center, out right);
                _overParent = left;
                _scoreLabel = PhoneGames.HudBar(left, ScoreText(), BuildMenu);
                if (_dead)
                    PhoneGames.OverRow(left, StartGame, BuildMenu);

                float cell = PhoneGames.FitCell(Cols, Rows, 2f);
                var board = new GameObject("Board", typeof(RectTransform));
                board.transform.SetParent(center, false);
                var boardLe = board.AddComponent<LayoutElement>();
                float gridH = Rows * (cell + 2f) + 8f;
                boardLe.flexibleHeight = 0f;
                boardLe.flexibleWidth = 0f;
                boardLe.minHeight = gridH;
                boardLe.preferredHeight = gridH;
                boardLe.preferredWidth = Cols * (cell + 2f) + 8f;
                var grid = board.AddComponent<GridLayoutGroup>();
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = Cols;
                grid.spacing = new Vector2(2f, 2f);
                grid.cellSize = new Vector2(cell, cell);
                grid.childAlignment = TextAnchor.MiddleCenter;
                grid.padding = new RectOffset(2, 2, 2, 2);

                _cells = new Image[Cols * Rows];
                for (int i = 0; i < _cells.Length; i++)
                {
                    var tile = PhoneUi.CreateImage(board.transform, "C", PhoneUi.White(), new Color(0.08f, 0.09f, 0.1f, 1f));
                    _cells[i] = tile.GetComponent<Image>();
                    _cells[i].raycastTarget = false;
                }

                PhoneGames.Dpad(right, () => Face(Vector2Int.left), () => Face(Vector2Int.right), () => Face(Vector2Int.up), () => Face(Vector2Int.down));
            }

            private string ScoreText()
            {
                if (_dead)
                    return PhoneLang.T("game_over", "Game over") + "  " + _score + "    " + PhoneLang.T("best", "Best") + " " + PhoneTheme.SnakeHigh;
                return "Lv " + _level + "   " + _score + "    " + PhoneLang.T("best", "Best") + " " + PhoneTheme.SnakeHigh;
            }

            private void Face(Vector2Int dir)
            {
                if (_dead)
                    return;
                if (dir + _dir == Vector2Int.zero)
                    return;
                _pending = dir;
            }

            private System.Collections.IEnumerator Run()
            {
                _tick = 0f;
                while (_page == "play" && !_dead)
                {
                    ReadDir();
                    _tick += Time.unscaledDeltaTime;
                    if (_tick >= TickTime())
                    {
                        _tick = 0f;
                        Step();
                        Draw();
                    }
                    yield return null;
                }
            }

            private void ReadDir()
            {
                if ((PhoneKeys.Down(PhoneKeys.GameUp) || PhoneGames.Down(KeyCode.UpArrow)) && _dir != Vector2Int.down) _pending = Vector2Int.up;
                else if ((PhoneKeys.Down(PhoneKeys.GameDown) || PhoneGames.Down(KeyCode.DownArrow)) && _dir != Vector2Int.up) _pending = Vector2Int.down;
                else if ((PhoneKeys.Down(PhoneKeys.GameLeft) || PhoneGames.Down(KeyCode.LeftArrow)) && _dir != Vector2Int.right) _pending = Vector2Int.left;
                else if ((PhoneKeys.Down(PhoneKeys.GameRight) || PhoneGames.Down(KeyCode.RightArrow)) && _dir != Vector2Int.left) _pending = Vector2Int.right;
            }

            private float TickTime()
            {
                return Mathf.Max(0.055f, 0.18f - (_level - 1) * 0.03f);
            }

            private void Step()
            {
                _dir = _pending;
                Vector2Int next = _snake[0] + _dir;
                if (next.x < 0 || next.y < 0 || next.x >= Cols || next.y >= Rows)
                {
                    Die();
                    return;
                }
                for (int i = 0; i < _snake.Count; i++)
                {
                    if (_snake[i] == next)
                    {
                        Die();
                        return;
                    }
                }
                _snake.Insert(0, next);
                if (next == _food)
                {
                    _score++;
                    if (_snake.Count >= Cols * Rows)
                    {
                        _level++;
                        ResetSnake();
                        PlaceFood();
                        if (_scoreLabel != null)
                            _scoreLabel.text = ScoreText();
                        _host.ShowToast("Level " + _level);
                        return;
                    }
                    if (_scoreLabel != null)
                        _scoreLabel.text = ScoreText();
                    PlaceFood();
                }
                else
                    _snake.RemoveAt(_snake.Count - 1);
            }

            private void Die()
            {
                _dead = true;
                if (_score > PhoneTheme.SnakeHigh)
                {
                    PhoneTheme.SnakeHigh = _score;
                    PhoneTheme.Commit();
                    _host.ShowToast("New high score  " + _score);
                }
                else
                    _host.ShowToast("Score " + _score + "   Best " + PhoneTheme.SnakeHigh);
                if (_scoreLabel != null)
                    _scoreLabel.text = ScoreText();
                if (_overParent != null && _overParent.Find("Again") == null)
                    PhoneGames.OverRow(_overParent, StartGame, BuildMenu);
            }

            private void PlaceFood()
            {
                for (int n = 0; n < 200; n++)
                {
                    _food = new Vector2Int(Random.Range(0, Cols), Random.Range(0, Rows));
                    bool hit = false;
                    for (int i = 0; i < _snake.Count; i++)
                    {
                        if (_snake[i] == _food)
                        {
                            hit = true;
                            break;
                        }
                    }
                    if (!hit)
                        return;
                }
            }

            private void Draw()
            {
                if (_cells == null)
                    return;
                for (int i = 0; i < _cells.Length; i++)
                {
                    if (_cells[i] == null)
                        continue;
                    _cells[i].color = new Color(0.08f, 0.09f, 0.1f, 1f);
                }
                Set(_food, new Color(0.92f, 0.42f, 0.28f, 1f));
                for (int i = 0; i < _snake.Count; i++)
                    Set(_snake[i], i == 0 ? new Color(0.42f, 0.92f, 0.48f, 1f) : new Color(0.22f, 0.72f, 0.34f, 1f));
            }

            private void Set(Vector2Int p, Color c)
            {
                int i = (Rows - 1 - p.y) * Cols + p.x;
                if (i >= 0 && i < _cells.Length && _cells[i] != null)
                    _cells[i].color = c;
            }

            private void Clear()
            {
                for (int i = _host.Content.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(_host.Content.GetChild(i).gameObject);
            }
        }
    }
}
