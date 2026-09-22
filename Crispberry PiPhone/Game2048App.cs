using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    internal static class Game2048App
    {
        private static Session _live;

        internal static void Register()
        {
            PiPhoneApi.RegisterApp(new PiPhoneApp
            {
                Id = BuiltinApps.Game2048Id,
                DisplayName = "2048",
                IconGlyph = "2",
                IconBackground = new Color(0.93f, 0.75f, 0.18f, 1f),
                IconForeground = new Color(0.22f, 0.18f, 0.16f, 1f),
                SortOrder = 71,
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
            private readonly IPiPhoneHost _host;
            private string _page = "menu";
            private readonly int[] _grid = new int[16];
            private int _score;
            private int _stage = 1;
            private bool _won;
            private bool _dead;
            private Image[] _cells;
            private TextMeshProUGUI[] _labels;
            private TextMeshProUGUI _scoreLabel;
            private float _cool;
            private bool _needRelease;
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
                else
                    BuildMenu();
            }

            public void BuildMenu()
            {
                _page = "menu";
                PhoneGames.Clear(_host);
                _host.SetTitle(PhoneLang.T("app.pip.2048", "2048"));
                PhoneUi.MaterialChip(_host.Content, "play", "Play", StartGame, new Vector2(40f, 40f));
                var high = PhoneUi.CreateLabel(_host.Content, "High", "High score  " + PhoneTheme.High2048, 16f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Size(high.gameObject, 28f);
                var hint = PhoneUi.CreateLabel(_host.Content, "Hint", "Slide tiles with WASD, arrows, or the pad. Combine matching numbers.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 48f);
            }

            private void StartGame()
            {
                _page = "play";
                _score = 0;
                _stage = 1;
                _won = false;
                _dead = false;
                _cool = 0f;
                _needRelease = false;
                for (int i = 0; i < 16; i++)
                    _grid[i] = 0;
                Spawn();
                Spawn();
                BuildPlayUi();
                Draw();
                _host.StartHostCoroutine(Run());
            }

            private void BuildPlayUi()
            {
                PhoneGames.Clear(_host);
                _host.SetTitle(PhoneLang.T("app.pip.2048", "2048"));
                Transform left;
                Transform center;
                Transform right;
                PhoneGames.PlayLayout(_host, out left, out center, out right);
                _overParent = left;
                _scoreLabel = PhoneGames.HudBar(left, ScoreText(), BuildMenu);
                if (_dead)
                    PhoneGames.OverRow(left, StartGame, BuildMenu);
                float cell = PhoneGames.FitCell(4, 4, 6f);
                PhoneGames.Board(center, 4, 4, new Vector2(cell, cell), 6f, out _cells, out _labels, false);
                PhoneGames.Dpad(right, () => TryMove(Vector2Int.left), () => TryMove(Vector2Int.right), () => TryMove(Vector2Int.up), () => TryMove(Vector2Int.down));
            }

            private string ScoreText()
            {
                if (_dead)
                    return PhoneLang.T("game_over", "Game over") + "  " + _score + "    " + PhoneLang.T("best", "Best") + " " + PhoneTheme.High2048;
                return "Lv " + _stage + "   " + _score + "    " + PhoneLang.T("best", "Best") + " " + PhoneTheme.High2048;
            }

            private System.Collections.IEnumerator Run()
            {
                while (_page == "play" && !_dead)
                {
                    _cool -= Time.unscaledDeltaTime;
                    if (_needRelease && !PhoneGames.DirHeld())
                        _needRelease = false;
                    Vector2Int dir;
                    if (!_needRelease && _cool <= 0f && PhoneGames.DirDown(out dir))
                        TryMove(dir);
                    yield return null;
                }
            }

            private void TryMove(Vector2Int dir)
            {
                if (_dead || dir == Vector2Int.zero || _cool > 0f)
                    return;
                Move(dir);
                _cool = 0.22f;
                _needRelease = true;
            }

            private void Move(Vector2Int dir)
            {
                if (_dead || dir == Vector2Int.zero)
                    return;
                int[] before = (int[])_grid.Clone();
                if (dir == Vector2Int.left)
                    SlideAll(true, false);
                else if (dir == Vector2Int.right)
                    SlideAll(true, true);
                else if (dir == Vector2Int.up)
                    SlideAll(false, false);
                else
                    SlideAll(false, true);
                bool changed = false;
                for (int i = 0; i < 16; i++)
                {
                    if (_grid[i] != before[i])
                        changed = true;
                }
                if (!changed)
                    return;
                Spawn();
                Draw();
                if (HasMove())
                    return;
                _dead = true;
                PhoneGames.Remember(ref PhoneTheme.High2048, _score, _host);
                if (_scoreLabel != null)
                    _scoreLabel.text = "Game over  " + _score + "    Best " + PhoneTheme.High2048;
                PhoneGames.OverRow(_overParent != null ? _overParent : _host.Content, StartGame, BuildMenu);
            }

            private void SlideAll(bool horizontal, bool reverse)
            {
                for (int i = 0; i < 4; i++)
                {
                    var line = new int[4];
                    for (int j = 0; j < 4; j++)
                    {
                        int x = horizontal ? j : i;
                        int y = horizontal ? i : j;
                        line[j] = _grid[y * 4 + x];
                    }
                    if (reverse)
                    {
                        System.Array.Reverse(line);
                        Slide(line);
                        System.Array.Reverse(line);
                    }
                    else
                        Slide(line);
                    for (int j = 0; j < 4; j++)
                    {
                        int x = horizontal ? j : i;
                        int y = horizontal ? i : j;
                        _grid[y * 4 + x] = line[j];
                    }
                }
            }

            private void Slide(int[] line)
            {
                var packed = new int[4];
                int n = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (line[i] != 0)
                    {
                        packed[n] = line[i];
                        n++;
                    }
                }
                int w = 0;
                for (int i = 0; i < n; i++)
                {
                    if (i + 1 < n && packed[i] > 0 && packed[i] == packed[i + 1])
                    {
                        packed[w] = packed[i] * 2;
                        _score += packed[w];
                        if (packed[w] == 2048 && !_won)
                        {
                            _won = true;
                            _stage = 2;
                            _host.ShowToast("Stage 2 — cursed tile");
                            InjectStone();
                        }
                        else if (packed[w] == 4096 && _stage < 3)
                        {
                            _stage = 3;
                            _host.ShowToast("Stage 3 — another curse");
                            InjectStone();
                        }
                        i++;
                    }
                    else
                        packed[w] = packed[i];
                    w++;
                }
                for (int i = w; i < 4; i++)
                    packed[i] = 0;
                for (int i = 0; i < 4; i++)
                    line[i] = packed[i];
            }

            private void InjectStone()
            {
                for (int n = 0; n < 16; n++)
                {
                    int i = Random.Range(0, 16);
                    if (_grid[i] == 0)
                    {
                        _grid[i] = -1;
                        return;
                    }
                }
            }

            private void Spawn()
            {
                int empty = 0;
                for (int i = 0; i < 16; i++)
                {
                    if (_grid[i] == 0)
                        empty++;
                }
                if (empty == 0)
                    return;
                int pick = Random.Range(0, empty);
                for (int i = 0; i < 16; i++)
                {
                    if (_grid[i] != 0)
                        continue;
                    if (pick == 0)
                    {
                        _grid[i] = Random.value < 0.9f ? 2 : 4;
                        return;
                    }
                    pick--;
                }
            }

            private bool HasMove()
            {
                for (int i = 0; i < 16; i++)
                {
                    if (_grid[i] == 0)
                        return true;
                    int x = i % 4;
                    int y = i / 4;
                    if (x < 3 && _grid[i] > 0 && _grid[i] == _grid[i + 1])
                        return true;
                    if (y < 3 && _grid[i] > 0 && _grid[i] == _grid[i + 4])
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
                for (int i = 0; i < 16; i++)
                {
                    int v = _grid[i];
                    if (_cells[i] != null)
                    {
                        if (v < 0)
                            _cells[i].color = new Color(0.18f, 0.16f, 0.14f, 1f);
                        else
                            _cells[i].color = v == 0 ? new Color(0.80f, 0.75f, 0.70f, 0.35f) : PhoneGames.Tile(v);
                    }
                    if (_labels[i] != null)
                    {
                        _labels[i].text = v == 0 ? string.Empty : (v < 0 ? "X" : v.ToString());
                        _labels[i].color = v < 0 ? new Color(0.85f, 0.55f, 0.35f, 1f) : PhoneGames.Ink(v);
                        _labels[i].fontSize = v >= 1000 ? 16f : 22f;
                    }
                }
            }
        }
    }
}
