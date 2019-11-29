using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Transgenesis {
    public static class Global {
        public static void SetCursor(int? left, int? top) {
            (Console.CursorLeft, Console.CursorTop) = (left ?? Console.CursorLeft, top ?? Console.CursorTop);
        }
        public static void SetCursor(Point p) {
            (Console.CursorLeft, Console.CursorTop) = (p.X, p.Y);
        }
        public static Point GetCursor() {
            return new Point(Console.CursorLeft, Console.CursorTop);
        }
        public static void PrintLine(object s, ConsoleColor? cf = null, ConsoleColor? cb = null) {
            //var f = (Console.ForegroundColor, Console.BackgroundColor);
            (Console.ForegroundColor, Console.BackgroundColor) = (cf ?? Console.ForegroundColor, cb ?? Console.BackgroundColor);
            Console.WriteLine(s);
            //(Console.ForegroundColor, Console.BackgroundColor) = f;
        }
        public static void ClearAhead() {
            Print(new string(' ', Console.WindowWidth - Console.CursorLeft), ConsoleColor.White, ConsoleColor.Black);
        }
        public static XElement NameElement(this XElement e, string name) {
            return e.Elements("E").First(s => s.Attribute("name")?.Value == name);
        }
        public static bool NameElement(this XElement e, string name, out XElement result) {
            return (result = e.Elements("E").FirstOrDefault(s => s.Attribute("name")?.Value == name)) != null;
        }
        public static string Att(this XElement e, string attribute) {
            return e.Attribute(attribute)?.Value;
        }
        public static void ClearBelow(int row) {
            while (Console.CursorTop < row) {
                ClearAhead();
                Console.WriteLine();
            }
        }
        public static void Print(object s, ConsoleColor? cf = null, ConsoleColor? cb = null) {
            //var f = (Console.ForegroundColor, Console.BackgroundColor);
            (Console.ForegroundColor, Console.BackgroundColor) = (cf ?? Console.ForegroundColor, cb ?? Console.BackgroundColor);
            Console.Write(s);
            //(Console.ForegroundColor, Console.BackgroundColor) = f;
        }
        public static bool Att(this XElement e, string attrib, out string result) {
            return (result = (string)e.Attribute("inherit")) != null;
        }

        public static List<HighlightEntry> GetSuggestions(string input, IEnumerable<string> items) {
            var startsWith = new List<HighlightEntry>();
            var contains = new List<HighlightEntry>();
            foreach (var s in items) {
                if (s.StartsWith(input)) {
                    startsWith.Add(new HighlightEntry() {
                        str = s,
                        highlightStart = 0,
                        highlightLength = input.Length
                    });
                } else {
                    int index = s.IndexOf(input);
                    if (index != -1) {
                        contains.Add(new HighlightEntry() {
                            str = s,
                            highlightStart = index,
                            highlightLength = input.Length
                        }); ;
                    }
                }
            }
            var result = new List<HighlightEntry>();
            result.AddRange(startsWith);
            result.AddRange(contains);
            return result;
        }
    }
}
