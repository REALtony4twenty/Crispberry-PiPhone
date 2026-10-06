using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    internal static class MinesApp
    {
        private static Session _live;

        internal static void Register()
        {
            PiPhoneApi.RegisterApp(new PiPhoneApp
            {
                Id = BuiltinApps.MinesId,
                DisplayName = "Mines",
                IconGlyph = "*",
                IconBackground = new Color(0.42f, 0.48f, 0.55f, 1f),
                SortOrder = 72,
                ShowOnHome = true,
                OnOpen = host => { _live = new Session(host); _live.StartGame(); },
                OnClose = () => { if (_live != null) _live.Halt(); _live = null; },
                OnOrientation = () => { if (_live != null) _live.Relayout(); }
            });
        }

        internal static void Tick()
        {
            if (_live != null)
                _live.TickClock();
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
            private const int Cols = 8;
            private const int Rows = 10;
            private int _tier;
            private int MineCount
            {
                get { return Mathf.Min(32, 10 + _tier * 6); }
            }
            private readonly IPiPhoneHost _host;
            private string _page = "menu";
            private readonly bool[] _mine = new bool[Cols * Rows];
            private readonly bool[] _open = new bool[Cols * Rows];
            private readonly bool[] _flag = new bool[Cols * Rows];
            private bool _flagMode;
            private bool _started;
            private bool _dead;
            private bool _won;
            private bool _clockOn;
            private float _readyAt;
            private float _elapsed;
            private int _shownSec = -1;
            private int _revealed;
            private Image[] _cells;
            private TextMeshProUGUI[] _labels;
            private TextMeshProUGUI _status;
            private Button _flagBtn;
            private Transform _overParent;

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
                _page = "off";
                _clockOn = false;
            }

            internal string ExportCast()
            {
                if (_page != "play")
                    return "menu";
                var sb = new StringBuilder();
                sb.Append("play|").Append(_dead ? '1' : '0').Append('|');
                for (int i = 0; i < _open.Length; i++)
                {
                    if (_open[i] && _mine[i])
                        sb.Append('*');
                    else if (_open[i])
                        sb.Append((char)('0' + Mathf.Clamp(Count(i), 0, 8)));
                    else if (_flag[i])
                        sb.Append('F');
                    else
                        sb.Append('.');
                }
                return sb.ToString();
            }

            public void Relayout()
            {
                if (_page == "play")
                    BuildPlayUi();
            }

            internal void TickClock()
            {
                if (!_clockOn || _dead || _won || _page != "play")
                    return;
                _elapsed += Time.unscaledDeltaTime;
                int sec = Mathf.FloorToInt(_elapsed);
                if (sec == _shownSec)
                    return;
                PaintStatus();
            }

            private static string Clock(float seconds)
            {
                int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
                return (s / 60).ToString() + ":" + (s % 60).ToString("00");
            }

            internal void StartGame()
            {
                _host.StartHostCoroutine(StartSoon());
            }

            private IEnumerator StartSoon()
            {
                yield return null;
                BeginRound();
            }

            private void BeginRound()
            {
                _page = "play";
                _tier = 0;
                _clockOn = false;
                _elapsed = 0f;
                _shownSec = -1;
                _readyAt = Time.unscaledTime + 0.15f;
                _flagMode = false;
                _started = false;
                _dead = false;
                _won = false;
                _revealed = 0;
                for (int i = 0; i < _mine.Length; i++)
                {
                    _mine[i] = false;
                    _open[i] = false;
                    _flag[i] = false;
                }
                BuildPlayUi();
            }

            private void BuildPlayUi()
            {
                PhoneGames.Clear(_host);
                _host.SetTitle(PhoneLang.T("app.pip.mines", "Mines"));
                Transform left;
                Transform center;
                Transform right;
                PhoneGames.PlayLayout(_host, out left, out center, out right);
                _overParent = left;
                _status = PhoneGames.HudBar(left, StatusLine(0), () => _host.GoBack(), StartGame, "New");
                float cell = _host.IsLandscape ? MineCell() : PhoneGames.FitCell(Cols, Rows, 3f);
                PhoneGames.Board(center, Cols, Rows, new Vector2(cell, cell), 3f, out _cells, out _labels, false);
                for (int i = 0; i < _cells.Length; i++)
                {
                    int idx = i;
                    _cells[i].raycastTarget = true;
                    var btn = _cells[i].gameObject.AddComponent<Button>();
                    btn.transition = Selectable.Transition.None;
                    btn.onClick.AddListener(() => Click(idx));
                    if (_labels[i] != null)
                        _labels[i].raycastTarget = false;
                }

                _flagBtn = PhoneUi.CreateIconChip(right, PhoneLang.T("flag", "Flag"), PhoneIcons.Material("flag"), ToggleFlag, _flagMode, new Vector2(44f, 40f));
                Draw();
            }

            private float MineCell()
            {
                float gap = 3f;
                float availW = 560f;
                float availH = PhoneGames.LandscapeBoardH;
                float cw = (availW - (Cols + 1) * gap) / Cols;
                float ch = (availH - (Rows + 1) * gap) / Rows;
                return Mathf.Clamp(Mathf.Floor(Mathf.Min(cw, ch)), 16f, 48f);
            }

            private void ToggleFlag()
            {
                if (_dead || _won)
                    return;
                _flagMode = !_flagMode;
                var fill = _flagBtn != null ? _flagBtn.GetComponent<Image>() : null;
                if (fill != null)
                    fill.color = _flagMode ? PhoneUi.Accent : PhoneUi.SurfaceAlt;
                Transform art = _flagBtn != null ? _flagBtn.transform.Find("I") : null;
                if (art != null)
                {
                    var icon = art.GetComponent<Image>();
                    if (icon != null)
                        icon.color = _flagMode ? new Color(0.10f, 0.11f, 0.12f, 1f) : PhoneUi.ButtonText;
                }
            }

            private void Click(int i)
            {
                if (Time.unscaledTime < _readyAt || _dead || _won || i < 0 || i >= _mine.Length)
                    return;
                if (!_started)
                {
                    PlaceMines(i);
                    _started = true;
                }
                if (_flagMode)
                {
                    if (_open[i])
                        return;
                    _flag[i] = !_flag[i];
                    PhoneSfx.Play("mine-flag");
                    Draw();
                    return;
                }
                if (_flag[i] || _open[i])
                    return;
                if (_mine[i])
                {
                    _dead = true;
                    _clockOn = false;
                    PhoneSfx.Play("mine-boom");
                    RevealAll();
                    _host.ShowToast("Boom.");
                    if (_status != null)
                        _status.text = "Boom.    " + BestLabel();
                    Draw();
                    return;
                }
                if (!_clockOn)
                {
                    _clockOn = true;
                    _elapsed = 0f;
                }
                Flood(i);
                if (_revealed >= Cols * Rows - MineCount)
                {
                    _won = true;
                    _clockOn = false;
                    if (PhoneTheme.HighMinesTime <= 0.05f || _elapsed < PhoneTheme.HighMinesTime)
                    {
                        PhoneTheme.HighMinesTime = _elapsed;
                        PhoneTheme.Save();
                    }
                    if (_status != null)
                        _status.text = "Cleared in " + Clock(_elapsed) + "    Best " + Clock(PhoneTheme.HighMinesTime);
                    _host.ShowToast("Cleared in " + Clock(_elapsed) + ".");
                    PhoneSfx.Play("mine-win");
                }
                else
                    PhoneSfx.Play("mine-dig");
                Draw();
            }

            private void PlaceMines(int safe)
            {
                int placed = 0;
                int guard = 0;
                while (placed < MineCount && guard < 400)
                {
                    guard++;
                    int i = Random.Range(0, _mine.Length);
                    if (i == safe || _mine[i])
                        continue;
                    int sx = safe % Cols;
                    int sy = safe / Cols;
                    int x = i % Cols;
                    int y = i / Cols;
                    if (Mathf.Abs(x - sx) <= 1 && Mathf.Abs(y - sy) <= 1)
                        continue;
                    _mine[i] = true;
                    placed++;
                }
                while (placed < MineCount)
                {
                    int i = Random.Range(0, _mine.Length);
                    if (i == safe || _mine[i])
                        continue;
                    _mine[i] = true;
                    placed++;
                }
            }

            private void Flood(int start)
            {
                var stack = new int[Cols * Rows];
                int top = 0;
                stack[top++] = start;
                while (top > 0)
                {
                    int i = stack[--top];
                    if (i < 0 || i >= _mine.Length || _open[i] || _flag[i] || _mine[i])
                        continue;
                    _open[i] = true;
                    _revealed++;
                    if (Count(i) != 0)
                        continue;
                    int x = i % Cols;
                    int y = i / Cols;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0)
                                continue;
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= Cols || ny >= Rows)
                                continue;
                            stack[top++] = ny * Cols + nx;
                        }
                    }
                }
            }

            private int Count(int i)
            {
                int x = i % Cols;
                int y = i / Cols;
                int n = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0)
                            continue;
                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= Cols || ny >= Rows)
                            continue;
                        if (_mine[ny * Cols + nx])
                            n++;
                    }
                }
                return n;
            }

            private void RevealAll()
            {
                for (int i = 0; i < _open.Length; i++)
                    _open[i] = true;
            }

            private void PaintStatus()
            {
                PaintStatus(CountFlags());
            }

            private void PaintStatus(int flags)
            {
                _shownSec = Mathf.FloorToInt(_elapsed);
                if (_status != null && !_dead && !_won)
                    _status.text = StatusLine(flags);
            }

            private int CountFlags()
            {
                int flags = 0;
                for (int i = 0; i < _flag.Length; i++)
                {
                    if (_flag[i])
                        flags++;
                }
                return flags;
            }

            private string StatusLine(int flags)
            {
                return Mathf.Max(0, MineCount - flags) + " mines    " + Clock(_clockOn || _elapsed > 0.05f ? _elapsed : 0f) + "    " + BestLabel();
            }

            private string BestLabel()
            {
                return PhoneTheme.HighMinesTime > 0.05f ? "Best " + Clock(PhoneTheme.HighMinesTime) : "Best --";
            }

            private void Draw()
            {
                int flags = CountFlags();
                PaintStatus(flags);
                if (_cells == null)
                    return;
                for (int i = 0; i < _cells.Length; i++)
                {
                    if (_cells[i] == null)
                        continue;
                    if (_open[i])
                    {
                        if (_mine[i])
                        {
                            _cells[i].color = new Color(0.72f, 0.22f, 0.18f, 1f);
                            if (_labels[i] != null)
                            {
                                _labels[i].text = "*";
                                _labels[i].color = Color.white;
                            }
                        }
                        else
                        {
                            int n = Count(i);
                            _cells[i].color = new Color(0.16f, 0.18f, 0.20f, 1f);
                            if (_labels[i] != null)
                            {
                                _labels[i].text = n == 0 ? string.Empty : n.ToString();
                                _labels[i].color = Number(n);
                            }
                        }
                    }
                    else if (_flag[i])
                    {
                        _cells[i].color = new Color(0.72f, 0.55f, 0.18f, 1f);
                        if (_labels[i] != null)
                        {
                            _labels[i].text = "F";
                            _labels[i].color = Color.white;
                        }
                    }
                    else
                    {
                        _cells[i].color = new Color(0.28f, 0.32f, 0.36f, 1f);
                        if (_labels[i] != null)
                            _labels[i].text = string.Empty;
                    }
                }
            }

            private static Color Number(int n)
            {
                switch (n)
                {
                    case 1: return new Color(0.35f, 0.55f, 0.95f, 1f);
                    case 2: return new Color(0.28f, 0.72f, 0.38f, 1f);
                    case 3: return new Color(0.92f, 0.32f, 0.28f, 1f);
                    case 4: return new Color(0.45f, 0.32f, 0.82f, 1f);
                    case 5: return new Color(0.72f, 0.28f, 0.22f, 1f);
                    default: return new Color(0.20f, 0.70f, 0.72f, 1f);
                }
            }
        }
    }
}
