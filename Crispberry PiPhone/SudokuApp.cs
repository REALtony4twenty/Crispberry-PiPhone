using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    internal static class SudokuApp
    {
        private static Session _live;

        internal static void Register()
        {
            PiPhoneApi.RegisterApp(new PiPhoneApp
            {
                Id = BuiltinApps.SudokuId,
                DisplayName = "Sudoku",
                IconGlyph = "9",
                IconBackground = new Color(0.22f, 0.48f, 0.62f, 1f),
                SortOrder = 76,
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
            private readonly int[] _given = new int[81];
            private readonly int[] _val = new int[81];
            private int _sel = -1;
            private int _diff;
            private bool _won;
            private Image[] _cells;
            private TextMeshProUGUI[] _labels;
            private TextMeshProUGUI _status;

            private static readonly string[] DiffName = { "Easy", "Medium", "Hard" };
            private static readonly int[] Punch = { 36, 46, 54 };

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
                _host.SetTitle(PhoneLang.T("app.pip.sudoku", "Sudoku"));
                PhoneUi.CreateButton(_host.Content, PhoneLang.T("play", "Play"), () => StartAt(0), new Vector2(220f, 48f));
                PhoneUi.CreateButton(_host.Content, "Easy", () => StartAt(0), new Vector2(200f, 40f));
                PhoneUi.CreateButton(_host.Content, "Medium", () => StartAt(1), new Vector2(200f, 40f));
                PhoneUi.CreateButton(_host.Content, "Hard", () => StartAt(2), new Vector2(200f, 40f));
                var high = PhoneUi.CreateLabel(_host.Content, "High", "Solved  " + PhoneTheme.HighSudoku, 16f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Size(high.gameObject, 28f);
            }

            private void StartAt(int diff)
            {
                _diff = diff < 0 ? 0 : (diff > 2 ? 2 : diff);
                _won = false;
                _sel = -1;
                NewPuzzle();
                _page = "play";
                BuildPlayUi();
            }

            private void NextPuzzle()
            {
                if (_diff < 2)
                    _diff++;
                StartAt(_diff);
            }

            private void BuildPlayUi()
            {
                PhoneGames.Clear(_host);
                _host.SetTitle(PhoneLang.T("app.pip.sudoku", "Sudoku"));
                Transform left;
                Transform center;
                Transform right;
                PhoneGames.PlayLayout(_host, out left, out center, out right);
                _status = PhoneGames.HudBar(left, StatusText(), BuildMenu);
                if (_won)
                    PhoneGames.OverRow(left, NextPuzzle, BuildMenu);

                float cell = PhoneGames.FitCell(9, 9, 2f, _host.IsLandscape ? 0f : 96f);
                if (cell > 38f)
                    cell = 38f;
                PhoneGames.Board(center, 9, 9, new Vector2(cell, cell), 2f, out _cells, out _labels, false);
                for (int i = 0; i < 81; i++)
                {
                    int idx = i;
                    _cells[i].raycastTarget = true;
                    var btn = _cells[i].gameObject.AddComponent<Button>();
                    btn.transition = Selectable.Transition.None;
                    btn.onClick.AddListener(() => Tap(idx));
                    if (_labels[i] != null)
                        _labels[i].raycastTarget = false;
                }

                var pad = new GameObject("Keys", typeof(RectTransform));
                pad.transform.SetParent(right, false);
                var ple = pad.AddComponent<LayoutElement>();
                ple.minHeight = _host.IsLandscape ? 200f : 80f;
                var keys = pad.AddComponent<GridLayoutGroup>();
                keys.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                keys.constraintCount = _host.IsLandscape ? 3 : 5;
                keys.cellSize = new Vector2(36f, 36f);
                keys.spacing = new Vector2(4f, 4f);
                keys.childAlignment = TextAnchor.MiddleCenter;
                for (int n = 1; n <= 9; n++)
                {
                    int v = n;
                    PhoneUi.CreateButton(pad.transform, v.ToString(), () => Enter(v), new Vector2(36f, 36f));
                }
                PhoneUi.CreateButton(pad.transform, "C", () => Enter(0), new Vector2(36f, 36f));
                Draw();
            }

            private string StatusText()
            {
                if (_won)
                    return "Solved!    " + PhoneTheme.HighSudoku;
                return DiffName[_diff] + "    " + PhoneTheme.HighSudoku + " solved";
            }

            private void Tap(int i)
            {
                if (_won || _given[i] != 0)
                    return;
                _sel = i;
                Draw();
            }

            private void Enter(int n)
            {
                if (_won || _sel < 0 || _given[_sel] != 0)
                    return;
                _val[_sel] = n;
                Draw();
                if (!Complete())
                    return;
                _won = true;
                PhoneGames.Remember(ref PhoneTheme.HighSudoku, PhoneTheme.HighSudoku + 1, _host);
                _host.ShowToast(_diff < 2 ? "Next is harder." : "Another hard puzzle.");
                BuildPlayUi();
            }

            private bool Complete()
            {
                for (int i = 0; i < 81; i++)
                {
                    if (_val[i] == 0 || Conflict(i, _val[i], _val))
                        return false;
                }
                return true;
            }

            private void Draw()
            {
                if (_status != null)
                    _status.text = StatusText();
                if (_cells == null)
                    return;
                for (int i = 0; i < 81; i++)
                {
                    int r = i / 9;
                    int c = i % 9;
                    bool band = ((r / 3) + (c / 3)) % 2 == 0;
                    Color bg = i == _sel
                        ? new Color(0.24f, 0.46f, 0.62f, 1f)
                        : (band ? new Color(0.12f, 0.14f, 0.18f, 1f) : new Color(0.08f, 0.09f, 0.11f, 1f));
                    if (_val[i] != 0 && Conflict(i, _val[i], _val))
                        bg = new Color(0.55f, 0.18f, 0.18f, 1f);
                    _cells[i].color = bg;
                    if (_labels[i] != null)
                    {
                        _labels[i].text = _val[i] == 0 ? string.Empty : _val[i].ToString();
                        _labels[i].color = _given[i] != 0 ? Color.white : new Color(0.55f, 0.82f, 1f, 1f);
                    }
                }
            }

            private void NewPuzzle()
            {
                int[] full = new int[81];
                Fill(full, 0);
                for (int i = 0; i < 81; i++)
                {
                    _given[i] = full[i];
                    _val[i] = full[i];
                }
                int holes = Punch[_diff];
                var order = new List<int>(81);
                for (int i = 0; i < 81; i++)
                    order.Add(i);
                for (int i = 80; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    int t = order[i];
                    order[i] = order[j];
                    order[j] = t;
                }
                for (int n = 0; n < holes; n++)
                {
                    int i = order[n];
                    _given[i] = 0;
                    _val[i] = 0;
                }
            }

            private static bool Fill(int[] grid, int i)
            {
                if (i >= 81)
                    return true;
                var nums = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
                for (int a = 8; a > 0; a--)
                {
                    int b = Random.Range(0, a + 1);
                    int t = nums[a];
                    nums[a] = nums[b];
                    nums[b] = t;
                }
                for (int n = 0; n < 9; n++)
                {
                    if (Conflict(i, nums[n], grid))
                        continue;
                    grid[i] = nums[n];
                    if (Fill(grid, i + 1))
                        return true;
                    grid[i] = 0;
                }
                return false;
            }

            private static bool Conflict(int i, int n, int[] grid)
            {
                int r = i / 9;
                int c = i % 9;
                for (int x = 0; x < 9; x++)
                {
                    int rowI = r * 9 + x;
                    int colI = x * 9 + c;
                    if (rowI != i && grid[rowI] == n)
                        return true;
                    if (colI != i && grid[colI] == n)
                        return true;
                }
                int br = (r / 3) * 3;
                int bc = (c / 3) * 3;
                for (int y = 0; y < 3; y++)
                {
                    for (int x = 0; x < 3; x++)
                    {
                        int b = (br + y) * 9 + (bc + x);
                        if (b != i && grid[b] == n)
                            return true;
                    }
                }
                return false;
            }
        }
    }
}
