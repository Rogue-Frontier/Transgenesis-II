using SadConsole;
using SadRogue.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SadConsole.ColoredString;

namespace Transgenesis {
    class TextEditor : IComponent {
        Stack<IComponent> screens;
        ConsoleManager c;
        Scroller scroller;
        StringBuilder s;
        Point pos;
        int cursor;
        int columnMemory;
        Action<string> OnClosed;
        public string Text => s.ToString();

        public TextEditor(Stack<IComponent> screens, ConsoleManager c, string Text = "", Action<string> OnClosed = null) {
            this.screens = screens;
            this.c = c;
            this.scroller = new(c);
            this.s = new(Text);
            this.cursor = 0;
            this.columnMemory = 0;
            this.pos = new(0, 0);
            this.OnClosed = OnClosed;
        }
        public void Update() {

        }
        public void Handle(ConsoleKeyInfo k) {
            //Global.Break();
            bool ctrl = (k.Modifiers & ConsoleModifiers.Control) != 0;
            switch (k.Key) {
                case ConsoleKey.Escape: {
                        OnClosed?.Invoke(s.ToString());
                        screens.Pop();
                        break;
                    }
                case ConsoleKey.Home: {
                    Home:
                        if (FindPrevLine(out int index)) {
                            cursor = index;
                        } else {
                            cursor = 0;
                        }
                        columnMemory = CountColumn();
                        break;
                    }
                case ConsoleKey.End: {
                        if (FindNextLine(out int index)) {
                            cursor = index;
                        } else {
                            cursor = 0;
                        }
                        columnMemory = CountColumn();
                        break;
                    }
                case ConsoleKey.LeftArrow: {
                    LeftArrow:
                        if (cursor > 0) {
                            cursor--;
                            if (ctrl && s[cursor] != ' ') {
                                goto LeftArrow;
                            }
                        }
                        columnMemory = CountColumn();
                        break;
                    }
                case ConsoleKey.RightArrow: {
                    RightArrow:
                        if (cursor < s.Length - 1 && ctrl && s[cursor + 1] != ' ') {
                            cursor++;
                            goto RightArrow;
                        } else if (cursor < s.Length) {
                            cursor++;
                        }
                        columnMemory = CountColumn();
                        break;
                    }
                case ConsoleKey.UpArrow: {
                        if (FindPrevLine(out int index)) {
                            int column = Math.Min(columnMemory, CountLineLength(index));
                            cursor = index + column;
                        } else {
                            cursor = 0;
                        }
                        break;
                    }
                case ConsoleKey.DownArrow: {
                        if (FindNextLine(out int index)) {
                            int column = Math.Min(columnMemory, CountLineLength(index));
                            cursor = index + column;
                        } else {
                            cursor = s.Length;
                        }
                        break;
                    }
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
                    columnMemory = CountColumn();
                    break;
                case ConsoleKey.Enter:
                    if (cursor == s.Length) {
                        int indent = CountIndent();
                        s.Append("\n" + new string(' ', indent));
                        cursor++;
                        cursor += indent;
                    } else {
                        int indent = CountIndent();
                        s.Insert(cursor, "\n" + new string(' ', indent));
                        cursor++;
                        cursor += indent;
                    }
                    columnMemory = CountColumn();
                    break;
                case ConsoleKey.Tab:
                    if (ctrl) {
                        break;
                    }
                    if (cursor == s.Length) {
                        s.Append("    ");
                    } else {
                        s.Insert(cursor, "    ");
                    }
                    cursor += 4;
                    columnMemory = CountColumn();
                    break;
                default:
                    //Don't type if we are doing a keyboard shortcut
                    if (ctrl) {
                        break;
                    }

                    if (k.KeyChar != 0) {
                        if (cursor == s.Length) {
                            s.Append(k.KeyChar);
                        } else {
                            s.Insert(cursor, k.KeyChar);
                        }
                        cursor++;
                        columnMemory = CountColumn();
                    }
                    break;
            }
        }
        int CountColumn() {
            int count = 0;
            int index = cursor - 1;
            while (index > -1 && s[index] != '\n') {
                index--;
                count++;
            }
            return count;
        }
        int CountIndent() {
            int index = cursor - 1;
            int indent = 0;
            while (index > -1) {
                switch (s[index]) {
                    case ' ':
                        indent++;
                        break;
                    case '\n':
                        return indent;
                    default:
                        indent = 0;
                        break;
                }
                index--;
            }
            return indent;
        }
        bool FindPrevLine(out int index) {
            index = Math.Min(cursor, s.Length - 1);
            if (index > -1) {
                index--;
            }
            while(index > -1 && s[index] != '\n') {
                index--;
            }
            if(index == -1) {
                return false;
            }
            index--;

            while(index > -1 && s[index] != '\n') {
                index--;
            }
            index++;
            return true;
        }
        bool FindNextLine(out int index) {
            if (s.Length == 0) {
                index = 0;
                return false;
            }
            index = Math.Min(cursor, s.Length - 1);
            while(index < s.Length && s[index] != '\n') {
                index++;
            }
            if(index != s.Length) {
                //Return index of first char of line
                index++;
                return true;
            } else {
                return false;
            }
        }
        int CountLineLength(int index) {
            int count = 0;
            while(index < s.Length && s[index] != '\n') {
                index++;
                count++;
            }
            return count;
        }
        public void Draw() {
            c.Clear();

            c.SetCursor(pos);
            var buffer = new List<ColoredString>();

            int width = c.width;
            var line = new ColoredString(width);
            int index = 0;
            int length = 0;
            bool cursorAfterNewline = false;
            foreach(var ch in s.ToString()) {
                if (ch == '\n') {
                    buffer.Add(line.SubString(0, length));
                    line = new(width);
                    length = 0;
                    if(cursor == index) {
                        buffer[buffer.Count - 1] += new ColoredString(c.ColorInvert(' '));
                        //cursorAfterNewline = true;
                    }
                    index++;
                    continue;
                }
                if(index == cursor || cursorAfterNewline) {
                    cursorAfterNewline = false;
                    line[length] = ConsoleManager.Effect(c.ColorInvert(ch));
                } else {
                    var cg = c.Color(ch);
                    var ce = ConsoleManager.Effect(cg);
                    line[length] = ce;
                }
                index++;
                length++;
                if (length == width) {
                    buffer.Add(line);
                    line = new(width);
                    length = 0;
                }
            }
            if (length > 0) {
                buffer.Add(line.SubString(0, length));
            }
            if (cursorAfterNewline) {
                buffer.Add(new(c.ColorInvert(' ')));
            } else if(index == cursor) {
                if(length == 0) {
                    buffer.Add(new(c.ColorInvert(' ')));
                } else {
                    buffer[buffer.Count - 1] += new ColoredString(c.ColorInvert(' '));
                }
            }
            scroller.Draw(buffer, 64);
        }
    }
}
