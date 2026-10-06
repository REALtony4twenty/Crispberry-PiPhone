using System.Collections.Generic;
using System.Text;
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
                OnOpen = host => { _live = new Session(host); _live.StartGame(); },
                OnClose = () => { if (_live != null) _live.Halt(); _live = null; },
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

        internal static string CastState()
        {
            return _live == null ? "menu" : _live.ExportCast();
        }

        private sealed class Session
        {
            private const int Cols = 12;
            private const int Rows = 16;
            private readonly IPiPhoneHost _host;
            private string _page = "play";
            private readonly List<Vector2Int> _snake = new List<Vector2Int>();
            private Vector2Int _dir = Vector2Int.up;
            private Vector2Int _pending = Vector2Int.up;
            private Vector2Int _food;
            private float _tick;
            private bool _dead;
            private bool _won;
            private int _score;
            private int _level = 1;
            private int _runId;
            private Image[] _cells;
            private TextMeshProUGUI _scoreLabel;

            public Session(IPiPhoneHost host)
            {
                _host = host;
            }

            public bool GoBack()
            {
                return false;
            }

            internal void Halt()
            {
                _runId++;
                _page = "off";
            }

            internal string ExportCast()
            {
                if (_page != "play")
                    return "menu";
                var cells = new char[Cols * Rows];
                for (int i = 0; i < cells.Length; i++)
                    cells[i] = '.';
                int fx = (Rows - 1 - _food.y) * Cols + _food.x;
                if (fx >= 0 && fx < cells.Length)
                    cells[fx] = 'f';
                for (int i = 0; i < _snake.Count; i++)
                {
                    Vector2Int p = _snake[i];
                    int n = (Rows - 1 - p.y) * Cols + p.x;
                    if (n >= 0 && n < cells.Length)
                        cells[n] = i == 0 ? 'H' : 's';
                }
                var sb = new StringBuilder();
                sb.Append("play|").Append(_score).Append('|').Append(_dead ? '1' : '0').Append('|');
                sb.Append(cells);
                return sb.ToString();
            }

            public void Relayout()
            {
                if (_page != "play")
                    return;
                BuildPlayUi();
                Draw();
            }

            internal void StartGame()
            {
                _host.StartHostCoroutine(StartSoon());
            }

            private System.Collections.IEnumerator StartSoon()
            {
                yield return null;
                _runId++;
                int run = _runId;
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
                _host.StartHostCoroutine(Run(run));
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
                _scoreLabel = PhoneGames.HudBar(left, ScoreText(), () => _host.GoBack(), StartGame, "New");

                float cell = SnakeCell();
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

                PhoneGames.Dpad(right, () => Aim(Vector2Int.left), () => Aim(Vector2Int.right), () => Aim(Vector2Int.up), () => Aim(Vector2Int.down));
            }

            private string ScoreText()
            {
                if (_won)
                    return "Clear  " + _score + "    " + PhoneLang.T("best", "Best") + " " + PhoneTheme.SnakeHigh;
                if (_dead)
                    return PhoneLang.T("game_over", "Game over") + "  " + _score + "    " + PhoneLang.T("best", "Best") + " " + PhoneTheme.SnakeHigh;
                return "Lv " + _level + "   " + _score + "    " + PhoneLang.T("best", "Best") + " " + PhoneTheme.SnakeHigh;
            }

            private void Aim(Vector2Int dir)
            {
                if (_dead || _won)
                    return;
                if (dir + _dir == Vector2Int.zero || dir == _pending)
                    return;
                _pending = dir;
                PhoneSfx.Play("snake-turn");
            }

            private float SnakeCell()
            {
                float gap = 2f;
                float availW = _host.IsLandscape ? 560f : 392f;
                float availH = _host.IsLandscape ? PhoneGames.LandscapeBoardH : 700f;
                float cw = (availW - (Cols + 1) * gap) / Cols;
                float ch = (availH - (Rows + 1) * gap) / Rows;
                return Mathf.Clamp(Mathf.Floor(Mathf.Min(cw, ch)), 14f, 40f);
            }

            private System.Collections.IEnumerator Run(int run)
            {
                _tick = 0f;
                while (run == _runId && _page == "play" && !_dead && !_won)
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
                if ((PhoneKeys.Down(PhoneKeys.GameUp) || PhoneGames.Down(KeyCode.UpArrow)) && _dir != Vector2Int.down) Aim(Vector2Int.up);
                else if ((PhoneKeys.Down(PhoneKeys.GameDown) || PhoneGames.Down(KeyCode.DownArrow)) && _dir != Vector2Int.up) Aim(Vector2Int.down);
                else if ((PhoneKeys.Down(PhoneKeys.GameLeft) || PhoneGames.Down(KeyCode.LeftArrow)) && _dir != Vector2Int.right) Aim(Vector2Int.left);
                else if ((PhoneKeys.Down(PhoneKeys.GameRight) || PhoneGames.Down(KeyCode.RightArrow)) && _dir != Vector2Int.left) Aim(Vector2Int.right);
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
                    PhoneSfx.Play("snake-eat");
                    if (_snake.Count >= Cols * Rows)
                    {
                        _won = true;
                        PhoneSfx.Play("snake-win");
                        if (_score > PhoneTheme.SnakeHigh)
                        {
                            PhoneTheme.SnakeHigh = _score;
                            PhoneTheme.Commit();
                        }
                        if (_scoreLabel != null)
                            _scoreLabel.text = ScoreText();
                        _host.ShowToast("The board is full.");
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
                PhoneSounds.PlayTone(196, 0.2f, "snake-die");
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
                    _cells[i].sprite = PhoneUi.White();
                    _cells[i].preserveAspect = false;
                    _cells[i].color = new Color(0.08f, 0.09f, 0.1f, 1f);
                }
                if (!_won)
                {
                    Image food = CellImage(_food);
                    if (food != null)
                    {
                        Sprite apple = PhoneIcons.Logo();
                        food.sprite = apple != null ? apple : PhoneUi.White();
                        food.preserveAspect = apple != null;
                        food.color = apple != null ? Color.white : new Color(0.92f, 0.42f, 0.28f, 1f);
                    }
                }
                for (int i = 0; i < _snake.Count; i++)
                    Set(_snake[i], i == 0 ? new Color(0.42f, 0.92f, 0.48f, 1f) : new Color(0.22f, 0.72f, 0.34f, 1f));
            }

            private Image CellImage(Vector2Int p)
            {
                int i = (Rows - 1 - p.y) * Cols + p.x;
                if (i < 0 || i >= _cells.Length)
                    return null;
                return _cells[i];
            }

            private void Set(Vector2Int p, Color c)
            {
                Image img = CellImage(p);
                if (img == null)
                    return;
                img.sprite = PhoneUi.White();
                img.preserveAspect = false;
                img.color = c;
            }

            private void Clear()
            {
                for (int i = _host.Content.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(_host.Content.GetChild(i).gameObject);
            }
        }
    }
}
