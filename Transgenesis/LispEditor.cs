using SadConsole;
using SadRogue.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SadConsole.ColoredString;

namespace Transgenesis {

    interface LispNode {
        public LispExpression parent { get; }
        int length { get; }

        public int index => parent.items.IndexOf(this);
        public void Replace(LispNode other) {
            parent.items[index] = other;
        }
    }
    class LispNil : LispNode {
        public LispExpression parent { get; set; }
        public int length => 0;
        public int index => parent.items.IndexOf(this);
        public override string ToString() => "Nil";
    }
    class LispExpression : LispNode {
        public LispExpression parent { get; set; }
        public int length => items.Count;
        public List<LispNode> items = new();
        public int index => parent.items.IndexOf(this);

        public override string ToString() => $"({string.Join(' ', items.Select(i => i?.ToString()??"Nil"))})";
        public ColoredString ToColored(LispNode selected, int index) {
            
            Func<string, string> color = s => s;
            var items = this.items.Select(i => i?.ToString() ?? "Nil");
            if (this == (selected is LispString s ? s.parent : selected)) {
                color = s => $"[c:r f:yellow]{s}[c:u]";

            }
            string content = string.Join(' ', items);
            return Parser.Parse($"{color("(")}{content}{color(")")}");
        }
    }
    class LispString : LispNode {
        public LispExpression parent { get; set; }
        public string value;
        public int length => value.Length;
        public int index => parent.items.IndexOf(this);
        public override string ToString() => value;
    }
    class LispEditor : IComponent {
        Stack<IComponent> screens;
        ConsoleManager c;
        Scroller scroller;
        Point pos = (0, 0);
        Action<string> OnClosed;

        LispExpression root = new();
        (LispNode node, int index) cursor;

