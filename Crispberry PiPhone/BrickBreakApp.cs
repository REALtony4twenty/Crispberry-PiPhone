using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    internal static class BrickBreakApp
    {
        private static Session _live;

        internal static void Register()
        {
            PiPhoneApi.RegisterApp(new PiPhoneApp
            {
                Id = BuiltinApps.BreakoutId,
                DisplayName = "Brick Break",
                IconGlyph = "=",
                IconBackground = new Color(0.86f, 0.32f, 0.28f, 1f),
                SortOrder = 75,
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
            private const int BrickCols = 7;
            private const int BrickRows = 8;
            private readonly IPiPhoneHost _host;
            private string _page = "menu";
            private bool _dead;
            private int _score;
            private int _lives;
            private int _level = 1;
            private int _left;
            private RectTransform _play;
            private RectTransform _paddle;
            private RectTransform _ball;
            private Image[] _bricks;
            private int[] _hp;
            private Vector2 _vel;
            private bool _launched;
            private TextMeshProUGUI _status;
            private Transform _overParent;
            private float _paddleW = 84f;
            private Vector2 _fieldSize;

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
                _host.SetTitle(PhoneLang.T("app.pip.breakout", "Brick Break"));
                PhoneUi.MaterialChip(_host.Content, "play", "Play", StartGame, new Vector2(40f, 40f));
                var high = PhoneUi.CreateLabel(_host.Content, "High", "High score  " + PhoneTheme.HighBreakout, 16f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Size(high.gameObject, 28f);
                var hint = PhoneUi.CreateLabel(_host.Content, "Hint", "Drag the field or use A/D. Tap or Space to serve. Each wall is a new layout.", 13f, FontStyles.Normal, TextAlignmentOptions.Center);
                hint.color = PhoneUi.TextDim;
                PhoneUi.Wrap(hint);
                PhoneUi.Size(hint.gameObject, 52f);
            }

            private void StartGame()
            {
                _page = "play";
                _dead = false;
                _score = 0;
                _lives = 3;
                _level = 1;
                _launched = false;
                _vel = Vector2.zero;
                _hp = null;
                _paddleW = 84f;
                BuildPlayUi();
                _host.StartHostCoroutine(Run());
            }

            private void BuildPlayUi()
            {
                PhoneGames.Clear(_host);
                _host.SetTitle(PhoneLang.T("app.pip.breakout", "Brick Break"));
                Transform left;
                Transform center;
                Transform right;
                PhoneGames.PlayLayout(_host, out left, out center, out right);
                _overParent = left;
                _status = PhoneGames.HudBar(left, StatusText(), BuildMenu);
                if (_dead)
                    PhoneGames.OverRow(left, StartGame, BuildMenu);

                var playGo = new GameObject("Play", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                playGo.transform.SetParent(center, false);
                var playImg = playGo.GetComponent<Image>();
                playImg.sprite = PhoneUi.White();
                playImg.color = new Color(0.05f, 0.06f, 0.16f, 1f);
                _play = playGo.GetComponent<RectTransform>();
                _play.pivot = new Vector2(0.5f, 0.5f);
                PhoneUi.Stretch(_play, 0f, 0f);
                PhoneUi.IgnoreLayout(playGo);
                playGo.AddComponent<PaddleDrag>().Session = this;

                _bricks = new Image[BrickCols * BrickRows];
                bool keep = _hp != null && _hp.Length == _bricks.Length;
                if (!keep)
                    _hp = new int[BrickCols * BrickRows];
                for (int i = 0; i < _bricks.Length; i++)
                {
                    var brick = PhoneUi.CreateImage(_play, "B", PhoneUi.Rounded(4), Color.white);
                    brick.anchorMin = brick.anchorMax = Vector2.zero;
                    brick.pivot = new Vector2(0.5f, 0.5f);
                    _bricks[i] = brick.GetComponent<Image>();
                    _bricks[i].raycastTarget = false;
                }
                if (!keep)
                    FillLevel(_level);

                _paddle = PhoneUi.CreateImage(_play, "Paddle", PhoneUi.Rounded(8), new Color(0.92f, 0.92f, 0.95f, 1f));
                _paddle.anchorMin = _paddle.anchorMax = Vector2.zero;
                _paddle.pivot = new Vector2(0.5f, 0.5f);

                _ball = PhoneUi.CreateImage(_play, "Ball", PhoneUi.Circle(), Color.white);
                _ball.anchorMin = _ball.anchorMax = Vector2.zero;
                _ball.pivot = new Vector2(0.5f, 0.5f);
                _ball.sizeDelta = new Vector2(14f, 14f);

                PhoneGames.Dpad(right, () => Nudge(-36f), () => Nudge(36f), Serve, null);
                _host.StartHostCoroutine(AfterLayout());
            }

            private IEnumerator AfterLayout()
            {
                for (int i = 0; i < 12; i++)
                {
                    yield return null;
                    PhoneGames.Flush();
                    if (_play != null && _play.rect.width > 80f && _play.rect.height > 100f)
                    {
                        LayoutField();
                        yield break;
                    }
                }
                LayoutField();
            }

            private void LayoutField()
            {
                if (_play == null || _play.rect.width < 80f || _play.rect.height < 100f)
                    return;
                float w = _play.rect.width;
                float h = _play.rect.height;
                float gap = 4f;
                float bw = (w - 16f - (BrickCols - 1) * gap) / BrickCols;
                float room = Mathf.Min(h * 0.5f, h - 96f);
                float bh = (room - (BrickRows - 1) * gap) / BrickRows;
                bh = Mathf.Clamp(bh, 8f, 22f);
                if (bw < 8f)
                    return;
                float originX = 8f + bw * 0.5f;
                float top = h - 12f - bh * 0.5f;
                for (int i = 0; i < _bricks.Length; i++)
                {
                    if (_bricks[i] == null)
                        continue;
                    int col = i % BrickCols;
                    int row = i / BrickCols;
                    _bricks[i].rectTransform.sizeDelta = new Vector2(bw, bh);
                    _bricks[i].rectTransform.anchoredPosition = new Vector2(originX + col * (bw + gap), top - row * (bh + gap));
                    PaintBrick(i);
                }
                _paddleW = Mathf.Clamp(w * 0.28f - (_level - 1) * 6f, 64f, Mathf.Min(168f, w * 0.42f));
                _paddle.sizeDelta = new Vector2(_paddleW, 16f);
                float px = _paddle.anchoredPosition.x;
                if (px < 1f)
                    px = w * 0.5f;
                float py = Mathf.Clamp(28f, 20f, h * 0.12f);
                _paddle.anchoredPosition = new Vector2(Mathf.Clamp(px, _paddleW * 0.5f + 4f, w - _paddleW * 0.5f - 4f), py);
                if (!_launched)
                    ResetBall();
                _fieldSize = new Vector2(w, h);
            }

            private string StatusText()
            {
                if (_dead)
                    return PhoneLang.T("game_over", "Game over") + "  " + _score + "    " + PhoneLang.T("best", "Best") + " " + PhoneTheme.HighBreakout;
                return "Lv " + _level + "   " + _lives + "   " + _score + "    " + PhoneLang.T("best", "Best") + " " + PhoneTheme.HighBreakout;
            }

            internal void DragTo(float screenX)
            {
                if (_play == null || _paddle == null)
                    return;
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_play, new Vector2(screenX, 0f), null, out local);
                float half = _play.rect.width * 0.5f;
                float x = local.x + half;
                float pw = _paddle.sizeDelta.x * 0.5f;
                float py = _paddle.anchoredPosition.y > 1f ? _paddle.anchoredPosition.y : 28f;
                _paddle.anchoredPosition = new Vector2(Mathf.Clamp(x, pw + 4f, _play.rect.width - pw - 4f), py);
            }

            private void Nudge(float dx)
            {
                if (_paddle == null || _play == null || _play.rect.width < 80f)
                    return;
                float pw = _paddle.sizeDelta.x * 0.5f;
                float x = Mathf.Clamp(_paddle.anchoredPosition.x + dx, pw + 4f, _play.rect.width - pw - 4f);
                float py = _paddle.anchoredPosition.y > 1f ? _paddle.anchoredPosition.y : 28f;
                _paddle.anchoredPosition = new Vector2(x, py);
            }

            private void Serve()
            {
                if (_dead || _launched)
                    return;
                if (_play == null || _play.rect.width < 80f || _play.rect.height < 100f)
                    return;
                _launched = true;
                float speed = 220f + _level * 24f;
                float side = Random.value < 0.5f ? -0.6f : 0.6f;
                _vel = new Vector2(side * speed, speed);
            }

            private void ResetBall()
            {
                _launched = false;
                _vel = Vector2.zero;
                if (_ball != null && _paddle != null)
                    _ball.anchoredPosition = new Vector2(_paddle.anchoredPosition.x, _paddle.anchoredPosition.y + 16f);
            }

            private IEnumerator Run()
            {
                while (_page == "play" && !_dead)
                {
                    if (_play != null && (Mathf.Abs(_play.rect.width - _fieldSize.x) > 4f || Mathf.Abs(_play.rect.height - _fieldSize.y) > 4f))
                    {
                        PhoneGames.Flush();
                        LayoutField();
                    }
                    float x = PhoneGames.HoldX();
                    if (Mathf.Abs(x) > 0.1f)
                        Nudge(x * 420f * Time.unscaledDeltaTime);
                    if (PhoneKeys.Down(PhoneKeys.BreakoutServe) || PhoneKeys.Down(PhoneKeys.GameUp) || PhoneGames.Down(KeyCode.UpArrow) || PhoneGames.Down(KeyCode.Space))
                        Serve();
                    if (_launched)
                        Step(Time.unscaledDeltaTime);
                    else if (_ball != null && _paddle != null)
                        _ball.anchoredPosition = new Vector2(_paddle.anchoredPosition.x, _paddle.anchoredPosition.y + 16f);
                    yield return null;
                }
            }

            private void Step(float dt)
            {
                if (_play == null || _ball == null || _paddle == null)
                    return;
                float w = _play.rect.width;
                float h = _play.rect.height;
                if (w < 8f || h < 8f)
                    return;
                Vector2 pos = _ball.anchoredPosition + _vel * dt;
                const float r = 7f;
                if (pos.x < r)
                {
                    pos.x = r;
                    _vel.x = Mathf.Abs(_vel.x);
                }
                else if (pos.x > w - r)
                {
                    pos.x = w - r;
                    _vel.x = -Mathf.Abs(_vel.x);
                }
                if (pos.y > h - r)
                {
                    pos.y = h - r;
                    _vel.y = -Mathf.Abs(_vel.y);
                }

                Rect paddle = new Rect(_paddle.anchoredPosition.x - _paddle.sizeDelta.x * 0.5f, _paddle.anchoredPosition.y - _paddle.sizeDelta.y * 0.5f, _paddle.sizeDelta.x, _paddle.sizeDelta.y);
                Rect ball = new Rect(pos.x - r, pos.y - r, r * 2f, r * 2f);
                if (ball.Overlaps(paddle) && _vel.y < 0f)
                {
                    pos.y = paddle.yMax + r;
                    float t = (pos.x - _paddle.anchoredPosition.x) / Mathf.Max(16f, _paddle.sizeDelta.x * 0.5f);
                    float speed = _vel.magnitude;
                    _vel.x = Mathf.Clamp(t, -0.85f, 0.85f) * speed;
                    _vel.y = Mathf.Abs(Mathf.Sqrt(Mathf.Max(40f, speed * speed - _vel.x * _vel.x)));
                    PhoneSounds.PlayTone(660, 0.04f);
                }

                for (int i = 0; i < _bricks.Length; i++)
                {
                    if (_hp[i] == 0 || _bricks[i] == null || !_bricks[i].enabled)
                        continue;
                    RectTransform rt = _bricks[i].rectTransform;
                    Rect br = new Rect(rt.anchoredPosition.x - rt.sizeDelta.x * 0.5f, rt.anchoredPosition.y - rt.sizeDelta.y * 0.5f, rt.sizeDelta.x, rt.sizeDelta.y);
                    if (!ball.Overlaps(br))
                        continue;
                    if (_hp[i] < 0)
                    {
                        Bounce(br, ref pos, r);
                        PhoneSounds.PlayTone(220, 0.04f);
                        break;
                    }
                    _hp[i]--;
                    if (_hp[i] <= 0)
                    {
                        _hp[i] = 0;
                        _bricks[i].enabled = false;
                        _left--;
                        _score += 10 * (BrickRows - i / BrickCols) * _level;
                    }
                    else
                        PaintBrick(i);
                    Bounce(br, ref pos, r);
                    PhoneSounds.PlayTone(990, 0.05f);
                    break;
                }

                if (pos.y < r)
                {
                    _lives--;
                    if (_lives <= 0)
                    {
                        _dead = true;
                        PhoneGames.Remember(ref PhoneTheme.HighBreakout, _score, _host);
                        if (_status != null)
                            _status.text = StatusText();
                        PhoneGames.OverRow(_overParent != null ? _overParent : _host.Content, StartGame, BuildMenu);
                        return;
                    }
                    ResetBall();
                    if (_status != null)
                        _status.text = StatusText();
                    return;
                }

                if (_left <= 0)
                    NextLevel();

                _ball.anchoredPosition = pos;
                if (_status != null && !_dead)
                    _status.text = StatusText();
            }

            private void Bounce(Rect brick, ref Vector2 pos, float r)
            {
                float cx = Mathf.Abs(pos.x - brick.center.x) / Mathf.Max(1f, brick.width);
                float cy = Mathf.Abs(pos.y - brick.center.y) / Mathf.Max(1f, brick.height);
                if (cx > cy)
                {
                    _vel.x = -_vel.x;
                    pos.x = pos.x < brick.center.x ? brick.xMin - r : brick.xMax + r;
                }
                else
                {
                    _vel.y = -_vel.y;
                    pos.y = pos.y < brick.center.y ? brick.yMin - r : brick.yMax + r;
                }
            }

            private void NextLevel()
            {
                _level++;
                _paddleW = Mathf.Max(52f, 84f - _level * 4f);
                FillLevel(_level);
                LayoutField();
                ResetBall();
                _host.ShowToast("Level " + _level);
            }

            private void FillLevel(int level)
            {
                int pattern = (level - 1) % 6;
                int extra = (level - 1) / 6;
                _left = 0;
                for (int i = 0; i < _hp.Length; i++)
                {
                    int col = i % BrickCols;
                    int row = i / BrickCols;
                    int hp = Pattern(pattern, col, row);
                    if (hp > 0)
                        hp += extra;
                    _hp[i] = hp;
                    if (hp > 0)
                        _left++;
                    PaintBrick(i);
                }
            }

            private static int Pattern(int pattern, int col, int row)
            {
                switch (pattern)
                {
                    case 1:
                        int mid = BrickCols / 2;
                        return Mathf.Abs(col - mid) <= row / 2 ? 1 : 0;
                    case 2:
                        if (col == 0 || col == BrickCols - 1)
                            return -1;
                        return (row + col) % 2 == 0 ? 1 : 2;
                    case 3:
                        return (col + row) % 2 == 0 ? 1 : 0;
                    case 4:
                        bool ring = row == 0 || row == BrickRows - 1 || col == 0 || col == BrickCols - 1;
                        bool inner = row >= 2 && row <= 5 && col >= 2 && col <= 4;
                        if (ring)
                            return 2;
                        return inner ? 1 : 0;
                    case 5:
                        if (row == 0)
                            return -1;
                        if (col == 0 || col == BrickCols - 1)
                            return 2;
                        return 1 + (row % 2);
                    default:
                        return row < 5 ? 1 : 0;
                }
            }

            private void PaintBrick(int i)
            {
                if (_bricks == null || i < 0 || i >= _bricks.Length || _bricks[i] == null)
                    return;
                int hp = _hp[i];
                _bricks[i].enabled = hp != 0;
                if (hp < 0)
                    _bricks[i].color = new Color(0.45f, 0.48f, 0.52f, 1f);
                else if (hp >= 3)
                    _bricks[i].color = new Color(0.72f, 0.32f, 0.82f, 1f);
                else if (hp == 2)
                    _bricks[i].color = new Color(0.95f, 0.55f, 0.18f, 1f);
                else
                    _bricks[i].color = BrickColor(i / BrickCols);
            }

            private static Color BrickColor(int row)
            {
                switch (row % 5)
                {
                    case 0: return new Color(0.86f, 0.24f, 0.24f, 1f);
                    case 1: return new Color(0.95f, 0.55f, 0.18f, 1f);
                    case 2: return new Color(0.95f, 0.82f, 0.22f, 1f);
                    case 3: return new Color(0.28f, 0.72f, 0.38f, 1f);
                    default: return new Color(0.22f, 0.55f, 0.92f, 1f);
                }
            }

            private sealed class PaddleDrag : MonoBehaviour, IDragHandler, IPointerClickHandler
            {
                internal Session Session;

                public void OnDrag(PointerEventData eventData)
                {
                    if (Session != null && eventData != null)
                        Session.DragTo(eventData.position.x);
                }

                public void OnPointerClick(PointerEventData eventData)
                {
                    if (Session != null)
                        Session.Serve();
                }
            }
        }
    }
}
