using System.Collections.Generic;
using System.Text;
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
                OnOpen = host => { _live = new Session(host); _live.StartGame(); },
                OnClose = () => { if (_live != null) _live.Halt(); _live = null; },
                OnOrientation = () => { if (_live != null) _live.Relayout(); }
            });
        }

        internal static bool TryGoBack()
        {
            return _live != null && _live.GoBack();
        }

        internal static string CastState()
        {
            return _live == null ? "menu" : _live.ExportCast();
        }

        private sealed class Session
        {
            private readonly IPiPhoneHost _host;
            private string _page = "play";
            private readonly int[] _grid = new int[16];
            private int _score;
            private int _stage = 1;
            private bool _won;
            private bool _dead;
            private bool _merged;
            private bool _jackpot;
            private Image[] _cells;
            private TextMeshProUGUI[] _labels;
            private TextMeshProUGUI _scoreLabel;
            private RectTransform _board;
            private bool _needRelease;
            private bool _busy;
            private int _runId;
            private int _moveGen;

            private struct SlideStep
            {
                public int From;
                public int To;
                public int Value;
            }

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
                _moveGen++;
                _page = "off";
            }

            internal string ExportCast()
            {
                if (_page != "play")
                    return "menu";
                var sb = new StringBuilder();
                sb.Append("play|").Append(_score).Append('|').Append(_dead ? '1' : '0').Append('|');
                for (int i = 0; i < _grid.Length; i++)
                {
                    if (i > 0)
                        sb.Append(',');
                    sb.Append(_grid[i]);
                }
                return sb.ToString();
            }

            public void Relayout()
            {
                _moveGen++;
                _busy = false;
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
                _moveGen++;
                int run = _runId;
                _page = "play";
                _score = 0;
                _stage = 1;
                _won = false;
                _dead = false;
                _busy = false;
                _needRelease = false;
                for (int i = 0; i < 16; i++)
                    _grid[i] = 0;
                Spawn();
                Spawn();
                BuildPlayUi();
                Draw();
                _host.StartHostCoroutine(Run(run));
            }

            private void BuildPlayUi()
            {
                PhoneGames.Clear(_host);
                _host.SetTitle(PhoneLang.T("app.pip.2048", "2048"));
                Transform left;
                Transform center;
                Transform right;
                PhoneGames.PlayLayout(_host, out left, out center, out right);
                _scoreLabel = PhoneGames.HudBar(left, ScoreText(), () => _host.GoBack(), StartGame, "New");
                float cell = BoardCell();
                var board = PhoneGames.Board(center, 4, 4, new Vector2(cell, cell), 6f, out _cells, out _labels, false);
                _board = board != null ? board.GetComponent<RectTransform>() : null;
                PhoneGames.Dpad(right, () => TryMove(Vector2Int.left), () => TryMove(Vector2Int.right), () => TryMove(Vector2Int.up), () => TryMove(Vector2Int.down));
            }

            private float BoardCell()
            {
                if (!_host.IsLandscape)
                    return PhoneGames.FitCell(4, 4, 6f);
                float gap = 6f;
                float cw = (480f - 5f * gap) / 4f;
                float ch = (PhoneGames.LandscapeBoardH - 5f * gap) / 4f;
                return Mathf.Clamp(Mathf.Floor(Mathf.Min(cw, ch)), 28f, 88f);
            }

            private string ScoreText()
            {
                if (_dead)
                    return PhoneLang.T("game_over", "Game over") + "  " + _score + "    " + PhoneLang.T("best", "Best") + " " + PhoneTheme.High2048;
                return "Lv " + _stage + "   " + _score + "    " + PhoneLang.T("best", "Best") + " " + PhoneTheme.High2048;
            }

            private System.Collections.IEnumerator Run(int run)
            {
                while (run == _runId && _page == "play" && !_dead)
                {
                    if (_needRelease && !PhoneGames.DirHeld())
                        _needRelease = false;
                    Vector2Int dir;
                    if (!_busy && !_needRelease && PhoneGames.DirDown(out dir))
                        TryMove(dir);
                    yield return null;
                }
            }

            private void TryMove(Vector2Int dir)
            {
                if (_busy || _dead || dir == Vector2Int.zero)
                    return;
                var steps = new List<SlideStep>();
                _merged = false;
                _jackpot = false;
                if (!PlaySlide(dir, steps))
                    return;
                if (_jackpot)
                    PhoneSfx.Play("g2048-win");
                else if (_merged)
                    PhoneSfx.Play("g2048-merge");
                else
                    PhoneSfx.Play("g2048-slide");
                int born = Spawn();
                _busy = true;
                _needRelease = true;
                _moveGen++;
                _host.StartHostCoroutine(Animate(steps, born, _moveGen));
            }

            private System.Collections.IEnumerator Animate(List<SlideStep> steps, int born, int gen)
            {
                yield return null;
                if (gen != _moveGen)
                    yield break;
                PhoneGames.Flush();
                Blank();
                var movers = new List<RectTransform>();
                var from = new List<Vector3>();
                var to = new List<Vector3>();
                Transform layer = _board != null && _board.parent != null ? _board.parent : _board;
                if (layer != null && _cells != null)
                {
                    for (int i = 0; i < steps.Count; i++)
                    {
                        SlideStep step = steps[i];
                        if (step.From < 0 || step.To < 0 || step.From >= _cells.Length || step.To >= _cells.Length)
                            continue;
                        if (_cells[step.From] == null || _cells[step.To] == null)
                            continue;
                        RectTransform tile = PhoneUi.CreateImage(layer, "Slide", PhoneUi.White(), TileColor(step.Value));
                        PhoneUi.IgnoreLayout(tile.gameObject);
                        tile.sizeDelta = _cells[step.To].rectTransform.rect.size;
                        tile.position = _cells[step.From].rectTransform.position;
                        var lab = PhoneUi.CreateLabel(tile, "N", TileText(step.Value), step.Value >= 1000 ? 16f : 22f, FontStyles.Bold, TextAlignmentOptions.Center);
                        lab.color = TileInk(step.Value);
                        PhoneUi.Stretch(lab.rectTransform, 0f, 0f);
                        movers.Add(tile);
                        from.Add(_cells[step.From].rectTransform.position);
                        to.Add(_cells[step.To].rectTransform.position);
                    }
                }
                float dur = 0.16f;
                float t = 0f;
                while (t < dur && gen == _moveGen)
                {
                    t += Time.unscaledDeltaTime;
                    float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                    for (int i = 0; i < movers.Count; i++)
                    {
                        if (movers[i] != null)
                            movers[i].position = Vector3.Lerp(from[i], to[i], u);
                    }
                    yield return null;
                }
                for (int i = 0; i < movers.Count; i++)
                {
                    if (movers[i] != null)
                        Object.Destroy(movers[i].gameObject);
                }
                if (gen != _moveGen)
                    yield break;
                int bornValue = 0;
                if (born >= 0 && born < _grid.Length)
                {
                    bornValue = _grid[born];
                    _grid[born] = 0;
                }
                Draw();
                if (born >= 0 && bornValue != 0 && _cells != null && born < _cells.Length && _cells[born] != null)
                {
                    _grid[born] = bornValue;
                    PaintCell(born);
                    RectTransform rt = _cells[born].rectTransform;
                    float pop = 0f;
                    while (pop < 0.1f && gen == _moveGen)
                    {
                        pop += Time.unscaledDeltaTime;
                        rt.localScale = Vector3.one * Mathf.Lerp(0.15f, 1f, Mathf.Clamp01(pop / 0.1f));
                        yield return null;
                    }
                    if (rt != null)
                        rt.localScale = Vector3.one;
                }
                if (gen != _moveGen)
                    yield break;
                Draw();
                if (!HasMove())
                {
                    _dead = true;
                    PhoneSfx.Play("g2048-dead");
                    PhoneGames.Remember(ref PhoneTheme.High2048, _score, _host);
                    if (_scoreLabel != null)
                        _scoreLabel.text = "Game over  " + _score + "    Best " + PhoneTheme.High2048;
                }
                _busy = false;
            }

            private bool PlaySlide(Vector2Int dir, List<SlideStep> steps)
            {
                bool horizontal = dir.x != 0;
                bool towardStart = dir == Vector2Int.left || dir == Vector2Int.up;
                bool changed = false;
                for (int i = 0; i < 4; i++)
                {
                    var line = new int[4];
                    var origin = new int[4];
                    for (int j = 0; j < 4; j++)
                    {
                        int x = horizontal ? (towardStart ? j : 3 - j) : i;
                        int y = horizontal ? i : (towardStart ? j : 3 - j);
                        int idx = y * 4 + x;
                        line[j] = _grid[idx];
                        origin[j] = idx;
                    }
                    if (Compress(line, origin, steps))
                        changed = true;
                    for (int j = 0; j < 4; j++)
                    {
                        int x = horizontal ? (towardStart ? j : 3 - j) : i;
                        int y = horizontal ? i : (towardStart ? j : 3 - j);
                        _grid[y * 4 + x] = line[j];
                    }
                }
                if (!changed)
                    steps.Clear();
                return changed;
            }

            private bool Compress(int[] line, int[] origin, List<SlideStep> steps)
            {
                var vals = new int[4];
                var from = new int[4];
                int n = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (line[i] == 0)
                        continue;
                    vals[n] = line[i];
                    from[n] = origin[i];
                    n++;
                }
                var packed = new int[4];
                int w = 0;
                bool moved = false;
                for (int i = 0; i < n; i++)
                {
                    int dest = origin[w];
                    if (i + 1 < n && vals[i] > 0 && vals[i] == vals[i + 1])
                    {
                        packed[w] = vals[i] * 2;
                        _merged = true;
                        _score += packed[w];
                        steps.Add(new SlideStep { From = from[i], To = dest, Value = vals[i] });
                        steps.Add(new SlideStep { From = from[i + 1], To = dest, Value = vals[i] });
                        if (from[i] != dest || from[i + 1] != dest)
                            moved = true;
                        if (packed[w] == 2048 && !_won)
                        {
                            _won = true;
                            _jackpot = true;
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
                    {
                        packed[w] = vals[i];
                        steps.Add(new SlideStep { From = from[i], To = dest, Value = vals[i] });
                        if (from[i] != dest)
                            moved = true;
                    }
                    w++;
                }
                for (int i = 0; i < 4; i++)
                {
                    if (line[i] != packed[i])
                        moved = true;
                    line[i] = packed[i];
                }
                return moved;
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

            private int Spawn()
            {
                int empty = 0;
                for (int i = 0; i < 16; i++)
                {
                    if (_grid[i] == 0)
                        empty++;
                }
                if (empty == 0)
                    return -1;
                int pick = Random.Range(0, empty);
                for (int i = 0; i < 16; i++)
                {
                    if (_grid[i] != 0)
                        continue;
                    if (pick == 0)
                    {
                        _grid[i] = Random.value < 0.9f ? 2 : 4;
                        return i;
                    }
                    pick--;
                }
                return -1;
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
                    if (_cells[i] != null)
                        _cells[i].rectTransform.localScale = Vector3.one;
                    PaintCell(i);
                }
            }

            private void Blank()
            {
                if (_cells == null)
                    return;
                for (int i = 0; i < 16; i++)
                {
                    if (_cells[i] != null)
                        _cells[i].color = new Color(0.80f, 0.75f, 0.70f, 0.35f);
                    if (_labels != null && i < _labels.Length && _labels[i] != null)
                        _labels[i].text = string.Empty;
                }
            }

            private void PaintCell(int i)
            {
                if (_cells == null || i < 0 || i >= _grid.Length || _cells[i] == null)
                    return;
                int v = _grid[i];
                _cells[i].color = TileColor(v);
                if (_labels != null && i < _labels.Length && _labels[i] != null)
                {
                    _labels[i].text = TileText(v);
                    _labels[i].color = TileInk(v);
                    _labels[i].fontSize = v >= 1000 ? 16f : 22f;
                }
            }

            private static Color TileColor(int v)
            {
                if (v < 0)
                    return new Color(0.18f, 0.16f, 0.14f, 1f);
                if (v == 0)
                    return new Color(0.80f, 0.75f, 0.70f, 0.35f);
                return PhoneGames.Tile(v);
            }

            private static string TileText(int v)
            {
                if (v == 0)
                    return string.Empty;
                return v < 0 ? "X" : v.ToString();
            }

            private static Color TileInk(int v)
            {
                if (v < 0)
                    return new Color(0.85f, 0.55f, 0.35f, 1f);
                return PhoneGames.Ink(v);
            }
        }
    }
}
