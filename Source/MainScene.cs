using JuiceboxEngine;
using JuiceboxEngine.Audio;
using JuiceboxEngine.Coroutines;
using JuiceboxEngine.Graphics;
using JuiceboxEngine.GUI;
using JuiceboxEngine.Input;
using JuiceboxEngine.Math;
using JuiceboxEngine.Particles;
using JuiceboxEngine.Playfab;
using JuiceboxEngine.Resources;
using JuiceboxEngine.Util;
using System.Collections;

namespace LD48
{
    class MainScene : Scene
    {
        private const int BPM = 132;
        private const float GAME_TIME = 160;
        private const float KICK_IN_BPM_EFFECT = 14.5f;
        private const int MAX_WEBSITES = 6; // Set this to the amount of website sprites. (Website{x}.png)

        private GameObject _background;
        private GameObject _website;
        private GameObject _debtCounter;
        private GameObject _debtPerSecondCounter;
        private GameObject _popup;
        private GameObject _timer;
        private GameObject _exit;

        private Sprite _websiteSprite;
        private TextComponent _debtText;
        private TextComponent _debtTextShadow;
        private TextComponent _debtPerSecondText;
        private TextComponent _debtPerSecondTextShadow;
        private TextComponent _timerText;
        private TextComponent _helpText;

        private BurstParticleComponent _websiteParticles;

        private GameObject[] _fixedCharges;

        public ulong debt;
        public ulong debtPerSecond;
        private ulong _shownDebt;
        
        private float _debtTimer;
        private float _timeLeft;

        private Text _fps;
        private float _defaultZoom;
        private bool _popupEnabledThisFrame;
        private bool _finished;
        private float _lowestScroll;

        // TODO: balance/proper names/descriptions
        public FixedCharge[] FixedCharges = new FixedCharge[]
        {
            new FixedCharge() { id = 0, name = "Food", price = 10, debtPerSecond = 1, desc = "I'm getting hungry.", unlockPrice = 10 },
            new FixedCharge() { id = 1, name = "Phone", price = 97, debtPerSecond = 11, desc = "Call me maybe.", unlockPrice = 20 },
            new FixedCharge() { id = 2, name = "Utilities", price = 890, debtPerSecond = 150, desc = "Unlimited power!", unlockPrice = 97 },
            new FixedCharge() { id = 3, name = "Car loan", price = 13059, debtPerSecond = 2000, desc = "Money goes vrooom.", unlockPrice = 890 },
            new FixedCharge() { id = 4, name = "Mortgage", price = 147923, debtPerSecond = 12000, desc = "Sign here, here, here!", unlockPrice = 13059 },
        };

        private ulong[] _fixedChargesGenerated;

        public MainScene(ResourceManager manager) 
            : base(manager)
        {
            debt = 0;
            debtPerSecond = 0;
            _timeLeft = GAME_TIME;
            _finished = false;

            GraphicsManager.Instance.OnResize += (x, y) =>
            {
                ScaleGame();
            };
        }

        private void ScaleGame()
        {
            if (Browser.IsMobile())
            {
                // No zoom on mobile.
                _defaultZoom = 1;
                return;
            }

            _defaultZoom = GraphicsManager.Instance.Height < 1000 ? 1 : 2;
            DefaultCamera.Zoom = _defaultZoom;
        }

