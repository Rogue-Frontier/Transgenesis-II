using Microsoft.Xna.Framework;
using SadConsole;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transgenesis {
    class Scroller {
        int scrolling = 0;
        int screenRows = 45;
        Input i;
        ConsoleManager c;

        public Scroller(Input i, ConsoleManager c) {
            this.i = i;
            this.c = c;
        }
        public void Update() { }
        public void Handle(ConsoleKeyInfo k) {
            switch(k.Key) {
                case ConsoleKey.PageUp when i.Text.Length == 0:
                    scrolling--;
                    break;
                case ConsoleKey.PageDown when i.Text.Length == 0:
                    scrolling++;
                    break;
            }
        }
        public void Draw(List<ColoredString> buffer) {
            scrolling = Math.Max(0, Math.Min(scrolling, buffer.Count - screenRows));
            c.margin = new Point(0, 0);
            c.SetCursor(c.margin);
            var count = Math.Min(screenRows, buffer.Count);
            var lines = buffer.GetRange(scrolling, count);

            //Let user know that there's more text
            if(scrolling > 0) {
                lines[0] = c.CreateString("...");
            }
            if (scrolling + count + 1 < buffer.Count) {
                lines[lines.Count - 1] = c.CreateString("...");
            }
            foreach (var line in lines) {
                c.Write(line);
                //Printing to the edge of the view already moves the cursor to the next line
                if (line.Count < c.width) {
                    c.NextLine();
                }
            }
        }
    }
}
