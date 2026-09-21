using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PhotonPlayer = Photon.Realtime.Player;

namespace Crispberry_PiPhone
{
    internal static class Connect4App
    {
        private const int Cols = 7;
        private const int Rows = 6;
        private static Session _live;
        private static readonly List<Game> Games = new List<Game>();
        private static bool _loaded;

        internal static void Register()
        {
            Load();
            PiPhoneApi.RegisterApp(new PiPhoneApp
            {
                Id = BuiltinApps.Connect4Id,
                DisplayName = "Four Across",
                IconGlyph = "4",
                IconBackground = new Color(0.16f, 0.42f, 0.82f, 1f),
                IconSprite = PhoneIcons.PaintFourAcross(),
                SortOrder = 74,
                ShowOnHome = true,
                OnOpen = host => { _live = new Session(host); _live.ShowMenu(); },
                OnClose = () => { _live = null; },
                OnOrientation = () => { if (_live != null) _live.Relayout(); }
            });
        }

        internal static bool TryGoBack()
        {
            return _live != null && _live.GoBack();
        }

        internal static void OnNet(object[] data)
        {
            if (data == null || data.Length < 5)
                return;
            string op = data[3] as string;
            string id = data[4] as string;
            if (string.IsNullOrEmpty(op) || string.IsNullOrEmpty(id))
                return;
            if (op == "invite")
            {
                Game g = Find(id) ?? NewGame(id, false);
                g.HostId = data.Length > 6 ? data[6] as string : g.HostId;
                g.HostName = data.Length > 5 ? data[5] as string : g.HostName;
                g.GuestId = BuiltinApps.LocalId();
                g.GuestName = BuiltinApps.LocalName();
                g.Status = "open";
                Save();
                PhoneNotify.Post(BuiltinApps.Connect4Id, "Four Across", (g.HostName ?? "Scout") + " started a lobby.");
                if (_live != null)
                    _live.ShowMenu();
                return;
            }
            Game game = Find(id);
            if (game == null)
                return;
            if (op == "join")
            {
                game.GuestId = data.Length > 6 ? data[6] as string : game.GuestId;
                game.GuestName = data.Length > 5 ? data[5] as string : game.GuestName;
                game.Status = "active";
                Save();
                if (_live != null && _live.Is(game.Id))
                    _live.ShowBoard(game);
                return;
            }
            if (op == "move" && data.Length > 5)
            {
                int col = data[5] is int ? (int)data[5] : Convert.ToInt32(data[5]);
                ApplyMove(game, col, false);
                if (_live != null && _live.Is(game.Id))
                    _live.ShowBoard(game);
                return;
            }
            if (op == "leave")
            {
                game.Status = "open";
                Save();
                if (_live != null)
                    _live.ShowMenu();
                return;
            }
            if (op == "replay")
            {
                ResetBoard(game);
                Save();
                if (_live != null && _live.Is(game.Id))
                    _live.ShowBoard(game);
            }
        }

        private static void ResetBoard(Game g)
        {
            if (g == null)
                return;
            g.Board = new string('0', Cols * Rows);
            g.Turn = 1;
            g.Status = "active";
        }

        private static Game NewGame(string id, bool ai)
        {
            var g = new Game
            {
                Id = string.IsNullOrEmpty(id) ? PhoneStore.NewId() : id,
                HostId = BuiltinApps.LocalId(),
                HostName = BuiltinApps.LocalName(),
                Board = new string('0', Cols * Rows),
                Turn = 1,
                Status = ai ? "active" : "open",
                Ai = ai
            };
            if (ai)
            {
                g.GuestId = "cpu";
                g.GuestName = "CPU";
            }
            Games.Insert(0, g);
            Save();
            return g;
        }

        private static Game Find(string id)
        {
            for (int i = 0; i < Games.Count; i++)
            {
                if (Games[i] != null && Games[i].Id == id)
                    return Games[i];
            }
            return null;
        }

        private static bool Drop(Game g, int col, int player)
        {
            if (g == null || col < 0 || col >= Cols || g.Status == "done")
                return false;
            char[] cells = g.Board.ToCharArray();
            for (int r = 0; r < Rows; r++)
            {
                int i = r * Cols + col;
                if (cells[i] == '0')
                {
                    cells[i] = player == 1 ? '1' : '2';
                    g.Board = new string(cells);
                    return true;
                }
            }
            return false;
        }