        protected override void InitializeScene()
        {
            GraphicsManager.Instance.Context.UseDepth(true);

            _fps = new Text(GUI.Root);
            _fps.Dimensions = new Vector2(10000, 16);

            DefaultCamera.ClearColor = new Color(63, 136, 197, 255);
            DefaultCamera.Parent.Transform.Position2D = new Vector2(0, -46);

            ScaleGame();

            _background = AddGameObject("Background");
            TileMap map = _background.AddComponent<TileMap>();
            map.TileSize = 16;
            map.MapData = ResourceManager.Load<Texture2D>("Textures/backgroundData.png");
            map.MapData.Wrap = Texture2D.WrapMode.Repeat;
            map.Sprites = ResourceManager.Load<Texture2D>("Textures/background.png");

            AudioComponent music = _background.AddComponent<AudioComponent>();
            music.SetAudioClip(ResourceManager.Load<AudioClip>("Sounds/main.mp3"));
            music.Play();

            _website = AddGameObject("website");
            _websiteSprite = _website.AddComponent<Sprite>();
            _websiteSprite.Texture = ResourceManager.Load<Texture2D>("Textures/Website0.png");
            _websiteSprite.Offset = new Vector2(-_websiteSprite.Texture.Width / 2, -_websiteSprite.Texture.Height / 2);
            _websiteSprite.Priority = 0.1f;

            EmptyUIElement websiteHit = new EmptyUIElement(GUI.Root);
            websiteHit.Dimensions = new Vector2(200, 200) * DefaultCamera.Zoom;
            websiteHit.Pivot = UIDefaults.Centered;

            websiteHit.OnMouseUp += WebsiteClick;
            websiteHit.OnMouseEnter += WebsiteEnter;
            websiteHit.OnMouseExit += WebsiteExit;

            AudioComponent webAudio = _website.AddComponent<AudioComponent>();
            webAudio.SetAudioClip(ResourceManager.Load<AudioClip>("Sounds/buy.mp3"));

            UIComponent websiteUI = _website.AddComponent<UIComponent>();
            websiteUI.Setup(websiteHit, this);

            GameObject buyParticles = AddGameObject("Particles");
            _websiteParticles = buyParticles.AddComponent<BurstParticleComponent>();
            _websiteParticles.Texture = ResourceManager.Load<Texture2D>("Textures/Dollar.png");
            _websiteParticles.Gravity = new Vector2(0, 5);
            _websiteParticles.BurstAmount = 5;

            _websiteParticles.OnRequestParticle += () =>
            {
                Particle particle = new Particle();
                particle.particleFrames = 1;
                particle.sourceRectangles = new Rectangle[] { new Rectangle(0, 0, 8, 8) };

                particle.velocity = new Vector2(Random.NextRange(-1.0f, 1.0f), Random.NextRange(-0.5f, -1.0f));
                particle.color = Color.White;
                particle.lifeTime = Random.NextRange(0.25f, 1.0f);
                particle.totalLifeTime = particle.lifeTime;
                particle.size = new Vector2(1, 1);

                return particle;
            };

            _websiteParticles.OnParticleUpdate += (particle) =>
            {
                particle.size = new Vector2(2, 2) * (particle.lifeTime / particle.totalLifeTime);
            };

            _debtCounter = AddGameObject("Debt counter");
            _debtCounter.Transform.Position2D = new Vector2(0, 54);

            _debtText = _debtCounter.AddComponent<TextComponent>();
            _debtText.Font = ResourceManager.Load<Font>("Fonts/8-bit.bff");
            _debtText.DisplayText = GetDisplayString(debt);
            _debtText.Alignment = TextAlignment.Center;

            _debtTextShadow = _debtCounter.AddComponent<TextComponent>();
            _debtTextShadow.Font = ResourceManager.Load<Font>("Fonts/8-bit.bff");
            _debtTextShadow.Color = Color.Black;
            _debtTextShadow.Alignment = TextAlignment.Center;
            _debtTextShadow.DisplayText = _debtText.DisplayText;
            _debtTextShadow.Offset = new Vector2(2, -2);

            _debtPerSecondCounter = AddGameObject("DPS counter");
            _debtPerSecondCounter.Transform.Position2D = new Vector2(0, 47);
            _debtPerSecondText = _debtPerSecondCounter.AddComponent<TextComponent>();
            _debtPerSecondText.DisplayText = GetDisplayString(debt);
            _debtPerSecondText.Alignment = TextAlignment.Center;

            _debtPerSecondTextShadow = _debtPerSecondCounter.AddComponent<TextComponent>();
            _debtPerSecondTextShadow.Offset = new Vector2(1, -1);
            _debtPerSecondTextShadow.Color = Color.Black;
            _debtPerSecondTextShadow.DisplayText = _debtPerSecondText.DisplayText;
            _debtPerSecondTextShadow.Alignment = TextAlignment.Center;

            _fixedCharges = new GameObject[FixedCharges.Length];
            _fixedChargesGenerated = new ulong[FixedCharges.Length];

            // Setup fixed charges objects.
            for(int i = 0; i < FixedCharges.Length; ++i)
            {
                FixedCharge fixedCharge = FixedCharges[i];

                GameObject charge = AddGameObject($"Fixed charge {i}");
                charge.Enabled = false;

                _fixedCharges[i] = charge;

                charge.Transform.Position = new Vector3(0, -80 - i * 20, 0.25f);

                Sprite chargeSprite = charge.AddComponent<Sprite>();
                chargeSprite.Texture = ResourceManager.Load<Texture2D>("Textures/Charge.png");
                chargeSprite.Offset = new Vector2(-chargeSprite.Texture.Width / 2, -chargeSprite.Texture.Height / 2);
                chargeSprite.Priority = 0.5f;

                EmptyUIElement chargeHit = new EmptyUIElement(GUI.Root);
                chargeHit.Dimensions = new Vector2(chargeSprite.Texture.Width * 2 * DefaultCamera.Zoom, chargeSprite.Texture.Height * DefaultCamera.Zoom);
                chargeHit.Pivot = UIDefaults.Centered;

                TextComponent chargeText = charge.AddComponent<TextComponent>();
                chargeText.Offset = new Vector2(-60, -9);
                chargeText.Color = Color.Black;
                chargeText.DisplayText = $"???? {GetDisplayStringSmall(fixedCharge.price)}";

                TextComponent chargeDebtText = charge.AddComponent<TextComponent>();
                chargeDebtText.Offset = new Vector2(22, -9);
                chargeDebtText.Color = Color.Black;
                chargeDebtText.DisplayText = $"${fixedCharge.debtPerSecond.KiloFormat()}/s";

                UIComponent chargeUI = charge.AddComponent<UIComponent>();
                chargeUI.Setup(chargeHit, this);

                AudioComponent audio = charge.AddComponent<AudioComponent>();
                audio.SetAudioClip(ResourceManager.Load<AudioClip>("Sounds/ka-ching.mp3"));

                // Setup UI events.
                chargeHit.OnMouseEnter += (ev) =>
                {
                    _popup.Transform.Position2D = charge.Transform.Position2D;
                    _popup.Enabled = true;
                    _popupEnabledThisFrame = true;

                    CoroutineManager.StartCoroutine(DefaultRoutines.Linear(0.3f, (x) =>
                    {
                        chargeSprite.Size = new Vector2(1, 1) * (1 + Easings.CircularEaseOut(x) / 4.0f);
                        charge.Transform.Scale = new Vector3(1, 1, 1) * (1 + Easings.CircularEaseOut(x) / 4.0f);

                        _popup.GetComponent<Sprite>().Size = new Vector2(1, 1) * (Easings.CircularEaseOut(x));
                        _popup.Transform.Scale = new Vector3(1, 1, 1) * (Easings.CircularEaseOut(x));
                    }));
                };

                chargeHit.OnMouseStay += (ev) =>
                {
                    _popup.GetComponent<TextComponent>().DisplayText = $"{fixedCharge.desc}\nHave: {fixedCharge.owned} (${fixedCharge.debtPerSecond * fixedCharge.owned}/s)\nTotal: {GetDisplayStringSmall(_fixedChargesGenerated[fixedCharge.id])}";
                };

                chargeHit.OnMouseExit += (ev) =>
                {
                    if(!_popupEnabledThisFrame)
                        _popup.Enabled = false;

                    CoroutineManager.StartCoroutine(DefaultRoutines.Linear(0.3f, (x) =>
                    {
                        chargeSprite.Size = new Vector2(1, 1) * (1 + Easings.QuadraticEaseOut(1.0f - x) / 4.0f);
                        charge.Transform.Scale = new Vector3(1, 1, 1) * (1 + Easings.QuadraticEaseOut(1.0f - x) / 4.0f);
                    }));
                };

                chargeHit.OnMouseUp += (ev) =>
                {
                    if (debt >= fixedCharge.price)
                    {
                        debtPerSecond += fixedCharge.debtPerSecond;
                        debt -= fixedCharge.price;
                        _shownDebt = debt;
                        _helpText.Enabled = false;

                        fixedCharge.owned++;
                        fixedCharge.price = (ulong)(fixedCharge.price * JMath.Pow(1.15f, fixedCharge.owned));
                        chargeText.DisplayText = $"{fixedCharge.name} {GetDisplayStringSmall(fixedCharge.price)}";

                        audio.Stop();
                        audio.Play();
                        audio.SetVolume(0.5f);
                    }

                    CoroutineManager.StartCoroutine(DefaultRoutines.Linear(0.3f, (x) =>
                    {
                        chargeSprite.Size = new Vector2(1, 1) * (1 + Easings.CircularEaseOut(x) / 4.0f);
                        charge.Transform.Scale = new Vector3(1, 1, 1) * (1 + Easings.CircularEaseOut(x) / 4.0f);
                    }));
                };
            }

            _popup = AddGameObject("Popup");
            _popup.Enabled = false;

            Sprite popupSprite = _popup.AddComponent<Sprite>();
            popupSprite.Texture = ResourceManager.Load<Texture2D>("Textures/WebsiteBuyFrame.png");
            popupSprite.Offset = new Vector2(-popupSprite.Texture.Width / 2, popupSprite.Texture.Height / 64);
            popupSprite.Priority = 0.000001f;

            TextComponent popupText = _popup.AddComponent<TextComponent>();
            popupText.Alignment = TextAlignment.Left;
            popupText.Offset = new Vector2(-50, popupSprite.Texture.Height / 1.5f);
            popupText.DisplayText = "All your base\nare belong to\nus.";
            popupText.Color = Color.Black;

            _timer = AddGameObject("Timer");
            _timer.Transform.Position2D = new Vector2(0, -68);
            _timerText = _timer.AddComponent<TextComponent>();
            _timerText.Alignment = TextAlignment.Center;            
            _timerText.DisplayText = "2:30 remaining";

            EmptyUIElement startelement = new EmptyUIElement(GUI.Root);
            startelement.Dimensions = new Vector2(80, 20) * _defaultZoom * 2;
            startelement.Pivot = UIDefaults.Centered;

            startelement.OnMouseEnter += (ev) =>
            {
                CoroutineManager.StartCoroutine(DefaultRoutines.Linear(0.3f, (x) =>
                {
                    _exit.GetComponent<Sprite>().Size = new Vector2(1, 1) * (1 + Easings.CircularEaseOut(x) / 4.0f);
                }));
            };

            startelement.OnMouseExit += (ev) =>
            {
                CoroutineManager.StartCoroutine(DefaultRoutines.Linear(0.3f, (x) =>
                {
                    _exit.GetComponent<Sprite>().Size = new Vector2(1, 1) * (1 + Easings.QuadraticEaseOut(1.0f - x) / 4.0f);
                }));
            };

            startelement.OnMouseUp += (x) =>
            {
                SceneManager.SwitchToScene(new MainMenu(ResourceManager));
            };

            _exit = AddGameObject("Exit");
            _exit.Transform.Position2D = new Vector2(0, 108);

            _exit.AddComponent<UIComponent>().Setup(startelement, this);

            Sprite exitSprite = _exit.AddComponent<Sprite>();
            exitSprite.Texture = ResourceManager.Load<Texture2D>("Textures/buttons.png");
            exitSprite.SourceRectangle = new Rectangle(0, 20, 128, 20);
            exitSprite.Offset = new Vector2(-35, -9);

            _helpText = AddGameObject("help").AddComponent<TextComponent>();
            _helpText.DisplayText = "Exchange debt to gain\n  debt automatically!";
            _helpText.Alignment = TextAlignment.Center;

            _helpText.Parent.Transform.Position2D = new Vector2(0, -110);
            _helpText.Enabled = false;

            _exit.Enabled = false;
        }

