using Microsoft.Xna.Framework;
using SadConsole;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using static Transgenesis.Global;

namespace Transgenesis {
    class Theme {
        public Color front = Color.White;
        //public Color back = Color.Multiply(Color.Turquoise, 0.25f).FillAlpha();
        public Color back = new Color(0, (int) (51 * 0.75), (int) (102 * 0.75), 255);
        public Color highlight = Color.LimeGreen;
        public void Deconstruct(out Color front, out Color back) {
            (front, back) = (this.front, this.back);
        }
    }
    class ConsoleManager {
        SadConsole.Console console => SadConsole.Global.CurrentScreen;
        SadConsole.Cursor cursor => SadConsole.Global.CurrentScreen.Cursor;
        public int width => console.Width;
        public ConsoleManager(Point p) {
            this.margin = p;
            theme = new Theme();
        }

        public Point margin;
        public Theme theme;

        List<(Point, string)> lines = new List<(Point, string)>();
        public void ClearLines() => lines.Clear();
        public void Clear() {
            ClearLines();
            console.Clear();
            console.Fill(theme.front, theme.back, ' ');
            /*
            lines.ForEach(t => {
                (Point cursor, string s) = t;
                Global.SetCursor(cursor);
                Global.Print(new string(' ', s.Length), front, back);
            });
            */
        }
        public void SetCursor(Point p) => cursor.Move(p);
        public void ResetCursor() {
            SetCursor(margin);
        }
        public void WriteLine(ColoredString s) {
            cursor.Print(s);
            NextLine();
        }
        public void Write(ColoredString s) {
            cursor.Print(s);
        }
        public void Write(string s, Color? front = null, Color? back = null) {
            cursor.Print(new ColoredString(s, front ?? theme.front, back ?? theme.back));
        }
        public void Write(char c, Color? front = null, Color? back = null) {
            Write(c.ToString(), front, back);
        }
        public void WriteInvert(char c, Color? front = null, Color? back = null) {
            cursor.Print(new ColoredString(c.ToString(), back ?? theme.back, front ?? theme.front));
        }
        public void WriteHighlight(string s, Color? front = null, Color? back = null) {
            cursor.Print(CreateString(s, front, back));
        }
        public void WriteLineHighlight(string s, Color? front = null, Color? back = null) {
            cursor.Print(CreateHighlightString(s));
            NextLine();
        }
        public ColoredString ColorString(string s, Color front, Color back) {
            return new ColoredString(s.Select(c => new ColoredGlyph(c, front, back)).ToArray());
        }
        public ColoredString CreateString(string s, Color? front = null, Color? back = null) {
            return ColorString(s, front ?? theme.front, back ?? theme.back);
        }
        public ColoredString CreateHighlightString(string s, Color? front = null, Color? back = null) {
            return ColorString(s, front ?? theme.highlight, back ?? theme.back);
        }
        public void WriteLine(string s, Color? front = null, Color? back = null) {
            Write(s, front, back);
            NextLine();
        }
        public void ResetLine() {
            cursor.Column = margin.X;
        }
        public void NextLine() {
            cursor.Row++;
            ResetLine();
        }
        public void Draw(HighlightEntry h) {
            var c = theme.highlight;
            int highlightStart = h.highlightStart;
            int highlightLength = h.highlightLength;
            string str = h.str;

            (var front, var back) = theme;
            if (highlightStart != -1) {
                Write(str.Substring(0, highlightStart), front, back);
                if (highlightLength != 0) {
                    Write(str.Substring(highlightStart, highlightLength), c, back);
                    Write(str.Substring(highlightStart + highlightLength), front, back);
                } else {
                    Write(str.Substring(highlightStart), front, back);
                }
            } else {
                Write(str, front, back);
            }
        }
        public void DrawSelected(HighlightEntry h) {
            var c = theme.highlight;
            int highlightStart = h.highlightStart;
            int highlightLength = h.highlightLength;

            (var front, var back) = theme;
            string str = h.str;
            if (highlightStart != -1) {
                Write(str.Substring(0, highlightStart), back, front);
                if (highlightLength != 0) {
                    Write(str.Substring(highlightStart, highlightLength), c, front);
                    Write(str.Substring(highlightStart + highlightLength), back, front);
                } else {
                    Write(str.Substring(highlightStart), back, front);
                }
            } else {
                Write(str, front, back);
            }
        }
    }

}
