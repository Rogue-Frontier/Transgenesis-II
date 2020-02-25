using Microsoft.Xna.Framework;
using SadConsole;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transgenesis {
    class TextEditor : IComponent {
        Stack<IComponent> screens;
        ConsoleManager c;
        Scroller scroller;
        StringBuilder s;
        Point pos;
        int cursor;
        Action<string> OnClosed;
        public string Text => s.ToString();

        public TextEditor(Stack<IComponent> screens, ConsoleManager c, Action<string> OnClosed) {
            this.screens = screens;
            this.c = c;
            this.scroller = new Scroller(c);
            this.s = new StringBuilder();
            this.cursor = 0;
            this.pos = new Point(0, 0);
            this.OnClosed = OnClosed;
        }
        public void Update() {

        }
        public void Handle(ConsoleKeyInfo k) {
            //Global.Break();
            bool ctrl = (k.Modifiers & ConsoleModifiers.Control) != 0;
            switch (k.Key) {
                case ConsoleKey.LeftArrow:
                LeftArrow:
                    if (cursor > 0) {
                        cursor--;
                        if (ctrl && s[cursor] != ' ') {
                            goto LeftArrow;
                        }
                    }
                    break;
                case ConsoleKey.RightArrow:
                RightArrow:
                    if (cursor < s.Length - 1 && ctrl && s[cursor + 1] != ' ') {
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
                    if (cursor == s.Length) {
                        s.Append('\n');
                        for (int i = cursor - 1; i > -1; i--) {
                            if(s[i] == ' ') {
                                s.Append(' ');
                                cursor++;
                            } else if(s[i] == '\n') {
                                break;
                            }
                        }
                        cursor++;
                    } else {
                        s.Insert(cursor, '\n');
                        for (int i = cursor - 1; i > -1; i--) {
                            if (s[i] == ' ') {
                                cursor++;
                                s.Insert(cursor, ' ');
                            } else if (s[i] == '\n') {
                                break;
                            }
                        }
                        cursor++;
                    }
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
                    }
                    break;
            }
        }
        public void Draw() {
            c.SetCursor(pos);
            List<ColoredString> buffer = new List<ColoredString>();

            int width = c.width;
            ColoredString line = new ColoredString(width);
            int index = 0;
            int length = 0;
            bool cursorAfterNewline = false;
            foreach(var ch in s.ToString()) {
                if (ch == '\n') {
                    buffer.Add(line.SubString(0, length));
                    line = new ColoredString(width);
                    length = 0;
                    if(cursor == length) {
                        cursorAfterNewline = true;
                    }
                    index++;
                    continue;
                }
                if(index == cursor || cursorAfterNewline) {
                    cursorAfterNewline = false;
                    line[length] = c.CreateCharInvert(ch);
                } else {
                    line[length] = c.CreateChar(ch);
                }
                index++;
                length++;
                if (length == width) {
                    buffer.Add(line);
                    line = new ColoredString(width);
                    length = 0;
                }
            }
            if (length > 0) {
                buffer.Add(line.SubString(0, length));
            }
            if (cursorAfterNewline) {
                buffer.Add(new ColoredString(c.CreateCharInvert(' ')));
            } else if(index == cursor) {
                if(length == 0) {
                    buffer.Add(new ColoredString(c.CreateCharInvert(' ')));
                } else {
                    buffer[buffer.Count - 1] += new ColoredString(c.CreateCharInvert(' '));
                }
            } 

            scroller.Draw(buffer);
        }
    }
}
