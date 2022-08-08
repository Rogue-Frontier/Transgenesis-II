using SadConsole;
using SadRogue.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static SadConsole.ColoredString;

namespace Transgenesis {

    interface LispNode {
        public LispExpression parent { get; }
        int length { get; }
        public int index => parent.items.IndexOf(this);
        public int end => 0;
        public bool enter { get; }

        public string GetSpaces(int tabs) => (enter ? $"\n\r{new string(' ', 4 * tabs)}" : "");
        public (LispExpression parent, int index) GetParentPosition() => (parent, index);
        public LispNode SiblingBefore => parent == null ? null :
            (index > 0 ?
                parent.items[index - 1] :
                ((LispNode)parent).SiblingBefore
            )?.LastDescendant ?? this;
        public LispNode SiblingAfter => parent == null ? null :
            (index < parent.items.Count - 1 ?
                parent.items[index + 1] :
                ((LispNode)parent).SiblingAfter
            )?.FirstDescendant ?? this;
        public LispNode LastDescendant => this;
        public LispNode FirstDescendant => this;
        public IEnumerable<LispNode> leaves { get; }
        public void AddSiblingBefore(LispNode other) =>
            parent.items.Insert(index, other);
        public void AddSiblingAfter(LispNode other) =>
            parent.items.Insert(index + 1, other);
        public LispNode Replace(LispNode other) {
            if(parent != null)
                parent.items[index] = other;
            return other;
        }
        public void Remove() =>
            parent?.items.RemoveAt(index);
        public string ToString(int tabs);
        public string ToColored((LispNode node, int index) cursor, int tabs);
        public static LispNode Parse(string s) {
            int i = 0;
            s = s.Trim();
            if(s.Length == 0) {
                return null;
            }
            return ParseItem();
            LispNode ParseItem(LispExpression parent = null, bool enter = false) {
                switch (s[i]) {
                    case '(': i++; return ParseExpression(parent, enter);
                    default: return ParseSymbol(parent, enter);
                }
            }
            LispExpression ParseExpression(LispExpression parent = null, bool enter = false) {
                LispExpression e = new(parent, new()) { enter = enter };
                enter = false;
                while (i < s.Length) {
                    switch (s[i]) {
                        case ' ': i++; break;
                        case ')': i++; return e;
                        case '\n':
                        case '\r':
                            i++;
                            enter = true;
                            break;
                        default:
                            e.items.Add(ParseItem(e, enter));
                            enter = false;
                            break;
                    }
                }
                throw new Exception("Unexpected end of file");
            }
            LispString ParseSymbol(LispExpression parent = null, bool enter = false) {
                LispString str = new(parent, "");
                while (i < s.Length) {
                    
                    switch (s[i]) {
                        case ' ':
                        case ')':
                            return str;
                        case var c:
                            i++;
                            str.value += c;
                            break;
                    }
                }
                return str;
            }
        }
    }
    class LispNil : LispNode {
        public LispExpression parent { get; set; }
        public int length => 0;
        public bool enter { get; set; }
        public IEnumerable<LispNode> leaves => new List<LispNode> { this };
        public LispNil(LispExpression parent) { this.parent = parent; }

