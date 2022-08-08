using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transgenesis {
    class Scroller {
        public int scrolling = 0;
        public int screenRows = 48;
        Input i;
        ConsoleManager c;

        public Scroller(ConsoleManager c, Input i = null) {
            this.i = i;
            this.c = c;
        }
        public void Update() { }
        public void Handle(ConsoleKeyInfo k) {
            switch(k.Key) {
                case ConsoleKey.PageUp when i == null || i.Text.Length == 0:
                    scrolling--;
                    break;
                case ConsoleKey.PageDown when i == null || i.Text.Length == 0:
                    scrolling++;
                    break;
            }
        }
        public void Handle(MouseScreenObjectState mouse) {
            var delta = mouse.Mouse.ScrollWheelValueChange;
            if (delta != 0 && (i == null || i.Text.Length == 0)) {
                scrolling += delta / 120;
            }
        }
        public void Draw(List<ColoredString> buffer) => Draw(buffer, screenRows);
        public void Draw(List<ColoredString> buffer, int screenRows) {
            scrolling = Math.Max(0, Math.Min(scrolling, buffer.Count - screenRows));
            c.margin = new Point(0, 1);
            c.SetCursor(c.margin);
            var count = Math.Min(screenRows, buffer.Count);
            var lines = buffer.GetRange(scrolling, count);

            //Let user know that there's more text
            if(scrolling > 0) {
                lines[0] = c.Color("...");
            }
            if (scrolling + count < buffer.Count) {
                lines[lines.Count - 1] = c.Color("...");
            }
            int printed = 0;
            foreach (var line in lines) {
                c.Write(line);
                //Printing to the edge of the view already moves the cursor to the next line
                if (line.Length < c.width) {
                    c.NextLine();
                }
                printed++;
            }
            while(printed < screenRows) {
                c.NextLine();
                printed++;
            }
        }
    }
}
