using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.IO;
using System.Diagnostics;

namespace Transgenesis {
    class Program {

        static void Main(string[] args) {
            new Program().Run();
        }

        public void Run() {
            Stack<IComponent> screens = new Stack<IComponent>();
            screens.Push(new Commander(screens));
            Console.CursorVisible = false;
            bool draw = true;
            while (true) {
                while (Console.KeyAvailable) {
                    var k = Console.ReadKey(true);
                    screens.Peek().Handle(k);
                    draw = true;
                }
                screens.Peek().Update();
                if (draw) {
                    (int left, int top) = (Console.WindowLeft, Console.WindowTop);

                    screens.Peek().Draw();
                    Console.BackgroundColor = ConsoleColor.Black;
                    Console.ForegroundColor = ConsoleColor.White;

                    (Console.WindowLeft, Console.WindowTop) = (left, top);

                }
                draw = false;
            }
        }
    }
}
