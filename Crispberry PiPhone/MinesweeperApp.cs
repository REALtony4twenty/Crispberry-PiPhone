using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    internal static class MinesweeperApp
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
                PhoneUi.MaterialChip(_host.Content, "play", "Play", StartGame, new Vector2(40f, 40f));
                var high = PhoneUi.CreateLabel(_host.Content, "High", "Best clears  " + PhoneTheme.HighMines, 16f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Size(high.gameObject, 28f);
                var hint = PhoneUi.CreateLabel(_host.Content, "Hint", "Tap a cell to open it. Switch to Flag to mark mines. First tap is always safe.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 52f);
            }

            private void StartGame()
            {
                _tier = 0;
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
                _status = PhoneGames.HudBar(left, MineCount + " mines   Lv " + (_tier + 1) + "    Best " + PhoneTheme.HighMines, BuildMenu);
                if (_dead || _won)
                    PhoneGames.OverRow(left, _won ? (UnityEngine.Events.UnityAction)NextRound : StartGame, BuildMenu);
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
                    RevealAll();
                    _host.ShowToast("Boom.");
                    if (_status != null)
                        _status.text = "Boom.    Best " + PhoneTheme.HighMines;
                    PhoneGames.OverRow(_overParent != null ? _overParent : _host.Content, StartGame, BuildMenu);
                    Draw();
                    return;
                }
                Flood(i);
                if (_revealed >= Cols * Rows - MineCount)
                {
                    _won = true;
                    int best = PhoneTheme.HighMines + 1;
                    PhoneGames.Remember(ref PhoneTheme.HighMines, best, _host);
                    if (_status != null)
                        _status.text = "Cleared!  Lv " + (_tier + 2) + " next    Best " + PhoneTheme.HighMines;
                    _host.ShowToast("Cleared. Next board has more mines.");
                    PhoneGames.OverRow(_overParent != null ? _overParent : _host.Content, NextRound, BuildMenu);
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

            private void Draw()
            {
                int flags = 0;
                for (int i = 0; i < _flag.Length; i++)
                {
                    if (_flag[i])
                        flags++;
                }
                if (_status != null && !_dead && !_won)
                    _status.text = Mathf.Max(0, MineCount - flags) + " mines    Best " + PhoneTheme.HighMines;
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
