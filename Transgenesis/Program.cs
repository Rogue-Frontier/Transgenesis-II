
using System.Collections.Generic;
using SadRogue.Primitives;
using Game = SadConsole.Game;
using System;
using Keys = SadConsole.Input.Keys;
using SadConsole.UI;
using SadConsole.Input;
using SadConsole;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Transgenesis {
    internal class Program {
        static int width = 150;
        static int height = 90;
        private static void Main(string[] args) {
            /*
            Dictionary<string, string> dict = new();
            foreach(var line in File.ReadAllLines("LispFunctionList.txt")) {
                Match m;
                if((m = new Regex("^\\((?<function>[^ ]+)(?<args>[^\\)]*)\\) -> (?<result>.+)$").Match(line)).Success) {
                    var function = m.Groups["function"].Value;
                    var argList = m.Groups["args"].Value.Trim().Split(' ');
                    dict[function] = line;
                }
            }
            */



            //SadConsole.UI.Themes.Library.Default.Colors.ControlHostBack = Color.Black;
            //SadConsole.UI.Themes.Library.Default.Colors.ControlBack = Color.Gray;

            //SadConsole.Settings.UnlimitedFPS = true;
            SadConsole.Settings.UseDefaultExtendedFont = true;
            SadConsole.Game.Create(width, height, "Content/IBMCGA.font", g => {
            });
            SadConsole.Game.Instance.OnStart = Init;
            SadConsole.Game.Instance.Run();
            SadConsole.Game.Instance.Dispose();


        }

        private static void Init() {
            var c = new MainConsole(width, height);

            //This allows trailing spaces to show up in command
            c.Cursor.DisableWordBreak = true;

            SadConsole.Game.Instance.Screen = c;
            c.FocusOnMouseClick = true;
        }
    }
    class MainConsole : ControlsConsole {
        Environment env = new();
        Queue<Stack<IScreen>> sessions = new();
        Stack<IScreen> screens;
        int index;
        public MainConsole(int width, int height) : base(width, height) {
            //screens.Push(new MainMenu(screens));
            //screens.Push(new TextEditor(screens, new ConsoleManager(new Point(0, 0)), s => { }));
            var s = "Schema.json";
            if (File.Exists(s)) {
                env = new(File.ReadAllText(s));
            }
            env.LoadState();
            try {
            } catch(Exception e) {
                throw;
            }
            CreateSession();
            DefaultBackground = Color.Black;
            DefaultForeground = Color.White;
        }
        public void CreateSession() {
            if(screens != null) {
                sessions.Enqueue(screens);
            }
            screens = new();
            screens.Push(new MainMenu(screens, env));
            index = sessions.Count;
        }
        public void SwitchSession() {
            sessions.Enqueue(screens);
            NextSession();
        }
        public void NextSession() {
            index = (index + 1) % sessions.Count;
            screens = sessions.Dequeue();
        }
        public override void Update(TimeSpan delta) {
            base.Update(delta);
            screens.Peek().Update();
        }
        public override void Render(TimeSpan delta) {
            base.Render(delta);
            screens.Peek().Draw();
            this.Print(0, 0, $"Session {index + 1} / {sessions.Count + 1} - {screens.Peek().name}");
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
        public override bool ProcessMouse(MouseScreenObjectState state) {
            screens.Peek().Handle(state);
            return base.ProcessMouse(state);
        }
    }
}
