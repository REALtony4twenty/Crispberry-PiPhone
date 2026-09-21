using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    internal static class SolitaireApp
    {
        private static Session _live;

        internal static void Register()
        {
            PiPhoneApi.RegisterApp(new PiPhoneApp
            {
                Id = BuiltinApps.SolitaireId,
                DisplayName = "Solitaire",
                IconGlyph = "A",
                IconBackground = new Color(0.16f, 0.42f, 0.28f, 1f),
                SortOrder = 77,
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
            private static readonly string[] ArtSuits = { "spades", "hearts", "diamonds", "clubs" };
            private static readonly string[] ArtRanks = { "A", "02", "03", "04", "05", "06", "07", "08", "09", "10", "J", "Q", "K" };

            private readonly IPiPhoneHost _host;
            private string _page = "menu";
            private readonly List<int> _stock = new List<int>();
            private readonly List<int> _waste = new List<int>();
            private readonly List<int>[] _tab = new List<int>[7];
            private readonly List<int>[] _found = new List<int>[4];
            private readonly bool[] _up = new bool[52];
            private int _draw = 1;
            private int _selPile = -1;
            private int _selFrom;
            private bool _won;
            private bool _auto;
            private int _dealId;
            private TextMeshProUGUI _status;

            public Session(IPiPhoneHost host)
            {
                _host = host;
                for (int i = 0; i < 7; i++)
                    _tab[i] = new List<int>();
                for (int i = 0; i < 4; i++)
                    _found[i] = new List<int>();
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
                _host.SetTitle(PhoneLang.T("app.pip.solitaire", "Solitaire"));
                PhoneUi.CreateButton(_host.Content, "Turn 1", () => Start(1), new Vector2(220f, 48f));
                PhoneUi.CreateButton(_host.Content, "Turn 3", () => Start(3), new Vector2(220f, 44f));
                var high = PhoneUi.CreateLabel(_host.Content, "High", "Wins  " + PhoneTheme.HighSolitaire, 16f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Size(high.gameObject, 28f);
                var hint = PhoneUi.CreateLabel(_host.Content, "Hint", "Turn 1 flips one card. Turn 3 flips three, and only the top one can be played. When the deck is empty and every card is face up, the stacks finish themselves. You can also tap the empty deck.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 52f);
            }

            private void Start(int draw)
            {
                _draw = draw < 1 ? 1 : draw;
                _won = false;
                _auto = false;
                _dealId++;
                _selPile = -1;
                Deal();
                _page = "play";
                BuildPlayUi();
            }

            private void Deal()
            {
                _stock.Clear();
                _waste.Clear();
                for (int i = 0; i < 7; i++)
                    _tab[i].Clear();
                for (int i = 0; i < 4; i++)
                    _found[i].Clear();
                for (int i = 0; i < 52; i++)
                    _up[i] = false;
                var deck = new List<int>(52);
                for (int i = 0; i < 52; i++)
                    deck.Add(i);
                for (int i = 51; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    int t = deck[i];
                    deck[i] = deck[j];
                    deck[j] = t;
                }
                int n = 0;
                for (int col = 0; col < 7; col++)
                {
                    for (int k = 0; k <= col; k++)
                    {
                        _tab[col].Add(deck[n]);
                        if (k == col)
                            _up[deck[n]] = true;
                        n++;
                    }
                }
                for (; n < 52; n++)
                    _stock.Add(deck[n]);
            }

            private void BuildPlayUi()
            {
                PhoneGames.Clear(_host);
                _host.SetTitle(PhoneLang.T("app.pip.solitaire", "Solitaire"));
                Transform left;
                Transform center;
                Transform right;
                PhoneGames.PlayLayout(_host, out left, out center, out right);
                _status = PhoneGames.HudBar(left, StatusText(), BuildMenu);
                if (_won)
                    PhoneGames.OverRow(left, () => Start(_draw), BuildMenu);

                var table = new GameObject("Table", typeof(RectTransform));
                table.transform.SetParent(center, false);
                var tle = table.AddComponent<LayoutElement>();
                tle.flexibleHeight = 1f;
                tle.minHeight = 160f;
                PhoneUi.AddVertical(table, 8f, new RectOffset(0, 0, 4, 4));

                var top = new GameObject("Top", typeof(RectTransform));
                top.transform.SetParent(table.transform, false);
                PhoneUi.Size(top, 54f);
                PhoneUi.AddHorizontal(top, 6f);
                var th = top.GetComponent<HorizontalLayoutGroup>();
                th.childForceExpandWidth = false;
                th.childAlignment = TextAnchor.MiddleLeft;
                CardFace(top.transform, _stock.Count > 0 ? -2 : -3, 48f, DrawStock, false);
                CardFace(top.transform, _waste.Count > 0 ? _waste[_waste.Count - 1] : -1, 48f, () => TapWaste(), _waste.Count > 0 && _selPile == 7);
                var spacer = new GameObject("Sp", typeof(RectTransform));
                spacer.transform.SetParent(top.transform, false);
                spacer.AddComponent<LayoutElement>().flexibleWidth = 1f;
                for (int f = 0; f < 4; f++)
                {
                    int fi = f;
                    int show = _found[f].Count > 0 ? _found[f][_found[f].Count - 1] : -1;
                    CardFace(top.transform, show, 48f, () => TapFound(fi), false);
                }

                var tabs = new GameObject("Tabs", typeof(RectTransform));
                tabs.transform.SetParent(table.transform, false);
                var tabsLe = tabs.AddComponent<LayoutElement>();
                tabsLe.flexibleHeight = 1f;
                PhoneUi.AddHorizontal(tabs, 4f);
                var hh = tabs.GetComponent<HorizontalLayoutGroup>();
                hh.childForceExpandWidth = true;
                hh.childForceExpandHeight = true;
                hh.childAlignment = TextAnchor.UpperCenter;
                for (int c = 0; c < 7; c++)
                {
                    int col = c;
                    var pile = new GameObject("P" + c, typeof(RectTransform));
                    pile.transform.SetParent(tabs.transform, false);
                    PhoneUi.AddVertical(pile, 2f, new RectOffset(0, 0, 0, 0));
                    var v = pile.GetComponent<VerticalLayoutGroup>();
                    v.childAlignment = TextAnchor.UpperCenter;
                    v.childForceExpandHeight = false;
                    v.childForceExpandWidth = false;
                    if (_tab[c].Count == 0)
                    {
                        CardFace(pile.transform, -1, 48f, () => TapTab(col, 0), false);
                        continue;
                    }
                    for (int k = 0; k < _tab[c].Count; k++)
                    {
                        int idx = k;
                        int card = _tab[c][k];
                        bool face = k == _tab[c].Count - 1;
                        bool up = _up[card];
                        float h = face ? 48f : (up ? 16f : 11f);
                        CardFace(pile.transform, up ? card : -2, h, () => TapTab(col, idx), up && IsSelected(card));
                    }
                }

                PhoneUi.CreateButton(right, "New", () => Start(_draw), new Vector2(88f, 36f));
            }

            private string StatusText()
            {
                if (_won)
                    return "You win!    " + PhoneTheme.HighSolitaire;
                return (_draw == 3 ? "Turn 3" : "Turn 1") + "    " + PhoneTheme.HighSolitaire + " wins";
            }

            private static Sprite CardArt(int card)
            {
                if (card == -2)
                    return PhoneIcons.Card("card_back");
                if (card < 0)
                    return PhoneIcons.Card("card_empty");
                int suit = card / 13;
                int rank = card % 13;
                if (suit < 0 || suit > 3 || rank < 0 || rank > 12)
                    return PhoneIcons.Card("card_empty");
                return PhoneIcons.Card("card_" + ArtSuits[suit] + "_" + ArtRanks[rank]);
            }

            private void CardFace(Transform parent, int card, float height, UnityEngine.Events.UnityAction click, bool selected)
            {
                const float width = 46f;
                var rt = PhoneUi.CreateImage(parent, "Card", PhoneUi.White(), selected ? PhoneUi.Accent : new Color(0f, 0f, 0f, 0f));
                var le = rt.gameObject.AddComponent<LayoutElement>();
                le.minWidth = 36f;
                le.preferredWidth = width;
                le.flexibleWidth = 0f;
                le.minHeight = height;
                le.preferredHeight = height;
                le.flexibleHeight = 0f;
                var img = rt.GetComponent<Image>();
                img.type = Image.Type.Simple;
                var button = rt.gameObject.AddComponent<Button>();
                button.targetGraphic = img;
                var colors = button.colors;
                colors.fadeDuration = 0.04f;
                colors.colorMultiplier = 1f;
                button.colors = colors;
                if (click != null)
                    button.onClick.AddListener(click);
                rt.gameObject.AddComponent<RectMask2D>();

                float inset = selected ? 3f : 1f;
                var artRt = PhoneUi.CreateImage(rt, "Art", CardArt(card), Color.white);
                artRt.anchorMin = new Vector2(0f, 1f);
                artRt.anchorMax = new Vector2(1f, 1f);
                artRt.pivot = new Vector2(0.5f, 1f);
                artRt.sizeDelta = new Vector2(-inset * 2f, width - inset * 2f);
                artRt.anchoredPosition = new Vector2(0f, -inset);
                var artImg = artRt.GetComponent<Image>();
                artImg.type = Image.Type.Simple;
                artImg.preserveAspect = true;
                artImg.raycastTarget = false;
            }

            private void CardBtn(Transform parent, int card, UnityEngine.Events.UnityAction click, bool face)
            {
                CardFace(parent, face || card < 0 ? card : -2, 48f, click, false);
            }

            private bool IsSelected(int card)
            {
                if (_selPile == 7)
                    return _waste.Count > 0 && _waste[_waste.Count - 1] == card;
                if (_selPile >= 0 && _selPile < 7)
                {
                    var pile = _tab[_selPile];
                    for (int i = _selFrom; i < pile.Count; i++)
                    {
                        if (pile[i] == card)
                            return true;
                    }
                }
                return false;
            }

            private void DrawStock()
            {
                if (_won || _auto)
                    return;
                if (_stock.Count == 0)
                {
                    if (CanAutoComplete())
                    {
                        _host.StartHostCoroutine(AutoComplete());
                        return;
                    }
                    for (int i = _waste.Count - 1; i >= 0; i--)
                    {
                        _stock.Add(_waste[i]);
                        _up[_waste[i]] = false;
                    }
                    _waste.Clear();
                    _selPile = -1;
                    BuildPlayUi();
                    return;
                }
                int n = Mathf.Min(_draw, _stock.Count);
                for (int i = 0; i < n; i++)
                {
                    int c = _stock[_stock.Count - 1];
                    _stock.RemoveAt(_stock.Count - 1);
                    _up[c] = true;
                    _waste.Add(c);
                }
                _selPile = -1;
                BuildPlayUi();
                MaybeAuto();
            }

            private void TapWaste()
            {
                if (_won || _waste.Count == 0)
                    return;
                if (_selPile == 7)
                    _selPile = -1;
                else
                {
                    _selPile = 7;
                    _selFrom = 0;
                    if (!TryAuto())
                    {
                        BuildPlayUi();
                        return;
                    }
                }
                BuildPlayUi();
            }

            private void TapFound(int f)
            {
                if (_won)
                    return;
                if (_selPile < 0)
                    return;
                if (TryMoveToFound(f))
                {
                    _selPile = -1;
                    AfterMove();
                    return;
                }
                _selPile = -1;
                BuildPlayUi();
            }

            private void TapTab(int col, int idx)
            {
                if (_won)
                    return;
                var pile = _tab[col];
                if (_selPile >= 0 && !(_selPile == col))
                {
                    if (TryMoveToTab(col))
                    {
                        _selPile = -1;
                        AfterMove();
                        return;
                    }
                }
                if (idx < pile.Count)
                {
                    int card = pile[idx];
                    if (!_up[card])
                    {
                        if (idx == pile.Count - 1)
                        {
                            _up[card] = true;
                            _selPile = -1;
                            BuildPlayUi();
                        }
                        return;
                    }
                    if (_selPile == col && _selFrom == idx)
                    {
                        _selPile = -1;
                        BuildPlayUi();
                        return;
                    }
                    _selPile = col;
                    _selFrom = idx;
                    if (!TryAuto())
                        BuildPlayUi();
                    return;
                }
                if (_selPile >= 0)
                {
                    if (TryMoveToTab(col))
                    {
                        _selPile = -1;
                        AfterMove();
                    }
                }
            }

            private bool CanAutoComplete()
            {
                if (_stock.Count > 0 || _won)
                    return false;
                int placed = 0;
                for (int f = 0; f < 4; f++)
                    placed += _found[f].Count;
                if (placed >= 52)
                    return false;
                for (int c = 0; c < 7; c++)
                {
                    for (int i = 0; i < _tab[c].Count; i++)
                    {
                        if (!_up[_tab[c][i]])
                            return false;
                    }
                }
                return true;
            }

            private IEnumerator AutoComplete()
            {
                int deal = _dealId;
                _auto = true;
                _selPile = -1;
                _host.ShowToast("Finishing the stacks.");
                int guard = 0;
                while (_page == "play" && !_won && deal == _dealId && guard < 160)
                {
                    guard++;
                    if (!MoveOneToFoundation())
                        break;
                    yield return new WaitForSecondsRealtime(0.08f);
                }
                if (deal == _dealId)
                    _auto = false;
            }

            private bool MoveOneToFoundation()
            {
                int bestRank = 99;
                int bestPile = -1;
                int bestFound = -1;
                if (_waste.Count > 0)
                    ConsiderFound(_waste[_waste.Count - 1], 7, ref bestRank, ref bestPile, ref bestFound);
                for (int c = 0; c < 7; c++)
                {
                    if (_tab[c].Count == 0)
                        continue;
                    ConsiderFound(_tab[c][_tab[c].Count - 1], c, ref bestRank, ref bestPile, ref bestFound);
                }
                if (bestPile >= 0)
                {
                    _selPile = bestPile;
                    _selFrom = bestPile == 7 ? 0 : _tab[bestPile].Count - 1;
                    if (TryMoveToFound(bestFound))
                    {
                        _selPile = -1;
                        AfterMove();
                        return true;
                    }
                    _selPile = -1;
                }
                return TryUncoverNext();
            }

            private void ConsiderFound(int card, int pile, ref int bestRank, ref int bestPile, ref int bestFound)
            {
                int f = card / 13;
                int rank = card % 13;
                if (!CanPlaceFound(card, f) || rank >= bestRank)
                    return;
                bestRank = rank;
                bestPile = pile;
                bestFound = f;
            }

            private static bool CanPlaceFoundRank(int have, int rank)
            {
                if (have == 0)
                    return rank == 0;
                return have == rank;
            }

            private bool CanPlaceFound(int card, int f)
            {
                if (card / 13 != f || f < 0 || f > 3)
                    return false;
                return CanPlaceFoundRank(_found[f].Count, card % 13);
            }

            private bool TryUncoverNext()
            {
                var order = new int[4];
                int n = 0;
                for (int f = 0; f < 4; f++)
                {
                    if (_found[f].Count <= 12)
                        order[n++] = f * 13 + _found[f].Count;
                }
                for (int a = 0; a < n; a++)
                {
                    for (int b = a + 1; b < n; b++)
                    {
                        if (order[b] % 13 < order[a] % 13)
                        {
                            int t = order[a];
                            order[a] = order[b];
                            order[b] = t;
                        }
                    }
                }
                for (int i = 0; i < n; i++)
                {
                    if (TryUncoverCard(order[i]))
                        return true;
                }
                return false;
            }

            private bool TryUncoverCard(int card)
            {
                int col;
                int idx;
                if (!FindInTableau(card, out col, out idx))
                    return false;
                if (idx >= _tab[col].Count - 1)
                    return false;
                _selPile = col;
                _selFrom = idx + 1;
                for (int dest = 0; dest < 7; dest++)
                {
                    if (dest == col)
                        continue;
                    if (TryMoveToTab(dest))
                    {
                        _selPile = -1;
                        AfterMove();
                        return true;
                    }
                }
                _selPile = -1;
                return false;
            }

            private bool FindInTableau(int card, out int col, out int idx)
            {
                for (int c = 0; c < 7; c++)
                {
                    for (int i = 0; i < _tab[c].Count; i++)
                    {
                        if (_tab[c][i] == card)
                        {
                            col = c;
                            idx = i;
                            return true;
                        }
                    }
                }
                col = -1;
                idx = -1;
                return false;
            }

            private bool TryAuto()
            {
                for (int f = 0; f < 4; f++)
                {
                    if (TryMoveToFound(f))
                    {
                        _selPile = -1;
                        AfterMove();
                        return true;
                    }
                }
                return false;
            }

            private List<int> SelectedRun()
            {
                var run = new List<int>();
                if (_selPile == 7)
                {
                    if (_waste.Count > 0)
                        run.Add(_waste[_waste.Count - 1]);
                    return run;
                }
                if (_selPile < 0 || _selPile > 6)
                    return run;
                var pile = _tab[_selPile];
                for (int i = _selFrom; i < pile.Count; i++)
                    run.Add(pile[i]);
                return run;
            }

            private bool TryMoveToTab(int col)
            {
                var run = SelectedRun();
                if (run.Count == 0)
                    return false;
                int card = run[0];
                var dest = _tab[col];
                if (dest.Count == 0)
                {
                    if (card % 13 != 12)
                        return false;
                }
                else
                {
                    int top = dest[dest.Count - 1];
                    if (!CanStack(card, top))
                        return false;
                }
                PullSelected();
                dest.AddRange(run);
                return true;
            }

            private bool TryMoveToFound(int f)
            {
                var run = SelectedRun();
                if (run.Count != 1)
                    return false;
                int card = run[0];
                if (card / 13 != f)
                    return false;
                int rank = card % 13;
                if (_found[f].Count == 0)
                {
                    if (rank != 0)
                        return false;
                }
                else
                {
                    int top = _found[f][_found[f].Count - 1];
                    if (top % 13 != rank - 1)
                        return false;
                }
                PullSelected();
                _found[f].Add(card);
                return true;
            }

            private static bool CanStack(int child, int parent)
            {
                int cr = child % 13;
                int pr = parent % 13;
                if (cr != pr - 1)
                    return false;
                bool cred = (child / 13) == 1 || (child / 13) == 2;
                bool pred = (parent / 13) == 1 || (parent / 13) == 2;
                return cred != pred;
            }

            private void PullSelected()
            {
                if (_selPile == 7)
                {
                    if (_waste.Count > 0)
                        _waste.RemoveAt(_waste.Count - 1);
                    return;
                }
                if (_selPile < 0 || _selPile > 6)
                    return;
                var pile = _tab[_selPile];
                int keep = _selFrom;
                while (pile.Count > keep)
                    pile.RemoveAt(pile.Count - 1);
            }

            private void AfterMove()
            {
                for (int c = 0; c < 7; c++)
                {
                    if (_tab[c].Count == 0)
                        continue;
                    int last = _tab[c][_tab[c].Count - 1];
                    if (!_up[last])
                        _up[last] = true;
                }
                int n = 0;
                for (int f = 0; f < 4; f++)
                    n += _found[f].Count;
                if (n >= 52)
                {
                    _won = true;
                    PhoneGames.Remember(ref PhoneTheme.HighSolitaire, PhoneTheme.HighSolitaire + 1, _host);
                    _host.ShowToast("You win!");
                }
                BuildPlayUi();
                MaybeAuto();
            }

            private void MaybeAuto()
            {
                if (_auto || _won || _page != "play" || _host == null)
                    return;
                if (!CanAutoComplete())
                    return;
                _host.StartHostCoroutine(AutoComplete());
            }
        }
    }
}