        private static void ApplyMove(Game g, int col, bool local)
        {
            if (g == null || g.Status == "done")
                return;
            int me = Mine(g) ? 1 : 2;
            int player = local ? me : (me == 1 ? 2 : 1);
            if (g.Ai && local)
                player = g.Turn;
            if (!Drop(g, col, player))
                return;
            if (Winner(g.Board) != 0 || Full(g.Board))
                g.Status = "done";
            else
                g.Turn = g.Turn == 1 ? 2 : 1;
            Save();
            if (local && !g.Ai)
            {
                int actor = OtherActor(g);
                if (actor > 0)
                    PhoneNet.SendGame(actor, "move", g.Id, col);
            }
            if (g.Ai && g.Status != "done" && g.Turn == 2)
            {
                int aiCol = BestAi(g.Board);
                Drop(g, aiCol, 2);
                if (Winner(g.Board) != 0 || Full(g.Board))
                    g.Status = "done";
                else
                    g.Turn = 1;
                Save();
            }
        }

        private static bool Mine(Game g)
        {
            return g != null && g.HostId == BuiltinApps.LocalId();
        }

        private static int OtherActor(Game g)
        {
            string id = Mine(g) ? g.GuestId : g.HostId;
            PhotonPlayer p = BuiltinApps.FindById(id);
            return p != null ? p.ActorNumber : 0;
        }

        private static int Cell(string board, int c, int r)
        {
            if (c < 0 || c >= Cols || r < 0 || r >= Rows)
                return 0;
            return board[r * Cols + c] - '0';
        }