        private void WebsiteEnter(UIEvent ev)
        {
            CoroutineManager.StartCoroutine(DefaultRoutines.Linear(0.3f, (x) =>
            {
                _websiteSprite.Size = new Vector2(1, 1) * (1 + Easings.CircularEaseOut(x) / 4.0f);
            }));
        }

        private void WebsiteExit(UIEvent ev)
        {
            CoroutineManager.StartCoroutine(DefaultRoutines.Linear(0.3f, (x) =>
            {
                _websiteSprite.Size = new Vector2(1, 1) * (1 + Easings.QuadraticEaseOut(1.0f - x) / 4.0f);
            }));
        }

        private void WebsiteClick(UIEvent ev)
        {
            WebsiteEnter(null);

            AudioComponent audio = _website.GetComponent<AudioComponent>();

            audio.Play();
            audio.SetVolume(0.35f);

            if (!_finished)
            {
                AddDebt(1);

                Vector2 pos = DefaultCamera.ScreenPointToWorld(InputManager.Instance.MousePosition);
                BurstDollars(1, pos);

                _websiteSprite.Texture = ResourceManager.Load<Texture2D>($"Textures/Website{Random.NextRange(0, MAX_WEBSITES)}.png");
            }
        }

        public void BurstDollars(int amount, Vector2 position)
        {
            _websiteParticles.Parent.Transform.Position2D = position;
            _websiteParticles.BurstAmount = amount > 255 ? 255 : amount;

            _websiteParticles.Burst();
        }

