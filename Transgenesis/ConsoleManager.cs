using System;
using System.Collections.Generic;
using System.Drawing;
using static Transgenesis.Global;
using Console = Colorful.Console;

namespace Transgenesis {
    class Theme {
        public Color front = Color.White, back = Color.Black;
        public Color highlight = Color.LimeGreen;
        public void Deconstruct(out Color front, out Color back) {
            (front, back) = (this.front, this.back);
        }
    }
    class ConsoleManager {
        public ConsoleManager(Point p) {
            this.margin = p;
            theme = new Theme();
        }

        public Point margin;
        public Theme theme;

        List<(Point, string)> lines = new List<(Point, string)>();
        public Point GetCursorPosition() => new Point(Console.CursorLeft, Console.CursorTop);
        public void ClearLines() => lines.Clear();
        public void Clear() {
            ClearLines();
            Console.Clear();
            /*
            lines.ForEach(t => {
                (Point cursor, string s) = t;
                Global.SetCursor(cursor);
                Global.Print(new string(' ', s.Length), front, back);
            });
            */
        }
        public void SetCursor(Point p) => Global.SetCursor(p);
        public void ResetCursor() {
            SetCursor(margin);
        }
        public void Write(string s, Color? front = null, Color? back = null) {
            (Console.ForegroundColor, Console.BackgroundColor) = (front ?? theme.front, back ?? theme.back);
            lines.Add((GetCursorPosition(), s));
            Console.Write(s);
        }
        public void Write(char c, Color? front = null, Color? back = null) {
            (Console.ForegroundColor, Console.BackgroundColor) = (front ?? theme.front, back ?? theme.back);
            lines.Add((GetCursorPosition(), c.ToString()));
            Console.Write(c);
        }
        public void WriteInvert(char c, Color? front = null, Color? back = null) {
            (Console.BackgroundColor, Console.ForegroundColor) = (front ?? theme.front, back ?? theme.back);
            lines.Add((GetCursorPosition(), c.ToString()));
            Console.Write(c);
        }
        public void WriteHighlight(string s, Color? front = null, Color? back = null) {
            (Console.ForegroundColor, Console.BackgroundColor) = (front ?? theme.highlight, back ?? theme.back);
            lines.Add((GetCursorPosition(), s));
            Console.Write(s);
        }
        public void WriteLineHighlight(string s, Color? front = null, Color? back = null) {
            (Console.ForegroundColor, Console.BackgroundColor) = (front ?? theme.highlight, back ?? theme.back);
            lines.Add((GetCursorPosition(), s));
            Console.WriteLine(s);
        }
        public void WriteLine(string s, Color? front = null, Color? back = null) {
            Write(s, front, back);
            NextLine();
        }
        public void ResetLine() {
            Console.SetCursorPosition(margin.X, Console.CursorTop);
        }
        public void NextLine() {
            Console.CursorTop++;
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