        private static int Winner(string board)
        {
            int[] dx = { 1, 0, 1, 1 };
            int[] dy = { 0, 1, 1, -1 };
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    int p = Cell(board, c, r);
                    if (p == 0)
                        continue;
                    for (int d = 0; d < 4; d++)
                    {
                        int n = 1;
                        for (int k = 1; k < 4; k++)
                        {
                            if (Cell(board, c + dx[d] * k, r + dy[d] * k) != p)
                                break;
                            n++;
                        }
                        if (n >= 4)
                            return p;
                    }
                }
            }
            return 0;
        }

        private static bool Full(string board)
        {
            return board.IndexOf('0') < 0;
        }

        private static int BestAi(string board)
        {
            int[] order = { 3, 2, 4, 1, 5, 0, 6 };
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < order.Length; i++)
                {
                    int col = order[i];
                    string next = TryDrop(board, col, pass == 1 ? 1 : 2);
                    if (next == null)
                        continue;
                    if (pass == 0 && Winner(next) == 2)
                        return col;
                    if (pass == 1 && Winner(next) == 1)
                        return col;
                    if (pass == 2)
                        return col;
                }
            }
            return 3;
        }

        private static string TryDrop(string board, int col, int player)
        {
            char[] cells = board.ToCharArray();
            for (int r = 0; r < Rows; r++)
            {
                int i = r * Cols + col;
                if (cells[i] == '0')
                {
                    cells[i] = player == 1 ? '1' : '2';
                    return new string(cells);
                }
            }
            return null;
        }

        private static void Load()
        {
            if (_loaded)
                return;
            _loaded = true;
            Games.Clear();
            string path = Path.Combine(PhoneStore.RootDir, "connect4.txt");
            if (!File.Exists(path))
                return;
            try
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string[] p = lines[i].Split('|');
                    if (p.Length < 8)
                        continue;
                    Games.Add(new Game
                    {
                        Id = p[0],
                        HostId = p[1],
                        GuestId = p[2],
                        HostName = p[3],
                        GuestName = p[4],
                        Board = p[5].Length == Cols * Rows ? p[5] : new string('0', Cols * Rows),
                        Turn = p[6] == "2" ? 2 : 1,
                        Status = p[7],
                        Ai = p.Length > 8 && p[8] == "1"
                    });
                }
            }
            catch (Exception ex)
            {
                Plugin.LogError("Four Across load: " + ex.Message);
            }
        }

        private static void Save()
        {
            try
            {
                PhoneStore.EnsureDir();
                var sb = new StringBuilder();
                for (int i = 0; i < Games.Count; i++)
                {
                    Game g = Games[i];
                    if (g == null)
                        continue;
                    sb.Append(g.Id).Append('|').Append(g.HostId).Append('|').Append(g.GuestId).Append('|')
                        .Append(g.HostName).Append('|').Append(g.GuestName).Append('|').Append(g.Board).Append('|')
                        .Append(g.Turn).Append('|').Append(g.Status).Append('|').Append(g.Ai ? "1" : "0").Append('\n');
                }
                File.WriteAllText(Path.Combine(PhoneStore.RootDir, "connect4.txt"), sb.ToString());
            }
            catch (Exception ex)
            {
                Plugin.LogError("Four Across save: " + ex.Message);
            }
        }

        private sealed class Game
        {
            public string Id;
            public string HostId;
            public string GuestId;
            public string HostName;
            public string GuestName;
            public string Board;
            public int Turn;
            public string Status;
            public bool Ai;
        }

        private sealed class Session
        {
            private readonly IPiPhoneHost _host;
            private string _page = "menu";
            private string _gameId;

            public Session(IPiPhoneHost host)
            {
                _host = host;
            }

            public bool Is(string id)
            {
                return _page == "board" && _gameId == id;
            }

            public bool GoBack()
            {
                if (_page == "menu")
                    return false;
                ShowMenu();
                return true;
            }

            public void Relayout()
            {
                if (_page == "board")
                {
                    Game g = Find(_gameId);
                    if (g != null)
                        ShowBoard(g);
                    else
                        ShowMenu();
                }
                else
                    ShowMenu();
            }

            public void ShowMenu()
            {
                _page = "menu";
                _gameId = null;
                PhoneGames.Clear(_host);
                _host.SetTitle(PhoneLang.T("app.pip.connect4", "Four Across"));
                PhoneUi.CreateButton(_host.Content, "Play vs CPU", () =>
                {
                    Game g = NewGame(null, true);
                    ShowBoard(g);
                }, new Vector2(240f, 44f));
                PhotonPlayer[] others = BuiltinApps.OtherPlayers();
                if (others.Length == 0)
                {
                    var empty = PhoneUi.CreateLabel(_host.Content, "E", "No scouts nearby for a lobby.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                    empty.color = PhoneUi.TextDim;
                    PhoneUi.Size(empty.gameObject, 28f);
                }
                else
                {
                    var lab = PhoneUi.CreateLabel(_host.Content, "L", "Start a lobby", 14f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                    PhoneUi.Size(lab.gameObject, 22f);
                    for (int i = 0; i < others.Length; i++)
                    {
                        PhotonPlayer p = others[i];
                        string name = BuiltinApps.PlayerName(p);
                        int actor = p.ActorNumber;
                        string pid = BuiltinApps.PlayerId(p);
                        PhoneUi.CreateButton(_host.Content, name, () => StartLobby(actor, pid, name), new Vector2(240f, 36f));
                    }
                }
                var mine = PhoneUi.CreateLabel(_host.Content, "M", "Lobbies", 14f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                PhoneUi.Size(mine.gameObject, 22f);
                int shown = 0;
                string me = BuiltinApps.LocalId();
                for (int i = 0; i < Games.Count; i++)
                {
                    Game g = Games[i];
                    if (g == null || (g.HostId != me && g.GuestId != me))
                        continue;
                    Game captured = g;
                    string label = (g.Ai ? "CPU" : (g.HostName + " vs " + (string.IsNullOrEmpty(g.GuestName) ? "?" : g.GuestName)))
                        + "  " + (g.Status == "done" ? "finished" : (g.Status == "open" ? "waiting" : "in play"));
                    var row = new GameObject("G", typeof(RectTransform));
                    row.transform.SetParent(_host.Content, false);
                    PhoneUi.Size(row, 40f);
                    PhoneUi.AddHorizontal(row, 6f);
                    PhoneUi.CreateButton(row.transform, label, () => ShowBoard(captured), new Vector2(200f, 36f)).GetComponent<LayoutElement>().flexibleWidth = 1f;
                    PhoneUi.CreateButton(row.transform, "X", () =>
                    {
                        Games.Remove(captured);
                        Save();
                        ShowMenu();
                    }, new Vector2(36f, 36f));
                    shown++;
                }
                if (shown == 0)
                {
                    var none = PhoneUi.CreateLabel(_host.Content, "N", "No saved games yet.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                    none.color = PhoneUi.TextDim;
                    PhoneUi.Size(none.gameObject, 24f);
                }
            }

            private void StartLobby(int actor, string pid, string name)
            {
                Game g = NewGame(null, false);
                g.GuestId = pid;
                g.GuestName = name;
                Save();
                PhoneNet.SendGame(actor, "invite", g.Id, BuiltinApps.LocalName(), BuiltinApps.LocalId());
                _host.ShowToast("Lobby ready. They can open Four Across anytime.");
                ShowBoard(g);
            }

            public void ShowBoard(Game g)
            {
                if (g == null)
                {
                    ShowMenu();
                    return;
                }
                if (g.Status == "open" && !g.Ai && g.GuestId == BuiltinApps.LocalId())
                {
                    g.Status = "active";
                    int actor = OtherActor(g);
                    if (actor > 0)
                        PhoneNet.SendGame(actor, "join", g.Id, BuiltinApps.LocalName(), BuiltinApps.LocalId());
                    Save();
                }
                _page = "board";
                _gameId = g.Id;
                PhoneGames.Clear(_host);
                _host.SetTitle(PhoneLang.T("app.pip.connect4", "Four Across"));
                Transform left;
                Transform center;
                Transform right;
                PhoneGames.PlayLayout(_host, out left, out center, out right);
                int win = Winner(g.Board);
                string status;
                if (win != 0)
                    status = win == 1 ? (g.HostName ?? "Host") + " wins" : (g.GuestName ?? "Guest") + " wins";
                else if (Full(g.Board))
                    status = "Draw";
                else if (g.Status == "open")
                    status = "Waiting for " + (g.GuestName ?? "friend");
                else
                    status = (g.Turn == 1 ? g.HostName : g.GuestName) + " to drop";
                var turn = PhoneUi.CreateLabel(left, "S", status, 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Wrap(turn);
                turn.overflowMode = TextOverflowModes.Ellipsis;
                PhoneUi.Size(turn.gameObject, 56f);
                PhoneUi.CreateButton(left, "Lobbies", ShowMenu, new Vector2(120f, 32f));

                var gridGo = new GameObject("Board", typeof(RectTransform));
                gridGo.transform.SetParent(center, false);
                var gle = gridGo.AddComponent<LayoutElement>();
                float cell = PhoneGames.FitCell(Cols, Rows, 4f);
                gle.flexibleHeight = 0f;
                gle.flexibleWidth = 0f;
                gle.minHeight = Rows * (cell + 4f) + 8f;
                gle.preferredHeight = gle.minHeight;
                gle.preferredWidth = Cols * (cell + 4f) + 8f;
                var grid = gridGo.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(cell, cell);
                grid.spacing = new Vector2(4f, 4f);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = Cols;
                grid.childAlignment = TextAnchor.MiddleCenter;

                bool myTurn = g.Status == "active" && !g.Ai && ((Mine(g) && g.Turn == 1) || (!Mine(g) && g.Turn == 2));
                bool cpuTurn = g.Ai && g.Status != "done" && g.Turn == 1;
                for (int r = Rows - 1; r >= 0; r--)
                {
                    for (int c = 0; c < Cols; c++)
                    {
                        int col = c;
                        int v = Cell(g.Board, c, r);
                        Color color = v == 1 ? PhoneUi.HangRed : (v == 2 ? new Color(0.95f, 0.82f, 0.18f, 1f) : PhoneUi.SurfaceAlt);
                        var disc = PhoneUi.CreateImage(gridGo.transform, "D", PhoneUi.Circle(), color);
                        disc.GetComponent<Image>().type = Image.Type.Simple;
                        if ((myTurn || cpuTurn) && v == 0 && g.Status != "done")
                        {
                            var btn = disc.gameObject.AddComponent<Button>();
                            btn.targetGraphic = disc.GetComponent<Image>();
                            btn.onClick.AddListener(() =>
                            {
                                ApplyMove(g, col, true);
                                ShowBoard(g);
                            });
                        }
                    }
                }
                if (!g.Ai)
                {
                    if (g.Status == "done")
                    {
                        PhoneUi.CreateButton(right, "Play again", () =>
                        {
                            ResetBoard(g);
                            Save();
                            int actor = OtherActor(g);
                            if (actor > 0)
                                PhoneNet.SendGame(actor, "replay", g.Id);
                            ShowBoard(g);
                        }, new Vector2(100f, 36f));
                    }
                    PhoneUi.CreateButton(right, "Leave lobby", () =>
                    {
                        int actor = OtherActor(g);
                        if (actor > 0)
                            PhoneNet.SendGame(actor, "leave", g.Id);
                        g.Status = "open";
                        Save();
                        ShowMenu();
                    }, new Vector2(100f, 36f));
                }
                else if (g.Status == "done")
                {
                    PhoneUi.CreateButton(right, "Play again", () =>
                    {
                        ResetBoard(g);
                        Save();
                        ShowBoard(g);
                    }, new Vector2(100f, 36f));
                }
            }
        }
    }
}
