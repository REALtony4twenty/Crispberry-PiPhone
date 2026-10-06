using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    internal static class EchoApp
    {
        private static Session _live;

        internal static void Register()
        {
            PiPhoneApi.RegisterApp(new PiPhoneApp
            {
                Id = BuiltinApps.SimonId,
                DisplayName = "Echo",
                IconGlyph = "4",
                IconBackground = new Color(0.18f, 0.62f, 0.48f, 1f),
                SortOrder = 73,
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
            private static readonly Color[] Colors =
            {
                new Color(0.18f, 0.72f, 0.38f, 1f),
                new Color(0.86f, 0.24f, 0.24f, 1f),
                new Color(0.95f, 0.78f, 0.20f, 1f),
                new Color(0.22f, 0.48f, 0.92f, 1f)
            };
            private static readonly int[] Tones = { 523, 659, 784, 392 };

            private readonly IPiPhoneHost _host;
            private string _page = "menu";
            private readonly List<int> _seq = new List<int>();
            private int _step;
            private bool _listen;
            private bool _dead;
            private Image[] _pads;
            private TextMeshProUGUI _status;
            private int _round;

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
                _round++;
                _listen = false;
                _page = "off";
            }

            internal string ExportCast()
            {
                if (_page != "play")
                    return "menu";
                int n = _seq != null ? _seq.Count : 0;
                return "play|" + n + "|" + (_dead ? "1" : "0");
            }

            public void Relayout()
            {
                if (_page == "play")
                    BuildPlayUi();
            }

            internal void StartGame()
            {
                _round++;
                _page = "play";
                _seq.Clear();
                _step = 0;
                _listen = false;
                _dead = false;
                BuildPlayUi();
                _host.StartHostCoroutine(NextRound());
            }

            private void BuildPlayUi()
            {
                PhoneGames.Clear(_host);
                _host.SetTitle(PhoneLang.T("app.pip.simon", "Echo"));
                Transform left;
                Transform center;
                Transform right;
                PhoneGames.PlayLayout(_host, out left, out center, out right);
                _status = PhoneGames.HudBar(left, StatusText(), () => _host.GoBack(), StartGame, "New");
                PhoneGames.HidePane(right);

                var grid = new GameObject("Pads", typeof(RectTransform));
                grid.transform.SetParent(center, false);
                var gle = grid.AddComponent<LayoutElement>();
                bool land = _host.IsLandscape;
                float pad = land ? 156f : 168f;
                gle.flexibleHeight = 0f;
                gle.flexibleWidth = 0f;
                gle.minHeight = pad * 2f + 28f;
                gle.preferredHeight = gle.minHeight;
                gle.preferredWidth = pad * 2f + 28f;
                var layout = grid.AddComponent<GridLayoutGroup>();
                layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                layout.constraintCount = 2;
                layout.cellSize = new Vector2(pad, pad);
                layout.spacing = new Vector2(12f, 12f);
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.padding = new RectOffset(8, 8, 8, 8);

                _pads = new Image[4];
                for (int i = 0; i < 4; i++)
                {
                    int idx = i;
                    var rt = PhoneUi.CreateImage(grid.transform, "P" + i, PhoneUi.Rounded(24), Colors[i]);
                    _pads[i] = rt.GetComponent<Image>();
                    _pads[i].raycastTarget = true;
                    var btn = rt.gameObject.AddComponent<Button>();
                    btn.transition = Selectable.Transition.None;
                    btn.onClick.AddListener(() => Tap(idx));
                }
            }

            private string StatusText()
            {
                int score = Mathf.Max(0, _seq.Count - (_dead ? 1 : 0));
                if (_dead)
                    return "Wrong    " + score + "    Best " + PhoneTheme.HighSimon;
                bool reverse = _seq.Count >= 6;
                return (reverse ? "Reverse  " : "Lv ") + _seq.Count + "    Best " + PhoneTheme.HighSimon;
            }

            private IEnumerator NextRound()
            {
                int round = _round;
                _listen = false;
                _seq.Add(Random.Range(0, 4));
                _step = 0;
                if (_status != null)
                    _status.text = StatusText();
                yield return new WaitForSecondsRealtime(0.45f);
                float wait = Mathf.Max(0.22f, 0.55f - _seq.Count * 0.02f);
                for (int i = 0; i < _seq.Count && round == _round && _page == "play" && !_dead; i++)
                {
                    yield return Flash(_seq[i], wait);
                    yield return new WaitForSecondsRealtime(0.12f);
                }
                if (round != _round)
                    yield break;
                _listen = true;
                if (_status != null)
                    _status.text = StatusText();
            }

            private IEnumerator Flash(int i, float wait)
            {
                if (_pads == null || i < 0 || i >= _pads.Length || _pads[i] == null)
                    yield break;
                PhoneSounds.PlayTone(Tones[i], Mathf.Max(0.08f, wait * 0.7f), "echo-pad");
                _pads[i].color = Color.Lerp(Colors[i], Color.white, 0.55f);
                yield return new WaitForSecondsRealtime(wait);
                if (_pads[i] != null)
                    _pads[i].color = Colors[i];
            }

            private void Tap(int i)
            {
                if (!_listen || _dead || _page != "play")
                    return;
                _host.StartHostCoroutine(Flash(i, 0.18f));
                int expect = _seq.Count >= 6 ? _seq[_seq.Count - 1 - _step] : _seq[_step];
                if (i != expect)
                {
                    _dead = true;
                    _listen = false;
                    _host.StartHostCoroutine(Song(false));
                    int score = Mathf.Max(0, _seq.Count - 1);
                    PhoneGames.Remember(ref PhoneTheme.HighSimon, score, _host);
                    if (_status != null)
                        _status.text = StatusText();
                    return;
                }
                _step++;
                if (_step < _seq.Count)
                    return;
                _listen = false;
                _host.StartHostCoroutine(AfterOk());
            }

            private IEnumerator AfterOk()
            {
                int round = _round;
                if (_status != null)
                    _status.text = "Nice    " + _seq.Count;
                yield return Song(true);
                yield return new WaitForSecondsRealtime(0.15f);
                if (round == _round && _page == "play" && !_dead)
                    yield return NextRound();
            }

            private IEnumerator Song(bool win)
            {
                int[] notes = win ? new[] { 392, 523, 659, 784 } : new[] { 784, 659, 523, 392 };
                for (int i = 0; i < notes.Length; i++)
                {
                    if (_page != "play")
                        yield break;
                    PhoneSounds.PlayTone(notes[i], 0.12f, win ? "echo-win" : "echo-lose");
                    yield return new WaitForSecondsRealtime(0.13f);
                }
            }
        }
    }
}
