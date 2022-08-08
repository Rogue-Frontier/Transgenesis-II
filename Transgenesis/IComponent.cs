using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace Transgenesis {
    interface IScreen : IComponent {
        string name { get; }
    }
    interface IComponent {
        void Update();
        void Handle(ConsoleKeyInfo k);
        void Handle(MouseScreenObjectState mouse) { }
        void Draw();
    }
    class Input {
        //To do: Flip Suggest area so that input is below to save space
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
        public Point pos = new Point(0, 44);
        public int height = 1;
        public void Clear() {
            s.Clear();
            cursor = 0;
        }
        public void Handle(ConsoleKeyInfo k) {
            //Global.Break();
            bool ctrl = (k.Modifiers & ConsoleModifiers.Control) != 0;
            switch (k.Key) {
                case ConsoleKey.LeftArrow:
                    LeftArrow:
                    if (cursor > 0) {
                        cursor--;
                        if(ctrl && s[cursor] != ' ') {
                            goto LeftArrow;
                        }
                    }
                    break;
                case ConsoleKey.RightArrow:
                    RightArrow:
                    if(cursor < s.Length - 1 && ctrl && s[cursor+1] != ' ') {
                        cursor++;
                        goto RightArrow;
                    } else if (cursor < s.Length) {
                        cursor++;
                    }
                    break;
                case ConsoleKey.Backspace:
                    //Global.Break();
                    if (ctrl) {
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
                    //Don't type if we are doing a keyboard shortcut
                    if(ctrl) {
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
            //c.SetCursor(pos);
            c.NextLine();
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
    class Suggest {
        Input i;
        public int currentEntry = -1;
        public List<HighlightEntry> items;
        public int replaceStart = 0, replaceLength = -1;

        public Point pos = new Point(0, 45);
        public int height = 8;
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
        public void SetItems(List<HighlightEntry> items, int replaceStart = 0, int replaceLength = -1) {
            this.items = items;
            currentEntry = Math.Min(currentEntry, items.Count - 1);
            if (currentEntry == -1 && items.Count > 0) {
                currentEntry = 0;
            }
            SetReplace(replaceStart, replaceLength);
        }
        public void SetReplace(int replaceStart, int replaceLength = -1) {
            this.replaceStart = replaceStart;
            this.replaceLength = replaceLength;
        }
        public void Clear() {
            items.Clear();
            currentEntry = -1;
        }
        public void Handle(ConsoleKeyInfo k) {
            switch (k.Key) {
                case ConsoleKey.UpArrow when (k.Modifiers & ConsoleModifiers.Shift) == 0:
                    if (currentEntry > -1)
                        currentEntry--;
                    else
                        //Wrap around
                        currentEntry = items.Count - 1;
                    break;
                case ConsoleKey.DownArrow when (k.Modifiers & ConsoleModifiers.Shift) == 0:
                    if (currentEntry + 1 < items.Count)
                        currentEntry++;
                    else
                        //Wrap around
                        currentEntry = -1;
                    break;
                case ConsoleKey.Tab: {
                        if (currentEntry == -1)
                            break;
                        var item = items[currentEntry];
                        if (item.highlightLength == item.str.Length) {
                            break;
                        }

                        string input = i.Text;
                        string itemStr = item.str;
                        //i.Text = input.Substring(0, input.Length - item.highlightLength - 1) + itemStr;
                        i.Text = $"{input.Substring(0, replaceStart)}{itemStr}{(replaceLength == -1 ? "" : input.Substring(replaceStart + replaceLength))}";
                        Clear();
                        break;
                    }

                case ConsoleKey.Spacebar when (k.Modifiers & ConsoleModifiers.Shift) == 0: {
                        //If we're holding shift down, then do not insert the entry

                        /*
                        if (index != -1) {
                            i.Text = options[index].str + " ";
                            i.cursor = i.Text.Length;
                            Clear();
                        }
                        */
                        if (currentEntry == -1)
                            break;
                        var item = items[currentEntry];
                        if (item.highlightLength == item.str.Length) {
                            break;
                        }

                        string input = i.Text;
                        string itemStr = item.str;
                        //i.Text = input.Substring(0, input.Length - item.highlightLength - 1) + itemStr;
                        i.Text = $"{input.Substring(0, replaceStart)}{itemStr}{(replaceLength == -1 ? "" : input.Substring(replaceStart + replaceLength))}";
                        Clear();
                        break;
                    }
                case ConsoleKey.Enter:
                    Clear();
                    break;
            }
        }
        public void Draw() {
            //c.SetCursor(pos);
            c.NextLine();
            c.margin = new Point(c.margin.X, c.cursor.Row);
            //int columnHeight = 8;

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


            int lastColumn = currentEntry / 8;
            int width = c.width - c.margin.X;
            int widthLeft = width;
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

            for (columnIndex = lastColumn - columnCount + 1; columnIndex <= lastColumn; columnIndex++) {

                for(int i = columnIndex * 8; i < (columnIndex + 1) * 8 && i < items.Count; i++) {
                    var o = items[i];
                    if (currentEntry == i) {
                        c.DrawSelected(o);
                    } else {
                        c.Draw(o);
                    }

                    //We're already on the next line if this line is the full width
                    c.NextLine();
                }

                c.margin += new Point(columnSizes[columnIndex], 0);

                c.ResetCursor();
            }
            //Reset the margin after we're done printing
            c.margin = pos;
            //ClearBelow(pos.Y + 6);
        }
    }

    class Tooltip {
        Input i;
        Suggest s;
        public Dictionary<string, string> help;
        Point pos = new Point(0, 53);
        ConsoleManager c;
        public string text;
        public ColoredString warning;
        public Tooltip(Input i, Suggest s, ConsoleManager c, Dictionary<string, string> help) {
            this.i = i;
            this.s = s;
            this.c = c;
            this.help = help;
        }
        public void Draw() {
            //Note that if the input already has a match, then highlighting another option in the Suggest menu will not do anything
            c.SetCursor(pos);
            if (text != null) {
                c.Write(text);
                c.NextLine();
            }
            if (warning != null) {
                c.Write(warning);
            }
        }
        public void Handle(ConsoleKeyInfo k) {
            string str;
            if (s.currentEntry > -1 && help.TryGetValue(s.items[s.currentEntry].str, out str)
                || help.TryGetValue(i.Text.TrimStart().Split()[0], out str)) {
                text = str;
            }
        }
    }
    class History {
        Input i;
        ConsoleManager c;
        public List<string> items = new List<string>();
        int index = -1;
        public int height = 10;
        public History(Input i, ConsoleManager c) {
            this.i = i;
            this.c = c;
        }
        public void Record() {
            if(i.Text.Length == 0) {
                return;
            }
            items.Remove(i.Text);
            items.Add(i.Text);
            i.Clear();
        }

        public void Handle(ConsoleKeyInfo k) {
            switch(k.Key) {
                case ConsoleKey.UpArrow when (k.Modifiers & ConsoleModifiers.Shift) != 0:
                    if(index == -1) {
                        index = items.Count - 1;
                    } else {
                        index--;
                    }
                    if(index != -1) {
                        i.Text = items[index];
                    } else {
                        i.Clear();
                    }
                    break;
                case ConsoleKey.DownArrow when (k.Modifiers & ConsoleModifiers.Shift) != 0:
                    if (index >= items.Count - 1) {
                        index = -1;
                    } else {
                        index++;
                    }
                    if (index != -1) {
                        i.Text = items[index];
                    } else {
                        i.Clear();
                    }
                    break;
                case ConsoleKey.Enter:
                    index = -1;
                    break;
            }
        }
        public void Draw() {
            //c.reverse = true;
            int start = Math.Max(0, (index != -1 ? (index + 1) : items.Count) - height);
            int end = Math.Min(start + height, items.Count);

            Point pos = new Point(120, c.console.Height - 3 - Math.Min(height, end - start));
            c.margin = pos;
            c.SetCursor(pos);
            c.NextLine();
            for (int i = start; i < end; i++) {

                if(i == index) {
                    c.WriteLineInvert(items[i]);
/*
 * Given a grammar, what are the transitions we're going to have for this PDA? 2 kinds
 * Defined for every variable of the grammar and every production of the variable
 * Production A to Beta is a transition to replace A with Beta. Epsilon transition.
 * For every production in the grammar, we have an epsilon transition
 * 
 * For every terminal of the grammar, we have a non-epsilon transition to consume it
 * 
 * */
                } else {
                    c.WriteLine(items[i]);
                }
            }

            //c.reverse = false;
        }
    }
}