        private void AddDebt(ulong debtAdded)
        {
            if (debtAdded == 0)
                return;

            ulong debtBefore = debt;
            ulong debtTarget = debt + debtAdded;

            debt += debtAdded;

            if (debtAdded == 1)
                _shownDebt = debt;

            CoroutineManager.StartCoroutine(DefaultRoutines.Linear(0.3f, (x) =>
            {
                _debtCounter.Transform.Scale = new Vector3(1, 1, 1) * (1 + Easings.QuadraticEaseOut(1.0f - x) / 4.0f);

                if(debtAdded != 1)
                    _shownDebt = (ulong)JMath.Interpolate((float)debtBefore, (float)debtTarget, x);
            }));

            for(int i = 0; i < FixedCharges.Length; ++i)
            {
                if(!_fixedCharges[i].Enabled)
                {
                    _fixedCharges[i].Enabled = debt >= FixedCharges[i].unlockPrice;

                    if (_fixedCharges[i].Enabled)
                    {
                        _helpText.Parent.Transform.Position2D = _fixedCharges[i].Transform.Position2D + new Vector2(0, -26);

                        if(i == 0)
                            _helpText.Enabled = true;
                    }
                }
            }
        }

        /// <summary>
        /// Convert a number to a presentable string.
        /// </summary>
        /// <param name="value">The value to make pretty.</param>
        /// <returns>Presentable string representing the value.</returns>
        private string GetDisplayString(ulong value)
        {
            return $"${value.ToString("#,##")}";
        }

