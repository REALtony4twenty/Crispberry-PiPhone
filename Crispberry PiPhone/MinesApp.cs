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
                OnOpen = host => { _live = new Session(host); _live.BuildMenu(); },
                OnClose = () => { _live = null; },
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
            private bool _timed;
            private bool _clockOn;
            private float _elapsed;
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
                if (_page == "menu")
                    return false;
                BuildMenu();
                return true;
            }

            public void Relayout()
            {
                if (_page == "play")
                    BuildPlayUi();
                else
                    BuildMenu();
            }

            public void BuildMenu()
            {
                _page = "menu";
                PhoneGames.Clear(_host);
                _host.SetTitle(PhoneLang.T("app.pip.mines", "Mines"));
                var modes = new GameObject("Modes", typeof(RectTransform));
                modes.transform.SetParent(_host.Content, false);
                PhoneUi.Size(modes, 44f);
                var modeRow = PhoneUi.AddHorizontal(modes, 8f);
                modeRow.childForceExpandWidth = false;
                modeRow.childAlignment = TextAnchor.MiddleCenter;
                PhoneUi.CreateButton(modes.transform, "Play", () => StartGame(false), new Vector2(120f, 40f));
                PhoneUi.CreateButton(modes.transform, "Timed", () => StartGame(true), new Vector2(120f, 40f));
                string best = "Best clears  " + PhoneTheme.HighMines;
                if (PhoneTheme.HighMinesTime > 0.05f)
                    best += "    Best time  " + Clock(PhoneTheme.HighMinesTime);
                var high = PhoneUi.CreateLabel(_host.Content, "High", best, 16f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Size(high.gameObject, 28f);
                var hint = PhoneUi.CreateLabel(_host.Content, "Hint", "Play has no clock. Timed starts on the first open. A lower time is better. First tap is always safe.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 52f);
            }

            internal void TickClock()
            {
                if (!_timed || !_clockOn || _dead || _won || _page != "play")
                    return;
                _elapsed += Time.unscaledDeltaTime;
                PaintStatus();
            }

            private static string Clock(float seconds)
            {
                int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
                return (s / 60).ToString() + ":" + (s % 60).ToString("00");
            }

            private void StartGame(bool timed)
            {
                _timed = timed;
                _tier = 0;
                _clockOn = false;
                _elapsed = 0f;
                BeginRound();
            }

            private void NextRound()
            {
                _tier++;
                BeginRound();
            }

            private void BeginRound()
            {
                _page = "play";
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
                _status = PhoneGames.HudBar(left, StatusLine(0), BuildMenu);
                if (_dead || _won)
                    PhoneGames.OverRow(left, _won && !_timed ? (UnityEngine.Events.UnityAction)NextRound : () => StartGame(_timed), BuildMenu);
                float cell = PhoneGames.FitCell(Cols, Rows, 3f);
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
                if (_dead || _won || i < 0 || i >= _mine.Length)
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
                    Draw();
                    return;
                }
                if (_flag[i] || _open[i])
                    return;
                if (_mine[i])
                {
                    _dead = true;
                    _clockOn = false;
                    RevealAll();
                    _host.ShowToast("Boom.");
                    if (_status != null)
                        _status.text = "Boom.    " + BestLabel();
                    PhoneGames.OverRow(_overParent != null ? _overParent : _host.Content, () => StartGame(_timed), BuildMenu);
                    Draw();
                    return;
                }
                if (_timed && !_clockOn)
                {
                    _clockOn = true;
                    _elapsed = 0f;
                }
                Flood(i);
                if (_revealed >= Cols * Rows - MineCount)
                {
                    _won = true;
                    _clockOn = false;
                    if (_timed)
                    {
                        if (PhoneTheme.HighMinesTime <= 0.05f || _elapsed < PhoneTheme.HighMinesTime)
                        {
                            PhoneTheme.HighMinesTime = _elapsed;
                            PhoneTheme.Save();
                        }
                        if (_status != null)
                            _status.text = "Cleared in " + Clock(_elapsed) + "    Best " + Clock(PhoneTheme.HighMinesTime);
                        _host.ShowToast("Cleared in " + Clock(_elapsed) + ".");
                    }
                    else
                    {
                        int best = PhoneTheme.HighMines + 1;
                        PhoneGames.Remember(ref PhoneTheme.HighMines, best, _host);
                        if (_status != null)
                            _status.text = "Cleared!  Lv " + (_tier + 2) + " next    Best " + PhoneTheme.HighMines;
                        _host.ShowToast("Cleared. Next board has more mines.");
                    }
                    PhoneGames.OverRow(_overParent != null ? _overParent : _host.Content, _timed ? (UnityEngine.Events.UnityAction)(() => StartGame(true)) : NextRound, BuildMenu);
                }
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
                string line = Mathf.Max(0, MineCount - flags) + " mines";
                if (_timed)
                    line += "    " + Clock(_elapsed);
                else
                    line += "   Lv " + (_tier + 1);
                line += "    " + BestLabel();
                return line;
            }

            private string BestLabel()
            {
                if (_timed)
                    return PhoneTheme.HighMinesTime > 0.05f ? "Best " + Clock(PhoneTheme.HighMinesTime) : "Best --";
                return "Best " + PhoneTheme.HighMines;
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