        public LispEditor(Stack<IComponent> screens, ConsoleManager c, string Text = "", Action<string> OnClosed = null) {
            this.screens = screens;
            this.c = c;
            this.scroller = new(c);
            this.OnClosed = OnClosed;
            cursor = (root, 0);

        }
        public void UpdateCursor(bool forward = true) {

            var original = cursor.node;
            Start:
            if(cursor.index == -1) {
                var parent = cursor.node.parent;
                if(parent == null) {
                    cursor = (original, 0);
                    return;
                }
                var index = cursor.node.index;
                cursor = (parent, index - 1);
                forward = false;
                goto Start;
            }
            if((cursor.node is LispString ls && cursor.index > ls.length) || (cursor.node is LispExpression exp && cursor.index >= exp.length)) {
                var parent = cursor.node.parent;
                if(parent == null) {
                    cursor = (original, parent is LispString ? original.length : original.length - 1);
                    return;
                }
                var index = cursor.node.index;
                cursor = (parent, index + 1);
                forward = true;
                goto Start;
            }
            if (cursor.node is LispExpression exp2) {
                var n = exp2.items[cursor.index];
                if (n != null) {
                    cursor = (n, forward ? 0 : n is LispString ? n.length : n.length - 1);
                    goto Start;
                }
            }
        }
        public void Update() {

        }
        public void Handle(ConsoleKeyInfo k) {
            //Global.Break();
            bool ctrl = (k.Modifiers & ConsoleModifiers.Control) != 0;
            switch (k.Key) {
                case ConsoleKey.Escape: {
                        OnClosed?.Invoke(root.ToString());
                        screens.Pop();
                        break;
                    }
                case ConsoleKey.LeftArrow: {
                        cursor.index--;
                        UpdateCursor();
                        break;
                    }
                case ConsoleKey.RightArrow: {
                        cursor.index++;
                        UpdateCursor();
                        break;
                    }
                case ConsoleKey.Backspace: {

                        void Remove(LispNode n) {
                            var parent = n.parent;
                            var index = n.index;
                            parent.items.RemoveAt(index);
                            if (parent.length > 0) {
                                cursor = (parent, index - 1);
                                UpdateCursor(false);
                            } else {
                                if(parent == root) {
                                    UpdateCursor(false);
                                    return;
                                }
                                Remove(parent);
                            }
                        }
                        switch (cursor.node) {
                            case LispExpression exp:
                                if (exp.length == 0) {
                                    Remove(exp);
                                } else if(exp.items[cursor.index] == null) {
                                    exp.items.RemoveAt(cursor.index);
                                    cursor.index--;
                                    if (exp.length == 0) {
                                        Remove(exp);
                                    } else {
                                        UpdateCursor(false);
                                    }
                                }
                                break;
                            case LispString ls: {
                                    if(ls.value.Length == 0) {
                                        Remove(ls);
                                        break;
                                    }
                                    if (cursor.index == 0) {
                                        break;
                                    }
                                    if (cursor.index == ls.value.Length) {
                                        
                                        ls.value = ls.value[..(ls.length - 1)];
                                        cursor.index--;
                                        if(ls.length == 0) {
                                            cursor = (ls.parent, ls.index);
                                            ls.parent.items[ls.index] = null;
                                            UpdateCursor(false);
                                        }
                                    } else {
                                        ls.value = ls.value[..(cursor.index - 1)] + ls.value[(cursor.index)..];
                                        cursor.index--;
                                    }
                                    break;
                                }

                        }
                        break;
                    }
                case ConsoleKey.Enter:
                    //Show on new line
                    break;
                case ConsoleKey.Spacebar: {
                        if (k.Modifiers.HasFlag(ConsoleModifiers.Shift)) {
                            var (node, i) = GetCursorExpression();
                            if (node == root) {
                                break;
                            }
                            var (parent, index) = (node.parent, node.index);
                            if (index == parent.length - 1) {
                                parent.items.Add(null);
                            } else {
                                parent.items.Insert(index + 1, null);
                            }
                            cursor = (parent, index + 1);
                        } else if (k.Modifiers.HasFlag(ConsoleModifiers.Control)) {
                            var (node, i) = GetCursorExpression();
                            if (node == root) {
                                break;
                            }
                            var (parent, index) = (node.parent, node.index);
                            if (index < parent.length) {
                                parent.items.Insert(index, null);
                            } else {
                                parent.items.Add(null);
                            }
                            cursor = (parent, index - 1);
                        } else {
                            if (cursor.node == root) {
                                var index = cursor.index;
                                if (index == root.items.Count) {
                                    root.items.Add(null);
                                } else {
                                    root.items.Insert(index + 1, null);
                                }
                                cursor.index++;
                            } else {
                                var parent = cursor.node.parent;

                                var index = cursor.node.index;
                                if (index == parent.items.Count) {
                                    parent.items.Add(null);
                                } else {
                                    parent.items.Insert(cursor.index == 0 ? index : index + 1, null);
                                }

                                cursor = (parent, cursor.index == 0 ? index : index + 1);
                            }
                        }
                        break;
                    }
                default: {
                        if(k.KeyChar == 0) {
                            break;
                        }
                        AddChar(k.KeyChar);
                        break;
                    }
            }
            void AddChar(char c) {
                switch (cursor.node) {
                    case LispExpression exp: {

                            var next = (LispNode)(c == '(' ? new LispExpression() { parent = exp, items = { null } } : new LispString() { parent = exp, value = $"{c}" });
                            var nextCursor = c == '(' ? (next, 0) : (next, 1);
                            if (exp.length == 0) {
                                exp.items.Add(next);
                                cursor = nextCursor;
                            } else if(exp.items[cursor.index] == null) {
                                exp.items[cursor.index] = next;
                                cursor = nextCursor;
                            }
                            break;
                        }
                    case LispString ls: {
                            if (k.KeyChar != 0) {
                                if (cursor.index >= ls.value.Length) {
                                    ls.value = ls.value + k.KeyChar;
                                    cursor.index = ls.value.Length;
                                } else {
                                    ls.value = ls.value.Insert(cursor.index, c.ToString());
                                    cursor.index++;
                                }

                            }
                            break;
                        }
                }
            }
        }
        public (LispExpression, int) GetCursorExpression() {
            return cursor.node switch {
                LispExpression exp => (exp, cursor.index),
                LispString ls => (ls.parent, ls.index)
            };
        }
        public void Draw() {
            c.Clear();

            c.SetCursor(pos);
            var s = root.ToString().Split(32).Select(s => new ColoredString(s)).ToList();
            scroller.Draw(s, 64);
        }
    }
}