        public override string ToString() => $"Nil";
        public string ToString(int tabs) => $"{((LispNode)this).GetSpaces(tabs)}Nil";
        public string ToColored((LispNode node, int index) cursor, int tabs) =>
            $"{((LispNode)this).GetSpaces(tabs)}{(this == cursor.node ? $"[c:r f:black][c:r b:yellow]Nil[c:u][c:u]" : "Nil")}";
    }
    class LispExpression : LispNode {
        public LispExpression parent { get; set; }
        public int length => items.Count;
        public int end => length - 1;
        public bool enter { get; set; }
        public IEnumerable<LispNode> leaves => items.SelectMany(i => i.leaves);
        public List<LispNode> items = new();
        public LispNode LastDescendant => items.Last().LastDescendant;
        public LispNode FirstDescendant => items.First().FirstDescendant;
        public LispExpression(LispExpression parent, List<LispNode> items = null) {
            this.parent = parent;
            this.items = items ?? new() { new LispNil(this) };
        }
        public override string ToString() => $"({string.Join(' ', items.Select(i => i.ToString()))})";
        public string ToString(int tabs) => $"{((LispNode)this).GetSpaces(tabs)}({string.Join(' ', items.Select(i => i.ToString(tabs + 1)))})";
        public string ToColored((LispNode node, int index) cursor, int tabs) {
            Func<string, string> color = s => s;
            if (this == cursor.node.parent) {
                color = s => $"[c:r f:yellow]{s}[c:u]";
            }
            string content = string.Join(' ', items.Select(i => i.ToColored(cursor, tabs + 1)));
            return $"{((LispNode)this).GetSpaces(tabs)}{color("(")}{content}{color(")")}";
        }
    }
    class LispString : LispNode {
        public LispExpression parent { get; set; }
        public string value;
        public int length => value.Length;
        public int end => length;
        public bool enter { get; set; }

        public IEnumerable<LispNode> leaves => new List<LispNode> { this };
        public LispString(LispExpression parent, string value) { this.parent = parent; this.value = value; }
        public override string ToString() => $"{value}";
        public string ToString(int tabs) => $"{((LispNode)this).GetSpaces(tabs)}{value}";
        public string ToColored((LispNode node, int index) cursor, int tabs) {
            if(this == cursor.node) {
                var value = this.value;
                if(cursor.index == value.Length) {
                    value += "[c:r b:yellow] [c:u]";
                } else {
                    value = value[..cursor.index]
                        + $"[c:r f:black][c:r b:yellow]{value[cursor.index]}[c:u][c:u]"
                        + value[(cursor.index + 1)..];
                }
                return $"{((LispNode)this).GetSpaces(tabs)}[c:r f:yellow]{value}[c:u]";
            }
            return $"{((LispNode)this).GetSpaces(tabs)}{value}";
        }
    }
    class LispEditor : IScreen {
        public string name => "Lisp";
        Stack<IScreen> screens;
        ConsoleManager c;
        Scroller scroller;
        Point pos = (0, 0);
        Action<string> OnClosed;

        LispNode root;
        (LispNode node, int index) cursor;