        /// <summary>
        /// Convert a number to a small presentable string.
        /// </summary>
        /// <param name="value">The value to make pretty.</param>
        /// <returns>Presentable string representing the value in few characters as possible.</returns>
        private string GetDisplayStringSmall(ulong value)
        {
            return $"${value.KiloFormat()}";
        }

        public void ShowLeaderboard()
        {
            PlayfabTaskLeaderboard task = PlayfabManager.Leaderboard.GetLeaderboard("Highscore", 0, 100);

            task.OnTaskCompleted += (lbTask) =>
            {
                if(lbTask.Success)
                {
                    PlayfabTaskLeaderboard leaderboardTask = (PlayfabTaskLeaderboard)lbTask;
                    Leaderboard leaderboard = leaderboardTask.Leaderboard;

                    GameObject[] entries = new GameObject[leaderboard.Entries.Count];

                    for (int i = 0; i < leaderboard.Entries.Count; ++i)
                    {
                        LeaderboardEntry entry = leaderboard.Entries[i];

                        GameObject lbEntryObj = AddGameObject($"Entry{i}");
                        lbEntryObj.Enabled = false;
                        entries[i] = lbEntryObj;

                        lbEntryObj.Transform.Position = new Vector3(0, -20 * i, -1f);

                        _lowestScroll = -20 * i;

                        Sprite entrySprite = lbEntryObj.AddComponent<Sprite>();
                        entrySprite.Texture = ResourceManager.Load<Texture2D>("Textures/buttons.png");

                        entrySprite.SourceRectangle = new Rectangle(0, 40, 128, 18);

                        if (i == 0)
                            entrySprite.SourceRectangle = new Rectangle(0, 58, 128, 18);
                        else if(i == 1)
                            entrySprite.SourceRectangle = new Rectangle(0, 58 + 18, 128, 18);
                        else if (i == 2)
                            entrySprite.SourceRectangle = new Rectangle(0, 58 + 18 * 2, 128, 18);

                        entrySprite.Offset = new Vector2(-entrySprite.Texture.Width / 2, -entrySprite.Texture.Height / 2);
                        entrySprite.Priority = 0.1f;

                        TextComponent text = lbEntryObj.AddComponent<TextComponent>();
                        text.Alignment = TextAlignment.Left;
                        text.Offset = new Vector2(-62, -64);

                        string name = entry.displayName;
                        if (name != null && name.Length > 12)
                        {
                            name = name.Substring(0, 10);
                            name += "..";
                        }

                        text.DisplayText = $"{entry.position}. {name} - {entry.value.ToString("#,##")}";
                        text.Color = Color.Black;
                    }

                    CoroutineManager.StartCoroutine(DefaultRoutines.LinearRepeat(1.0f, (x) =>
                    {
                        for (int i = 0; i < entries.Length; ++i)
                        {
                            entries[i].Transform.Rotation2D = JMath.Sin(Time.TotalSeconds + i) * (JMath.PI_OVER_TWO / 32);
                        }
                    }));

                    CoroutineManager.StartCoroutine(RevealLeaderboard(entries));
                }
                else
                {
                    System.Console.WriteLine("Failed to get leaderboards... :(");
                }
            };
        }

