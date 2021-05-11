using JuiceboxEngine;
using JuiceboxEngine.Audio;
using JuiceboxEngine.Coroutines;
using JuiceboxEngine.Graphics;
using JuiceboxEngine.GUI;
using JuiceboxEngine.Input;
using JuiceboxEngine.Math;
using JuiceboxEngine.Playfab;
using JuiceboxEngine.Resources;
using JuiceboxEngine.Util;
using System;
using System.Collections;

namespace LD48
{
    class MainMenu : Scene
    {
        private const int BPM = 132;

        private float _defaultZoom;
        private GameObject _title;
        private GameObject _start;
        private GameObject _background;
        private TextComponent _leaderboardText;
        private TextComponent _nameText;

        private AudioComponent _audio;

        private string _loginID;

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

        float _lowestScroll;

        public MainMenu(ResourceManager manager) 
            : base(manager)
        {
            GraphicsManager.Instance.OnResize += (x, y) =>
            {
                ScaleGame();
            };
        }

        private void Login()
        {
            _loginID = LocalStorage.GetValue("login_id").As<string>();

            if (_loginID == null)
            {
                _loginID = Guid.NewGuid().ToString();

                Console.WriteLine("Registering with Playfab...");
                _nameText.DisplayText = "Registering...";
                PlayfabManager.Identity.LoginWithCustomID(_loginID, true);
            }
            else
            {
                Console.WriteLine("Logging in with Playfab...");
                PlayfabManager.Identity.LoginWithCustomID(_loginID, false);
            }
            PlayfabManager.Identity.LoginTask.OnTaskCompleted += LoginFinished;
        }

        private void LoginFinished(PlayfabTask task)
        {
            if(task.Success)
            {
                LocalStorage.StoreValue("login_id", _loginID);
                _nameText.DisplayText = "Welcome!";
                Console.WriteLine($"Logged in with Playfab! {PlayfabManager.Identity.Username}");

                PlayfabManager.Identity.GetDisplayNameTask.OnTaskCompleted += (nameTask) =>
                {
                    if(PlayfabManager.Identity.Username != null)
                        _nameText.DisplayText = $"Welcome, {PlayfabManager.Identity.Username}";
                };
            }
            else
            {
                Console.WriteLine("Failed to log in with Playfab.");
            }

            ShowLeaderboard();
        }

        protected override void InitializeScene()
        {
            ScaleGame();

            Text text = new Text(GUI.Root);
            text.Pivot = UIDefaults.TopCenter;
            text.Anchor = UIDefaults.TopCenter;
            text.ShadowOffset = new Point(1, -1);
            text.DisplayText = "Created by Mathijs Koning and Thom Zeilstra for Ludum Dare 48";
            text.ResizeToText(16);

            _background = AddGameObject("Background");
            TileMap map = _background.AddComponent<TileMap>();
            map.TileSize = 16;
            map.MapData = ResourceManager.Load<Texture2D>("Textures/backgroundData.png");
            map.MapData.Wrap = Texture2D.WrapMode.Repeat;
            map.Sprites = ResourceManager.Load<Texture2D>("Textures/background.png");

            _audio = _background.AddComponent<AudioComponent>();
            _audio.SetAudioClip(ResourceManager.Load<AudioClip>("Sounds/menu.mp3"));
            _audio.Play();
            _audio.Loop(true);

            _title = AddGameObject("Title");
            _title.Transform.Position2D = new Vector2(0, 64);
            Sprite titleSprite = _title.AddComponent<Sprite>();
            titleSprite.Texture = ResourceManager.Load<Texture2D>("Textures/Title.png");
            titleSprite.Offset = new Vector2(-titleSprite.Texture.Width / 2, -titleSprite.Texture.Height / 2);
            titleSprite.Size = new Vector2(2, 2);

            _leaderboardText = AddGameObject().AddComponent<TextComponent>();
            _leaderboardText.Alignment = TextAlignment.Center;
            _leaderboardText.DisplayText = "Loading leaderboard...";
            _leaderboardText.Color = Color.Black;

            _leaderboardText.Parent.Transform.Position2D = new Vector2(0, -48);

            _nameText = AddGameObject().AddComponent<TextComponent>();
            _nameText.Alignment = TextAlignment.Center;
            _nameText.DisplayText = "Logging in...";
            _nameText.Color = Color.Black;

            UIElement ui = new EmptyUIElement(GUI.Root);
            ui.Dimensions = new Vector2(128, 16) * _defaultZoom * 2;
            ui.Pivot = UIDefaults.BottomCenter;

            ui.OnMouseUp += (ev) =>
            {
                AskUsername("Leaderboard user name:");
            };

            ui.OnMouseEnter += (ev) =>
            {
                _nameText.Color = Color.White;
            };

            ui.OnMouseExit += (ev) =>
            {
                _nameText.Color = Color.Black;
            };

            UIComponent uiComp = _nameText.Parent.AddComponent<UIComponent>();
            uiComp.Setup(ui, this);

            _nameText.Parent.Transform.Position2D = new Vector2(0, 16);

            EmptyUIElement startelement = new EmptyUIElement(GUI.Root);
            startelement.Dimensions = new Vector2(80, 20) * _defaultZoom * 2;
            startelement.Pivot = UIDefaults.Centered;

            startelement.OnMouseEnter += (ev) =>
            {
                CoroutineManager.StartCoroutine(DefaultRoutines.Linear(0.3f, (x) =>
                {
                    _start.GetComponent<Sprite>().Size = new Vector2(1, 1) * (1 + Easings.CircularEaseOut(x) / 4.0f);
                }));
            };

            startelement.OnMouseExit += (ev) =>
            {
                CoroutineManager.StartCoroutine(DefaultRoutines.Linear(0.3f, (x) =>
                {
                    _start.GetComponent<Sprite>().Size = new Vector2(1, 1) * (1 + Easings.QuadraticEaseOut(1.0f - x) / 4.0f);
                }));
            };

            startelement.OnMouseUp += (x) =>
            {
                if (PlayfabManager.Identity.Username == null)
                {
                    AskUsername("Leaderboard user name:");
                }
                else
                {
                    SceneManager.SwitchToScene(new MainScene(ResourceManager));
                }
            };

            _start = AddGameObject("Start");
            _start.Transform.Position2D = new Vector2(0, 0);

            _start.AddComponent<UIComponent>().Setup(startelement, this);

            Sprite startSprite = _start.AddComponent<Sprite>();
            startSprite.Texture = ResourceManager.Load<Texture2D>("Textures/buttons.png");
            startSprite.SourceRectangle = new Rectangle(0, 0, 128, 20);
            startSprite.Offset = new Vector2(-35, -9);

            Login();
        }