        public LispEditor(Stack<IScreen> screens, ConsoleManager c, string Text = "", Action<string> OnClosed = null) {
            this.screens = screens;
            this.c = c;
            this.scroller = new(c);
            this.OnClosed = OnClosed;
            this.root = LispNode.Parse(Text) ?? new LispExpression(null);
            cursor = (root, 0);
            UpdateCursor(false);

        }
        public void UpdateCursor(bool forward = true) {

            var node = cursor.node;
            Start:
            if(cursor.index == -1) {
                var parent = cursor.node.parent;
                if(parent == null) {
                    cursor = (node, 0);
                    return;
                }
                var index = cursor.node.index;
                cursor = (parent, index - 1);
                forward = false;
                goto Start;
            }
            if(cursor.index > cursor.node.end) {
                var parent = cursor.node.parent;
                if(parent == null) {
                    cursor = (node, node.end);
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
                    cursor = (n, forward ? 0 : n.end);
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
                        OnClosed?.Invoke(root.ToString(0));
                        screens.Pop();
                        break;
                    }
                case ConsoleKey.DownArrow: {
                        
                        var prev = cursor.node;
                        var n = prev.SiblingAfter;
                        while (!n.enter) {
                            prev = n;
                            n = prev.SiblingAfter;
                            if (prev == n) {
                                break;
                            }
                        }
                        cursor = (n, 0);
                        break;
                    }
                case ConsoleKey.UpArrow: {
                        var prev = cursor.node;
                        var n = prev.SiblingBefore;
                        while (!n.enter) {
                            prev = n;
                            n = prev.SiblingBefore;
                            if(prev == n) {
                                break;
                            }
                        }
                        cursor = (n, 0);
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
                            if(n == root) {
                                root = new LispNil(null);
                                cursor = (root, 0);
                                return;
                            }
                            cursor = n.GetParentPosition();
                            n.Remove();
                            if (cursor.node.length > 0) {
                                cursor.index--;
                                UpdateCursor(false);
                            } else {
                                if(cursor.node == root) {
                                    root = new LispNil(null);
                                    cursor = (root, 0);
                                    UpdateCursor(false);
                                    return;
                                }
                                Remove(cursor.node);
                            }
                        }
                        switch (cursor.node) {
                            case LispNil nil: {
                                    Remove(nil);
                                    break;
                                }
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
                                            var n = new LispNil(cursor.node.parent) { enter = ls.enter };
                                            Replace(n);
                                            cursor = (n, 0);
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
                    if(cursor.node == root) {
                        break;
                    }
                    if(cursor.node.index == 0) {
                        cursor.node.parent.enter = !cursor.node.parent.enter;
                        break;
                    }
                    switch (cursor.node) {
                        case LispNil l: l.enter = !l.enter; break;
                        case LispString s: s.enter = !s.enter; break;
                    }
                    break;
                case ConsoleKey.Spacebar: {
                        if(cursor.node == root) {
                            break;
                        }
                        if (k.Modifiers.HasFlag(ConsoleModifiers.Shift)) {
                            var (parent, index) = cursor.node.GetParentPosition();
                            if (parent == root) {
                                break;
                            }
                            var n = new LispNil(parent.parent);
                            ((LispNode)parent).AddSiblingAfter(n);
                            cursor = (n, 0);
                        } else if (k.Modifiers.HasFlag(ConsoleModifiers.Control)) {
                            var (parent, index) = cursor.node.GetParentPosition();
                            if (parent == root) {
                                break;
                            }
                            var n = new LispNil(parent.parent);
                            ((LispNode)parent).AddSiblingBefore(n);
                            cursor = (n, 0);
                        } else {
                            /*
                            LispNode n;
                            switch (cursor.node) {
                                case LispNil:
                                    n = new LispNil(cursor.node.parent);
                                    cursor.node.AddSiblingAfter(n);
                                    break;
                                case LispString s:

                                    break;
                                    
                            }
                            */
                            Action<LispNil> add = (cursor.node is LispNil || cursor.index > 0) ? cursor.node.AddSiblingAfter : cursor.node.AddSiblingBefore;
                            var n = new LispNil(cursor.node.parent) {  enter = false };
                            add(n);
                            cursor = (n, 0);
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
                    case LispNil nil: {
                            var parent = cursor.node.parent;
                            if(c == '(') {
                                var n = new LispExpression(parent) { enter = nil.enter };
                                Replace(n);
                                cursor = (n.items[0], 0);
                            } else {
                                var n = new LispString(parent, $"{c}") { enter = nil.enter };
                                Replace(n);
                                cursor = (n, 1);
                            }
                            UpdateCursor(false);
                            break;
                        }
                    case LispString s: {
                            if (k.KeyChar != 0) {
                                if (cursor.index >= s.value.Length) {
                                    s.value = s.value + k.KeyChar;
                                    cursor.index = s.value.Length;
                                } else {
                                    s.value = s.value.Insert(cursor.index, c.ToString());
                                    cursor.index++;
                                }
                            }
                            break;
                        }
                }
            }
            void Replace(LispNode n) {
                if (cursor.node == root) {
                    root = n;
                } else {
                    cursor.node.Replace(n);
                }
            }
        }

        public void Draw() {
            c.Clear();

            c.SetCursor(pos);
            var s = Parser.Parse(root.ToColored(cursor, 0));
            c.Write(s);
        }
    }
}
