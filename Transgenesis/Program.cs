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

namespace Transgenesis {
    class Program : Game {
        public static void Main(string[] args) {
            using(var game = new Program()) {
                game.Run();
            }
        }
        class KeyboardHandler : KeyboardConsoleComponent {
            public override void ProcessKeyboard(SadConsole.Console console, Keyboard info, out bool handled) {
                handled = true;
            }
        }
        public Program() : base("Content/IBM_ext.font", 80, 50, null) { }
        Stack<IComponent> screens = new Stack<IComponent>();
        protected override void Initialize() {
            screens.Push(new Commander(screens));

            IsMouseVisible = true;
            base.Initialize();
            var con = new Console(80, 50);
            SadConsole.Global.CurrentScreen = con;
            con.IsVisible = true;
            con.IsFocused = true;
            con.FillWithRandomGarbage();
        }
        protected override void Update(GameTime delta) {
            base.Update(delta);
            screens.Peek().Update();
        }
        protected override void Draw(GameTime delta) {
            GraphicsDevice.Clear(Color.Black);
            base.Draw(delta);
            screens.Peek().Draw();
        }
    }
}