        public void ShowLeaderboard()
        {
            PlayfabTaskLeaderboard task = PlayfabManager.Leaderboard.GetLeaderboard("Highscore", 0, 100);

            task.OnTaskCompleted += (lbTask) =>
            {
                if (lbTask.Success)
                {
                    _leaderboardText.DisplayText = "Deepest in debt:";

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
                        entrySprite.Priority = 0.1f;

                        if (i == 0)
                            entrySprite.SourceRectangle = new Rectangle(0, 58, 128, 18);
                        else if (i == 1)
                            entrySprite.SourceRectangle = new Rectangle(0, 58 + 18, 128, 18);
                        else if (i == 2)
                            entrySprite.SourceRectangle = new Rectangle(0, 58 + 18 * 2, 128, 18);

                        entrySprite.Offset = new Vector2(-entrySprite.Texture.Width / 2, -entrySprite.Texture.Height / 2);

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
                    _leaderboardText.DisplayText = "Can't load leaderboard.";
                    System.Console.WriteLine("Failed to get leaderboards... :(");
                }
            };
        }

        IEnumerator RevealLeaderboard(GameObject[] objects)
        {
            for (int i = 0; i < objects.Length; ++i)
            {
                objects[i].Enabled = true;
                yield return new WaitForSeconds(0.02f);
            }
        }

        protected override void PreUpdate()
        {
            _background.Transform.Translate2D(new Vector2(-16, -16) * Time.DeltaTime * _defaultZoom);

            if (InputManager.Instance.MouseKeyHeld(MouseKey.LeftMouse))
            {
                DefaultCamera.Parent.Transform.Translate2D((InputManager.Instance.MouseDelta * new Vector2(0, GraphicsManager.Instance.Height)) / DefaultCamera.Zoom / Config.ConfigValues.PixelSize);

                _audio.Play();
                _audio.Loop(true);
            }

            DefaultCamera.Zoom = _defaultZoom + JMath.Clamp(JMath.Sin(Time.TotalSeconds * JMath.TWO_PI * (BPM / 60)), 0, 1) * 0.025f;

            if (DefaultCamera.Parent.Transform.Position2D.Y > 0)
                DefaultCamera.Parent.Transform.Position2D = new Vector2(DefaultCamera.Parent.Transform.Position2D.X, 0);

            if (DefaultCamera.Parent.Transform.Position2D.Y < _lowestScroll)
                DefaultCamera.Parent.Transform.Position2D = new Vector2(DefaultCamera.Parent.Transform.Position2D.X, _lowestScroll);

            _title.Transform.Rotation2D = JMath.Sin(Time.TotalSeconds) * (JMath.PI_OVER_TWO / 16);
            _start.Transform.Rotation2D = JMath.Sin(Time.TotalSeconds + 1) * (JMath.PI_OVER_TWO / 16);
        }

        private void AskUsername(string msg)
        {
            string preview = PlayfabManager.Identity.Username == null ? $"Guest #{JuiceboxEngine.Util.Random.NextRange(0, 999999)}" : PlayfabManager.Identity.Username;
            string username = Browser.Prompt(msg, preview);

            // No username given, so don't update name.
            if (username == null)
            {
                if (PlayfabManager.Identity.Username == null)
                    username = preview;
                else
                    return;
            }

            PlayfabTask task = PlayfabManager.Identity.UpdateDisplayName(username);
            task.OnTaskCompleted += UpdateUsername;
        }

        private void UpdateUsername(PlayfabTask task)
        {
            if (task.Success)
            {
                _nameText.DisplayText = $"Welcome, {PlayfabManager.Identity.Username}";
            }
            else
            {
                AskUsername("Something went wrong. " + task.ErrorMessage);
            }
        }

        protected override void LateUpdate()
        {
            
        }

        protected override void FinalizeScene()
        {
            
        }
    }
}
