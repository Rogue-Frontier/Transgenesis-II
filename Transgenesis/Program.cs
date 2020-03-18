using SadConsole;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.IO;
using System.Diagnostics;
using SadConsole.Components;
using SadConsole.Input;
using MonoGame;
using Microsoft.Xna.Framework;
using Game = SadConsole.Game;
using System;
using SadConsole.Themes;
using Keys = Microsoft.Xna.Framework.Input.Keys;
using Newtonsoft.Json;

namespace Transgenesis {
    class Program : Game {
        public static void Main(string[] args) {
            using (var game = new Program()) {
                game.Run();
            }
        }
        public Program() : base("Content/IBM_ext.font", 150, 65, null) { }
        protected override void Initialize() {
            IsMouseVisible = true;
            base.Initialize();
            var con = new MainConsole(150, 65);
            //This allows trailing spaces to show up in command
            con.Cursor.DisableWordBreak = true;

            SadConsole.Global.CurrentScreen = con;
            con.IsVisible = true;
            con.IsFocused = true;
            con.Font = con.Font.Master.GetFont(Font.FontSizes.One);
            GraphicsDeviceManager.IsFullScreen = true;
        }
    }
    class MainConsole : ControlsConsole {
        Environment env = new Environment();
        Queue<Stack<IComponent>> sessions = new Queue<Stack<IComponent>>();
        Stack<IComponent> screens;
        public MainConsole(int width, int height) : base(width, height) {
            //screens.Push(new MainMenu(screens));
            //screens.Push(new TextEditor(screens, new ConsoleManager(new Point(0, 0)), s => { }));

            try {
                env.LoadState();
            } catch(Exception e) {
                throw;
            }
            CreateSession();

            Theme = new WindowTheme {
                ModalTint = Color.Black,
                FillStyle = new Cell(Color.White, Color.Black),
            };
            DefaultBackground = Color.Black;
            DefaultForeground = Color.White;
        }
        public void CreateSession() {
            if(screens != null) {
                sessions.Enqueue(screens);
            }
            screens = new Stack<IComponent>();
            screens.Push(new MainMenu(screens, env));
        }
        public void SwitchSession() {
            sessions.Enqueue(screens);
            NextSession();
        }
        public void NextSession() {
            screens = sessions.Dequeue();
        }
        public override void Update(TimeSpan delta) {
            base.Update(delta);
            screens.Peek().Update();
        }
        public override void Draw(TimeSpan delta) {
            base.Draw(delta);
            screens.Peek().Draw();
        }
        public override bool ProcessKeyboard(Keyboard info) {
            bool handle = true;
            
            //The greatest hack I've ever written
            //This translates keyboard info from MonoGame to key events from Windows Console
            bool shift = info.IsKeyDown(Keys.LeftShift) || info.IsKeyDown(Keys.RightShift);
            bool alt = info.IsKeyDown(Keys.LeftAlt) || info.IsKeyDown(Keys.RightAlt);
            bool ctrl = info.IsKeyDown(Keys.LeftControl) || info.IsKeyDown(Keys.RightControl);
            foreach (var key in info.KeysPressed) {
                if(key == Keys.Back) {
                    //Global.Break();
                }
                if(key == Keys.Tab) {
                    if(shift) {
                        CreateSession();
                    } else {
                        SwitchSession();
                    }
                    break;
                }
                screens.Peek().Handle(new ConsoleKeyInfo(key.Character, (ConsoleKey)key.Key, shift, alt, ctrl));
            }
            if(screens.Count == 0) {
                if(sessions.Count == 0) {
                    System.Environment.Exit(0);
                } else {
                    screens = sessions.Dequeue();
                }
            }
            return handle;
        }
    }
}
