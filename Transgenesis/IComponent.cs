using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public Point pos = new Point(0, 47);
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
                    //Don't type of we are doing a keyboard shortcut
                    if((k.Modifiers & ConsoleModifiers.Control) != 0) {
                        break;
                    }

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
        public Point pos = new Point(0, 48);
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
                case ConsoleKey.Spacebar when (k.Modifiers & ConsoleModifiers.Shift) == 0:
                    //If we're holding shift down, then do not insert the entry

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
            c.SetCursor(pos);
            c.margin = pos;
            int columnHeight = 8;

            if(items.Count == 0) {
                return;
            }

            //One column per 8 items
            var totalColumns = (items.Count - 1) / 8 + 1;
            int[] columnSizes = new int[totalColumns];

            //Find the largest width of each group of 8 items
            int columnIndex;
            for(columnIndex = 0; columnIndex < columnSizes.Length; columnIndex++) {
                int index = columnIndex * 8;
                int itemCount = Math.Max(0, Math.Min(8, items.Count - index));
                int columnSize = items.GetRange(index, itemCount).Select(i => i.str.Length).Max();
                columnSize = ((columnSize) / 4) * 4 + 4;
                columnSizes[index / 8] = columnSize;
            }


            int lastColumn = index / 8;
            int widthLeft = c.width;
            int columnCount = 0;
            columnIndex = lastColumn;

            //Count the columns we can show preceding the selected column
            CountColumnsBefore:
            widthLeft -= columnSizes[columnIndex];
            if(widthLeft > -1) {
                //If we still had width left to show this column
                columnCount++;
                if(columnIndex > 0) {
                    columnIndex--;
                    goto CountColumnsBefore;
                }
            }

            //If the column we selected is not the very last column
            columnIndex = lastColumn + 1;
            if (columnIndex < totalColumns) {
                //Count the columns after the selected column
                CountColumnsAfter:
                widthLeft -= columnSizes[columnIndex];
                if (widthLeft > -1) {
                    lastColumn++;
                    columnCount++;
                    //If we still had width left to show this column
                    if (columnIndex < totalColumns - 1) {
                        //And we still have columns left to show (we have to make sure index won't be out of range when we goto
                        columnIndex++;
                        goto CountColumnsAfter;
                    }
                }
            }

            int width = c.width;
            for (columnIndex = lastColumn - columnCount + 1; columnIndex <= lastColumn; columnIndex++) {

                for(int i = columnIndex * 8; i < (columnIndex + 1) * 8 && i < items.Count; i++) {
                    var o = items[i];
                    if (index == i) {
                        c.DrawSelected(o);
                    } else {
                        c.Draw(o);
                    }

                    //We're already on the next line if this line is the full width
                    c.NextLine();
                }

                c.margin.X += columnSizes[columnIndex];
                c.ResetCursor();
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
        Point pos = new Point(0, 56);
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
            //Use Up/Down arrows with Shift held down
        }

        public void Draw() {
        }
    }
}