        IEnumerator RevealLeaderboard(GameObject[] objects)
        {
            for (int i = 0; i < objects.Length; ++i)
            {
                objects[i].Enabled = true;
                yield return new WaitForSeconds(0.1f);
            }
        }

        protected override void PreUpdate()
        {
            // After game is finished.
            if (_timeLeft < 0)
            {
                _timeLeft -= Time.DeltaTimeRealTime;

                if (_timeLeft < -1)
                {
                    if (InputManager.Instance.MouseKeyHeld(MouseKey.LeftMouse))
                        DefaultCamera.Parent.Transform.Translate2D((InputManager.Instance.MouseDelta * new Vector2(0, GraphicsManager.Instance.Height)) / DefaultCamera.Zoom / Config.ConfigValues.PixelSize);

                    if (DefaultCamera.Parent.Transform.Position2D.Y > 0)
                        DefaultCamera.Parent.Transform.Position2D = new Vector2(DefaultCamera.Parent.Transform.Position2D.X, 0);

                    if (DefaultCamera.Parent.Transform.Position2D.Y < _lowestScroll)
                        DefaultCamera.Parent.Transform.Position2D = new Vector2(DefaultCamera.Parent.Transform.Position2D.X, _lowestScroll);
                }

                if (!_finished)
                {
                    _finished = true;
                    _timer.Enabled = false;

                    _exit.Enabled = true;

                    PlayfabTask task = PlayfabManager.Leaderboard.SetLeaderboardEntry(new string[] { "Highscore", "TotalScore", "Attempts" }, new int[] { (int)debt, (int)debt, 1 });
                    task.OnTaskCompleted += (x) =>
                    {
                        // Show leaderboard AFTER uploading score. regardless if an error occured or not.
                        CoroutineManager.StartCoroutine(ShowLeaderboards());
                    };


                    for (int i = 0; i < _fixedCharges.Length; ++i)
                    {
                        _fixedCharges[i].GetComponent<UIComponent>().Enabled = false;
                    }

                    _websiteSprite.Texture = ResourceManager.Load<Texture2D>("Textures/WebsiteBlocked.png");

                    Vector2 start = DefaultCamera.Parent.Transform.Position2D;

                    CoroutineManager.StartCoroutine(DefaultRoutines.Linear(1.0f, (x) =>
                    {
                        DefaultCamera.Parent.Transform.Position2D = Vector2.Interpolate(start, new Vector2(0, 0), x);

                        for (int i = 0; i < _fixedCharges.Length; ++i)
                        {
                            _fixedCharges[i].Transform.Position2D += new Vector2(0, -128 * x);
                        }
                    }));
                }
            }
            // During the game.
            else
            {
                // Move camera zoom on the beat.
                if (GAME_TIME - _timeLeft > KICK_IN_BPM_EFFECT)
                    DefaultCamera.Zoom = _defaultZoom + JMath.Clamp(JMath.Sin(Time.TotalSeconds * JMath.TWO_PI * (BPM / 60)), 0, 1) * 0.025f;

                _debtTimer += Time.DeltaTimeRealTime;
                _timeLeft -= Time.DeltaTimeRealTime;

                System.TimeSpan span = System.TimeSpan.FromSeconds(_timeLeft);
                _timerText.DisplayText = $"{span.Minutes}:{span.Seconds.ToString().PadLeft(2, '0')} remaining";

                while (_debtTimer >= 1.0f)
                {
                    AddDebt(debtPerSecond);
                    BurstDollars(debtPerSecond == 0 ? 0 : (int)debtPerSecond / 5 + 1, _debtPerSecondCounter.Transform.Position2D);

                    _debtTimer -= 1.0f;

                    for (int i = 0; i < _fixedCharges.Length; ++i)
                    {
                        _fixedChargesGenerated[i] += FixedCharges[i].debtPerSecond * FixedCharges[i].owned;
                    }
                }

                _debtText.DisplayText = "Debt " + GetDisplayString(_shownDebt);
                _debtTextShadow.DisplayText = _debtText.DisplayText;

                _debtPerSecondText.DisplayText = $"${debtPerSecond}/s";
                _debtPerSecondTextShadow.DisplayText = _debtPerSecondText.DisplayText;

                for (int i = 0; i < _fixedCharges.Length; ++i)
                {
                    if (debt >= FixedCharges[i].price)
                        _fixedCharges[i].GetComponent<Sprite>().Color = Color.White;
                    else
                        _fixedCharges[i].GetComponent<Sprite>().Color = new Color(0.5f, 0.5f, 0.5f, 1.0f);

                    _fixedCharges[i].Transform.Rotation2D = JMath.Sin(Time.TotalSeconds + i) * (JMath.PI_OVER_TWO / 32);
                }
            }

            // Rotate website.
            _website.Transform.Rotation2D = JMath.Sin(Time.TotalSeconds) * (JMath.PI_OVER_TWO / 16);

            _exit.Transform.Rotation2D = JMath.Sin(Time.TotalSeconds + 2) * (JMath.PI_OVER_TWO / 16);

            // Scroll background.
            _background.Transform.Translate2D(new Vector2(-16, -16) * Time.DeltaTime * _defaultZoom);
        }

        IEnumerator ShowLeaderboards()
        {
            yield return new WaitForSeconds(2.0f);
            ShowLeaderboard();
        }

        protected override void LateUpdate()
        {
            _popupEnabledThisFrame = false;
        }

        protected override void FinalizeScene()
        {
            
        }
    }
}
