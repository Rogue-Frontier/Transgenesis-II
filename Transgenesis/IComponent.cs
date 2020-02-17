using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Text;
namespace Transgenesis {
    interface IComponent {
        void Update();
        void Handle(ConsoleKeyInfo k);
        void Draw();
    }
    class Input : IComponent {
        ConsoleManager c;
        private StringBuilder s = new StringBuilder();
        public int cursor = 0;
        public string Text {
            get => s.ToString();
            set {
                s.Clear();
                s.Append(value);
                cursor = s.Length;
            }
        }
        public Input(ConsoleManager c) {
            this.c = c;
        }
        public Point pos = new Point(0, 24);
        public void Clear() {
            s.Clear();
            cursor = 0;
        }
        public void Update() {
        }
        public void Handle(ConsoleKeyInfo k) {
            //Global.Break();
            switch (k.Key) {
                case ConsoleKey.LeftArrow when (k.Modifiers & ConsoleModifiers.Control) == 0:
                    if (cursor > 0) {
                        cursor--;
                    }
                    break;
                case ConsoleKey.RightArrow when (k.Modifiers & ConsoleModifiers.Control) == 0:
                    if (cursor < s.Length) {
                        cursor++;
                    }
                    break;
                case ConsoleKey.Backspace:
                    //Global.Break();
                    if ((k.Modifiers & ConsoleModifiers.Control) != 0) {
                        //Make sure we have characters to delete
                        if (cursor == 0) {
                            break;
                        }
                        //If we are at a space, just delete it
                        if (s[cursor - 1] == ' ') {
                            cursor--;
                            s.Remove(cursor, 1);
                        } else {
                            //Otherwise, delete characters until we reach a space
                            int length = 0;
                            while (cursor > 0 && s[cursor - 1] != ' ') {
                                cursor--;
                                length++;
                            }
                            s.Remove(cursor, length);
                        }
                    } else {
                        if (s.Length > 0 && cursor > 0) {
                            cursor--;
                            s.Remove(cursor, 1);
                        }
                    }
                    break;
                case ConsoleKey.Enter:

                    break;
                default:
                    if (k.KeyChar != 0) {
                        if (cursor == s.Length) {
                            s.Append(k.KeyChar);
                        } else {
                            s.Insert(cursor, k.KeyChar);
                        }
                        cursor++;
                    }
                    break;
            }
        }
        public void Draw() {
            c.SetCursor(pos);
            if (cursor == s.Length) {
                c.Write(Text);
                c.WriteInvert(' ');
            } else {
                string text = Text;
                for (int i = 0; i < text.Length; i++) {
                    if (i == cursor) {
                        c.WriteInvert(text[i]);
                    } else {
                        c.Write(text[i]);
                    }
                }
            }
            /*
            for(int i = s.Length + 1; i < Console.WindowWidth; i++) {
                Print(' ', ConsoleColor.White, ConsoleColor.Black);
            }
            */
        }
    }
    class Suggest : IComponent {
        Input i;
        public int index = -1;
        public List<HighlightEntry> items;
        public Point pos = new Point(0, 25);
        ConsoleManager c;
        public Suggest(Input i, ConsoleManager c) {
            this.i = i;
            items = new List<HighlightEntry>();
            this.c = c;
        }
        public Suggest(Input i, List<HighlightEntry> options, ConsoleManager c) {
            this.i = i;
            this.items = options;

            this.c = c;
        }
        public void SetItems(List<HighlightEntry> items) {
            this.items = items;
            index = Math.Min(index, items.Count - 1);
            if (index == -1 && items.Count > 0) {
                index = 0;
            }
        }
        public void Clear() {
            items.Clear();
            index = -1;
        }
        public void Update() {

        }
        public void Handle(ConsoleKeyInfo k) {
            switch (k.Key) {
                case ConsoleKey.UpArrow:
                    if (index > -1)
                        index--;
                    else
                        //Wrap around
                        index = items.Count - 1;
                    break;
                case ConsoleKey.DownArrow:
                    if (index + 1 < items.Count)
                        index++;
                    else
                        //Wrap around
                        index = -1;
                    break;
                case ConsoleKey.Spacebar:
                    /*
                    if (index != -1) {
                        i.Text = options[index].str + " ";
                        i.cursor = i.Text.Length;
                        Clear();
                    }
                    */
                    if (index == -1)
                        break;
                    var item = items[index];
                    if (item.highlightLength == item.str.Length) {
                        break;
                    }

                    string input = i.Text;
                    string itemStr = item.str;
                    i.Text = input.Substring(0, input.Length - item.highlightLength - 1) + itemStr;
                    Clear();
                    break;
            }
        }
        public void Draw() {
            int columns = c.width;
            int column = 0;
            c.SetCursor(pos);
            for (int i = 0; i < items.Count; i++) {
                var o = items[i];
                if (index == i) {
                    c.DrawSelected(o);
                } else {
                    c.Draw(o);
                }
                c.NextLine();

                //Begin a new column of items
                if (i % 16 == Math.Max(15, 1 + items.Count / columns)) {
                    //if(c.margin.X + 32 >= Console.WindowWidth) {
                    if (column >= columns) {
                        continue;
                    }
                    column++;
                    c.margin.X += 32;
                    c.ResetCursor();
                }

                //ClearAhead();
                //Console.WriteLine();
            }
            //Reset the margin after we're done printing
            c.margin = pos;
            //ClearBelow(pos.Y + 6);
        }
    }

    class Tooltip : IComponent {
        Input i;
        Suggest s;
        Dictionary<string, string> help;
        Point pos = new Point(0, 42);
        ConsoleManager c;
        public Tooltip(Input i, Suggest s, ConsoleManager c, Dictionary<string, string> help) {
            this.i = i;
            this.s = s;
            this.c = c;
            this.help = help;
        }
        public void Draw() {
            //Note that if the input already has a match, then highlighting another option in the Suggest menu will not do anything
            if (help.TryGetValue(i.Text.TrimStart().Split()[0], out string helptext)) {
                c.SetCursor(pos);
                c.Write(helptext);
            } else if (s.index > -1 && help.TryGetValue(s.items[s.index].str, out helptext)) {
                c.SetCursor(pos);
                c.Write(helptext);
            }
        }
        public void Handle(ConsoleKeyInfo k) {
        }
        public void Update() {
        }
    }
    class History : IComponent {
        Input i;
        int index = -1;
        public List<string> items;
        public void Update() {
        }

        public void Handle(ConsoleKeyInfo k) {
        }

        public void Draw() {
        }
    }
}
