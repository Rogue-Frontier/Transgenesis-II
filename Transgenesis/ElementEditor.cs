using SadRogue.Primitives;
using SadConsole;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static Transgenesis.Global;
using ColoredString = SadConsole.ColoredString;
using SadConsole.Input;
using System.Diagnostics;
using ArchConsole;
using Console = SadConsole.Console;

namespace Transgenesis {

    public static class Common {
        //https://stackoverflow.com/a/12016968
        //Blend another color over this color
        public static Color Blend(this Color background, Color foreground, byte setAlpha = 0xff) {
            //Background should be premultiplied because we ignore its alpha value
            var alpha = (byte)(foreground.A);
            var inv_alpha = (byte)(255 - foreground.A);
            return new(
                r: (byte)((alpha * foreground.R + inv_alpha * background.R) >> 8),
                g: (byte)((alpha * foreground.G + inv_alpha * background.G) >> 8),
                b: (byte)((alpha * foreground.B + inv_alpha * background.B) >> 8),
                alpha: setAlpha
                );
        }
    }
    public class RectButton : Console {
        public Action leftClick;
        public Action rightClick;

        public Action leftHold;
        public Action rightHold;

        MouseWatch mouse;
        public bool enabled;
        public RectButton(Rectangle r, Action leftClick = null, Action rightClick = null, bool enabled = true) : base(r.Width, r.Height) {
            Position = new(r.X, r.Y);
            this.leftClick = leftClick;
            this.rightClick = rightClick;
            this.mouse = new();
            this.enabled = enabled;
        }
        public override bool ProcessMouse(MouseScreenObjectState state) {
            mouse.Update(state, IsMouseOver);
            if (!enabled) {
                goto Done;
            }
            if (!IsMouseOver) {
                goto Done;
            }
            if (mouse.leftPressedOnScreen) {
                switch (mouse.left) {
                    case ClickState.Released: leftClick?.Invoke(); break;
                    case ClickState.Held: leftHold?.Invoke(); break;
                }
            }
            if (mouse.rightPressedOnScreen) {
                switch (mouse.right) {
                    case ClickState.Released: rightClick?.Invoke(); break;
                    case ClickState.Held: rightHold?.Invoke(); break;
                }
            }
        Done:
            return base.ProcessMouse(state);
        }
        bool isLeftDown => leftClick != null && mouse.nowLeft;
        bool isRightDown => rightClick != null && mouse.nowRight;
        public override void Render(TimeSpan timeElapsed) {
            Color b;

            if (!enabled || !IsMouseOver) {
                return;
            } else if (isLeftDown || isRightDown) {
                b = Color.White.SetAlpha(128);
            } else {
                b = Color.White.SetAlpha(51);
            }

            var par = (Console)Parent;
            foreach (var p in Area.Positions()) {
                var cg = par.GetCellAppearance(p.X + Position.X, p.Y + Position.Y);
                cg.Background = cg.Background.Blend(b);
                this.SetCellAppearance(p.X, p.Y, cg);
            }
            base.Render(timeElapsed);
        }
    }
    class ElementEditor : IScreen {
        public string name => $"Editor: {extension.name}";
        ProgramState state;
        Stack<IScreen> screens;
        Environment env;
        GameData extension;
        ConsoleManager c;

        bool scrollToFocused;
        XElement focused;
        HashSet<XElement> keepExpanded;

        ElementFormatter formatter;

        Input i;
        History h;
        Suggest s;
        Tooltip t;
        Scroller scroller;

        Dictionary<string, string> helpMain = new Dictionary<string, string>() {
                {"",    "Navigate Mode" + "\r\n" +
                        "-Up          Previous element" + "\r\n" +
                        "-Down        Next element" + "\r\n" +
                        "-Ctrl+Up     Move element up" + "\r\n" +
                        "-Ctrl+Down   Move element down" + "\r\n" +
                        "-Left        Parent element" + "\r\n" +
                        "-Right       First child element" + "\r\n" +
                        "-Return      Expand/collapse element" + "\r\n" +
                        "-Typing      Enter a command" + "\r\n"},
                {"add", "add {subelement} {attribute}=\"{value}\"...\r\n" +
                        "Adds the named subelement to the current element, if allowed"},
                {"reorder", "reorder {attribute...}\r\n" +
                            "Reorders the attributes in the current element in the specified order" },
                {"set",     "set {attribute}=\"[value]\"\r\n" +
                            "Sets the named attribute to the specified value on the current element.\r\n" +
                            "If [value] is empty, then deletes the attribute from the element" },
                //{"remove", "remove <subelement>\r\n" +
                //        "Removes the named subelement from the current element"},
                {"remove",  "remove\r\n" +
                            "Removes the current element from its parent, if allowed"},
                {"bind",    "bind\r\n" +
                            "Updates type bindings for the current extension"},
                {"bindall", "bindall\r\n" +
                            "Updates type bindings for all loaded extensions"},
                {"save",    "save\r\n" +
                            "Saves the current extension to its file"},
                {"saveall", "saveall\r\n" +
                            "Saves all loaded extensions to their files"},
                {"expand",  "expand\r\n" +
                            "Expands the current element to display all of its attributes and children"},
                {"collapse","collapse\r\n" +
                            "Collapses the current element to hide most of its attributes and children"},
                {"moveup",  "moveup\r\n" +
                            "Moves the current element up in its parent's order of children"},
                {"movedown","movedown\r\n" +
                            "Moves the current element down in its parent's order of children"},
                {"root",    "root\r\n" +
                            "Selects the root as the current element"},
                {"parent",  "parent\r\n" +
                            "Selects the parent of the current element"},
                {"next",    "next\r\n" +
                            "Selects the next child of the current element's parent"},
                {"prev",    "prev\r\n" +
                            "Selects the previous child of the current element's parent"},
                {"text",    "text\r\n" +
                            "Edits the text content in the current element"},
                {"goto",    "[extension.][entity.]element[.element[#index]...]" + "\r\n" +
                            "Selects the specified element"},
                {"types",   "types\r\n" +
                            "Opens the Type Editor on this extension"},
                {"run",     "run\r\n" +
                            "Runs the program"},
                {"editmodule","editmodule [path]\r\n" +
                            "Edits the module at the path relative to the current extension file. If currently focused on a Module reference, you can omit <path>"},
                {"createmodule", "createmodule <extensionType> <path>\r\n" +
                            "Creates a module at the path relative to the current extension file"},
                {"exit",    "exit\r\n" +
                            "Exits this XML Editor and returns to the main menu"},
            };

        public ElementEditor(ProgramState state, Stack<IScreen> screens, Environment env, GameData extension, ConsoleManager c, XElement focused = null) {
            this.state = state;
            this.screens = screens;
            this.env = env;
            this.extension = extension;
            this.focused = focused ?? extension.structure;
            this.keepExpanded = new HashSet<XElement>();
            this.c = c;
            i = new Input(c);
            h = new History(i, c);
            s = new Suggest(i, c);
            t = new Tooltip(i, s, c, helpMain);
            scroller = new Scroller(c, i);
            //{"", () => new List<string>{ "set", "add", "remove", "bind", "bindall", "save", "saveall", "moveup", "movedown", "root", "parent", "next", "prev", "types", "exit" } },
        }

        void Click(XElement clicked) {
            if (true) {

                ToggleExpand(clicked);
                return;
            }
            if(focused == clicked) {
                ToggleExpand(focused);
                return;
            }
            focused = clicked;
        }
        void ToggleExpand(XElement e) {
            if (keepExpanded.Contains(e)) {
                keepExpanded.Remove(e);
            } else if (e.Nodes().Any() || e.Attributes().Count() > 3) {

                keepExpanded.Add(e);
            }
        }

        private Dictionary<string, (Rectangle rect, RectButton button)> buttons = new();
        public void Draw() {
            //Console.WriteLine(extension.structure.ToString());

            var root = focused;
            while(root.Parent != null) {
                root = root.Parent;
            }

            formatter = new ElementFormatter(c);
            HashSet<XElement> semiexpanded = new HashSet<XElement>();
            bool invertExpand = true;
            if(invertExpand) {
                MarkAncestorsSemiExpanded(focused);
                formatter.ShowElementTree(root, focused, keepExpanded, semiexpanded);
            } else {
                HashSet<XElement> expanded = new HashSet<XElement>(keepExpanded);

                const bool expandFocusedPath = true;
                if (expandFocusedPath) {
                    var f = focused;
                    while (f != null) {
                        expanded.Add(f);
                        f = f.Parent;
                    }
                }

                //Add this element and its ancestors to the semiexpanded list
                foreach (var expandedElement in keepExpanded) {
                    MarkAncestorsSemiExpanded(expandedElement);
                }
                MarkAncestorsSemiExpanded(focused);

                formatter.ShowElementTree(root, focused, expanded, semiexpanded);
            }

            var all = root.DescendantsAndSelf().ToList();
            HashSet<string> keptButtons = new();
            HashSet<string> removedButtons = new(buttons.Keys);
            foreach(var pair in formatter.smart.buttons) {
                var id = pair.Key;
                
                var r = new Rectangle(pair.Value.MinExtent + (0, 1 - scroller.scrolling), pair.Value.MaxExtent + (0, 1 - scroller.scrolling));
                
                if(r.MaxExtentY < scroller.yMin || r.MinExtentY > scroller.yMax) {
                    continue;
                }
                if(r.MinExtentY < scroller.yMin) {
                    var delta = scroller.yMin - r.MinExtentY;
                    r = new(r.MinExtent + (0, delta), r.MaxExtent);
                } else if(r.MaxExtentY > scroller.yMax) {
                    var delta = scroller.yMax - r.MaxExtentY;
                    r = new(r.MinExtent, r.MaxExtent + (0, delta));
                }

                removedButtons.Remove(id);
                if (buttons.TryGetValue(id, out var current)) {
                    if (current.rect == r) {
                        keptButtons.Add(id);
                        continue;
                    } else {
                        c.console.Children.Remove(current.button);
                    }
                }

                var e = all[int.Parse(id)];
                var b = new RectButton(r, () => ToggleExpand(e), () => focused = e);
                c.console.Children.Add(b);
                buttons[id] = (r, b);
            }
            foreach(var id in removedButtons) {
                c.console.Children.Remove(buttons[id].button);
                buttons.Remove(id);
            }

            var buffer = formatter.buffer;

            //We auto-expand children of the focused element if we press Ctrl-F
            //MarkDescendantsSemiExpanded(focused);
            void MarkAncestorsSemiExpanded(XElement element) {
                while (element != null) {
                    if (!semiexpanded.Contains(element)) {
                        semiexpanded.Add(element);
                        element = element.Parent;
                    } else {
                        //If we've already marked this element, then we have also marked its ancestors
                        break;
                    }
                }
            }
            void MarkDescendantsSemiExpanded(XElement parent) {
                Queue<XElement> elements = new Queue<XElement>(parent.Elements());
                while (elements.Count > 0) {
                    var e = elements.Dequeue();
                    semiexpanded.Add(e);
                    foreach (var child in e.Elements()) {
                        elements.Enqueue(child);
                    }
                }
            }
            if (scrollToFocused) {
                ScrollToFocused();
                scrollToFocused = false;
            }
            c.Clear();
            c.SetCursor(new Point(0, 1));
            if (i.Text.Length == 0) {
                scroller.Draw(buffer, scroller.screenRows + s.height);
                //i.Draw();
            } else {
                scroller.Draw(buffer);
                i.Draw();
                s.Draw();
            }
            t.Draw();
            //h.Draw();
        }
        public void ScrollToFocused() {
            if (formatter.highlightLines.Count > 0) {
                int topRow = scroller.scrolling;
                int bottomRow = topRow + scroller.screenRows - 1;

                int topLine = formatter.highlightLines.Min();
                int bottomLine = formatter.highlightLines.Max();

                if (bottomRow - 1 < topLine) {
                    //Scroll down to see the top of the element on the last line
                    scroller.scrolling = topLine - scroller.screenRows + 2;
                } else if (topRow + 1 > bottomLine) {
                    //Scroll up to see the bottom of the element on the first line
                    scroller.scrolling = bottomLine - 1;
                }
            }
        }
        public void ScrollToHome() {
            scroller.scrolling = 0;
        }
        public void ScrollToEnd() {
            scroller.scrolling = formatter.buffer.Count - scroller.screenRows;
        }
        public void ScrollToFocusedOpenTag() {
            if (formatter.highlightLines.Count > 0) {
                int topLine = formatter.highlightLines.Min();
                //Scroll down to see the start of the element on the first line
                scroller.scrolling = topLine - 1;
            }
        }
        public void ScrollToFocusedCloseTag() {
            if (formatter.highlightLines.Count > 0) {
                int topRow = scroller.scrolling;
                int bottomLine = formatter.highlightLines.Max();

                //Scroll down to see the end of the element on the last line
                scroller.scrolling = bottomLine - scroller.screenRows + 2;
            }
        }

        public void Handle(ConsoleKeyInfo k) {
            i.Handle(k);
            h.Handle(k);
            s.Handle(k);
            scroller.Handle(k);
            t.warning = null;
            string input = i.Text;

            switch (k.Key) {
                /*
                case ConsoleKey.LeftArrow when (k.Modifiers & ConsoleModifiers.Control) != 0:
                    focused = focused.Parent ?? focused;
                    break;
                case ConsoleKey.RightArrow when (k.Modifiers & ConsoleModifiers.Control) != 0:
                    focused = focused.Elements().FirstOrDefault() ?? focused;
                    break;
                case ConsoleKey.OemPlus when (k.Modifiers & ConsoleModifiers.Control) != 0:
                    focused = focused.ElementsAfterSelf().FirstOrDefault() ?? focused;
                    break;
                case ConsoleKey.OemMinus when (k.Modifiers & ConsoleModifiers.Control) != 0:
                    focused = focused.ElementsBeforeSelf().LastOrDefault() ?? focused;
                    break;
                    */

                case ConsoleKey.R when (k.Modifiers & ConsoleModifiers.Control) != 0:
                    //Removes the current element
                    RemoveFocused();
                    break;
                case ConsoleKey.D when (k.Modifiers & ConsoleModifiers.Control) != 0:
                    //Make a duplicate of the element
                    var duplicate = new XElement(focused);
                    //Remember to copy the base template so that we know how to treat this element
                    env.LoadWithTemplate(duplicate, env.bases[focused]);
                    focused.AddAfterSelf(duplicate);
                    break;
                case ConsoleKey.C when (k.Modifiers & ConsoleModifiers.Control) != 0:
                    //Remember this element
                    state.copied = focused;
                    break;
                case ConsoleKey.V when (k.Modifiers & ConsoleModifiers.Control) != 0:
                    //Later, we should attempt to manually reconstruct the element as allowed by the parent's template

                    //Paste a deep copy of the element
                    if (state.copied != null) {
                        var copy = new XElement(state.copied);
                        //Remember to copy the base template so that we know how to treat this element
                        env.LoadWithTemplate(copy, env.bases[state.copied]);
                        focused.Add(copy);
                    }
                    break;
                case ConsoleKey.DownArrow when (k.Modifiers & ConsoleModifiers.Control) != 0:
                    MoveDown();
                    break;
                case ConsoleKey.UpArrow when (k.Modifiers & ConsoleModifiers.Control) != 0:
                    MoveUp();
                    break;

                //Navigate using arrow keys when command input is empty
                case ConsoleKey.LeftArrow when i.Text.Length == 0:
                    focused = focused.Parent ?? focused;
                    scrollToFocused = true;
                    break;
                case ConsoleKey.RightArrow when i.Text.Length == 0:
                    focused = focused.Elements().FirstOrDefault() ?? focused;
                    scrollToFocused = true;
                    break;
                case ConsoleKey.DownArrow when i.Text.Length == 0:
                    focused = focused.ElementsAfterSelf().FirstOrDefault() ?? focused;
                    scrollToFocused = true;
                    break;
                case ConsoleKey.UpArrow when i.Text.Length == 0:
                    focused = focused.ElementsBeforeSelf().LastOrDefault() ?? focused;
                    scrollToFocused = true;
                    break;
                case ConsoleKey.Home when i.Text.Length == 0:
                    if((k.Modifiers & ConsoleModifiers.Shift) == 0) {
                        ScrollToHome();
                    } else {
                        ScrollToFocusedOpenTag();
                    }
                    break;
                case ConsoleKey.End when i.Text.Length == 0:
                    if ((k.Modifiers & ConsoleModifiers.Shift) == 0) {
                        ScrollToEnd();
                    } else {
                        ScrollToFocusedCloseTag();
                    }
                    break;
                case ConsoleKey.Enter: {
                        if(input.Length == 0) {
                            ToggleExpand(focused);
                            break;
                        }


                        string[] parts = input.Split(' ');
                        switch (parts[0]) {
                            case "add": {
                                    string elementName = parts[1];
                                    if (env.CanAddElement(focused, env.bases[focused], elementName, out XElement subtemplate)) {
                                        var subelement = env.FromTemplate(subtemplate, elementName);
                                        focused.Add(subelement);
                                        foreach(Match m in new Regex("(?<attribute>[a-zA-Z0-9]+)=\"(?<value>[^\"]*)\"").Matches(input)) {
                                            string key = m.Groups["attribute"].Value;
                                            string value = m.Groups["value"].Value;
                                            subelement.SetAttributeValue(key, value);
                                        }
                                        focused = subelement;
                                        h.Record();
                                    }
                                    break;
                                }
                            case "set": {
                                    if (parts.Length == 1)
                                        break;
                                    foreach (Match m in new Regex("(?<attribute>[a-zA-Z0-9]+)=\"(?<value>[^\"]*)\"").Matches(input)) {
                                        string key = m.Groups["attribute"].Value;
                                        string value = m.Groups["value"].Value;
                                        if (value.Length > 0) {
                                            focused.SetAttributeValue(key, value);
                                        } else {
                                            focused.Attribute(key)?.Remove();
                                        }
                                    }
                                    h.Record();
                                    break;
                                }
                            case "reorder": {
                                    Dictionary<string, string> attributes = new Dictionary<string, string>();
                                    foreach(var a in focused.Attributes()) {
                                        attributes[a.Name.LocalName] = a.Value;
                                    }
                                    focused.RemoveAttributes();
                                    foreach(var a in parts.Skip(1)) {
                                        if(attributes.TryGetValue(a, out string value)) {
                                            focused.SetAttributeValue(a, value);
                                            attributes.Remove(a);
                                        }
                                    }
                                    foreach(var a in attributes.Keys) {
                                        focused.SetAttributeValue(a, attributes[a]);
                                    }
                                    h.Record();
                                    break;
                                }
                            case "bind": {
                                    extension.updateTypeBindings(env);
                                    h.Record();
                                    break;
                                }
                            case "bindall": {
                                    foreach (var ext in env.extensions.Values) {
                                        ext.updateTypeBindings(env);
                                    }
                                    h.Record();
                                    break;
                                }
                            case "save": {
                                    extension.Save();
                                    h.Record();
                                    break;
                                }
                            case "saveall": {
                                    foreach (var extension in env.extensions.Values) {
                                        extension.Save();
                                    }
                                    h.Record();
                                    break;
                                }
                            case "remove": {
                                    //TO DO

                                    //For now, this just removes the current element if it's not the root
                                    RemoveFocused();
                                    h.Record();
                                    break;
                                }
                            case "expand": {
                                    keepExpanded.Add(focused);
                                    h.Record();
                                    break;
                                }
                            case "collapse": {
                                    keepExpanded.Remove(focused);
                                    h.Record();
                                    break;
                                }
                            case "moveup": {
                                    MoveUp();
                                    h.Record();
                                    break;
                                }
                            case "movedown": {
                                    MoveDown();
                                    h.Record();
                                    break;
                                }
                            case "goto": {
                                    new GotoHandler() {
                                        state = state,
                                        screens = screens,
                                        env = env,
                                        extension = extension,
                                        c = c,
                                        focused = focused
                                    }.HandleGoto(parts);
                                    h.Record();
                                    break;
                                }
                            case "root": {
                                    while (focused.Parent != null) {
                                        focused = focused.Parent;
                                    }
                                    h.Record();
                                    break;
                                }
                            case "parent": {
                                    focused = focused.Parent ?? focused;
                                    h.Record();
                                    break;
                                }
                                /*
                            case "child":
                                focused = focused.Elements().FirstOrDefault() ?? focused;
                                break;
                            */
                            /*
                            case "find":
                                //Start from the focused element and find elements matching this criteria
                                break;
                            */
                            case "next": {
                                    focused = focused.ElementsAfterSelf().FirstOrDefault() ?? focused;
                                    h.Record();

                                    break;
                                }
                            case "prev": {
                                    focused = focused.ElementsBeforeSelf().LastOrDefault() ?? focused;
                                    h.Record();
                                    break;
                                }
                            case "text": {
                                    screens.Push(new TextEditor(screens, c, string.Join(" ", focused.Nodes().OfType<XText>().Select(t => t.Value)).Replace("\t", "    "), str => {
                                        foreach (var t in focused.Nodes().OfType<XText>()) {
                                            t.Remove();
                                        }
                                        focused.AddFirst(new XText(str));
                                    }));
                                    h.Record();
                                    break;
                                }
                            case "lisp": {
                                    screens.Push(new LispEditor(screens, c, string.Join(" ", focused.Nodes().OfType<XText>().Select(t => t.Value)).Replace("\t", "    "), str => {
                                        foreach (var t in focused.Nodes().OfType<XText>()) {
                                            t.Remove();
                                        }
                                        focused.AddFirst(new XText(str));
                                    }));
                                    h.Record();
                                    break;
                                }
                            case "types": {
                                    screens.Push(new TypeEditor(screens, env, extension, c, new GotoHandler() {
                                        state = state,
                                        screens = screens,
                                        env = env,
                                        extension = extension,
                                        c = c
                                    }));
                                    h.Record();
                                    break;
                                }
                            case "createmodule": {
                                    if (parts.Length < 3) {
                                        break;
                                    }
                                    //Always use full-path so that we can easily find this
                                    var path = Path.GetDirectoryName(extension.path) + Path.DirectorySeparatorChar + parts[2];
                                    env.CreateExtension(parts[1], path);
                                    env.SaveState();
                                    h.Record();
                                    break;
                                }
                            case "editmodule": {

                                    string module;
                                    if (parts.Length < 2) {
                                        var att = env.bases[focused].Att("module");
                                        if(att == null) {
                                            break;
                                        }
                                        module = focused.Att(att);
                                        if(module == null) {
                                            break;
                                        }
                                    } else {
                                        module = parts[1];
                                    }
                                    //Always use full-path so that we can easily find this
                                    var path = Path.GetDirectoryName(extension.path) + Path.DirectorySeparatorChar + module;
                                    if (env.extensions.TryGetValue(path, out var ext)) {
                                        screens.Push(state.sessions.Initialize(ext, new ElementEditor(state, screens, env, ext, c)));
                                    }
                                    h.Record();
                                    break;
                                }
                            case "editparent": {
                                    var parent = extension.parent;
                                    if(parent != null) {
                                        screens.Push(state.sessions.Initialize(parent, new ElementEditor(state, screens, env, parent, c)));
                                    }
                                    h.Record();
                                    break;
                                }
                            case "run": {
                                    Process.Start(new ProcessStartInfo() {
                                        FileName = env.schema.Att("run"),
                                        UseShellExecute = true
                                    });
                                    h.Record();
                                    break;
                                }
                            case "exit": {
                                    screens.Pop();
                                    h.Record();
                                    break;
                                }
                        }
                        break;
                    }
                //Allow Suggest/History to handle up/down arrows
                case ConsoleKey.UpArrow:
                case ConsoleKey.DownArrow:
                    break;
                default: UpdateSuggest(); break;
            }
            t.Handle(k);
            if (input == "") {
                t.text = $"<{focused.Tag()}> {env.bases[focused].Att("desc")}\n\r{t.text}";
            }
            /*
            if(str.Length > 0) {
                List<string> all = null;
                switch (str[0]) {
                    case char c when c >= 'a' && c <= 'z':
                        all = env.bases[focused].GetValidAttributes();
                        break;
                    case char c when c >= 'A' && c <= 'Z':
                        all = env.bases[focused].GetValidSubelements();
                        break;
                    case '&':

                        break;
                    default:
                        all = new List<string>();
                        break;
                }
                var items = Global.GetSuggestions(str, all);
                s.SetItems(items);
            }
            */
            void RemoveFocused() {
                var parent = focused.Parent;
                if (parent != null && env.CanRemoveElement(parent, env.bases[focused])) {
                    var after = focused.ElementsAfterSelf().FirstOrDefault();
                    focused.Remove();
                    //focused = parent;
                    focused = after ?? parent;
                }
            }
            void MoveUp() {
                var before = focused.ElementsBeforeSelf().LastOrDefault();
                if (before != null) {
                    focused.Remove();
                    before.AddBeforeSelf(focused);
                }
            }
            void MoveDown() {
                var after = focused.ElementsAfterSelf().FirstOrDefault();
                if (after != null) {
                    focused.Remove();
                    after.AddAfterSelf(focused);
                }
            }
        }
        public void Handle(MouseScreenObjectState mouse) {
            scroller.Handle(mouse);
        }

        public void Update() {
        }
        public static bool TryMatch(string input, Regex r, out Match m) {
            return (m = r.Match(input)).Success;
        }
        public void UpdateSuggest() {
            var input = i.Text;
            //Disable suggest when input is completely empty so that we can navigate aroung the UI with arrow keys
            
            t.help = helpMain;
            List<HighlightEntry> Suggest(string text, IEnumerable<string> choices) => Global.GetSuggestions(text, choices);

            List<HighlightEntry> result = new List<HighlightEntry>();
            int replaceStart = 0, replaceLength = -1;
            if (input.Length == 0) {
                goto Done;
            }

            if (!TryMatch(input, new Regex("^(?<cmd>[a-zA-Z0-9]+)"), out Match m)) {
                goto Done;
            }
            var cmd = m.Groups["cmd"].Value;
            switch (cmd) {
                case "add":
                    if (TryMatch(input, new Regex("^add\\s+(?<element>[a-zA-Z0-9_]*)$"), out m)) {
                        //Suggest element name
                        var g = m.Groups["element"];
                        replaceStart = g.Index;
                        //replaceLength = g.Length;
                        var el = env.GetAddableElements(focused, env.bases[focused]);
                        t.help = el.ToDictionary(e => e.Att("name"), e => $"<{e.Att("name")}> {e.Att("desc")}");
                        result = Suggest(g.Value, el.Select(e => e.Att("name")));
                        if (!result.Any()) {
                            t.warning = (new ColoredString($"Unknown subelement {g.Value}"));
                        }
                    } else if (TryMatch(input, new Regex("^add\\s+(?<element>[a-zA-Z0-9_]+)\\s+(?<attributes>([a-zA-Z0-9_]+=\"[^\"]*\"\\s*)*\\s)?(?<attribute>[a-zA-Z0-9_]*)$"), out m)) {
                        //Suggest attribute
                        var sub = m.Groups["element"].Value;
                        var g = m.Groups["attribute"];
                        replaceStart = g.Index;
                        //replaceLength = g.Length;
                        var e = env.bases[focused];
                        if (!e.TryNameElement(sub, out e)) {
                            t.warning = (new ColoredString($"Unknown subelement {sub}"));
                            s.Clear();
                            break;
                        }

                        e = env.InitializeTemplate(e);

                        var previous = new Regex("(?<key>[a-zA-Z0-9_]+)=\"[^\"]*\"").Matches(m.Groups["attributes"].Value).Select(m => m.Groups["key"].Value);

                        t.help = e.Elements("A").ToDictionary(a => a.Att("name"), a =>
                            $"<{e.Att("name")}> {e.Att("desc")}\n\r{a.Att("name")}=\"{{{a.Att("type")}}}\"\n\r{a.Att("desc")}");
                        result = Suggest(g.Value, e.GetValidAttributes().Except(previous));
                    } else if(TryMatch(input, new Regex("^add\\s+(?<element>[a-zA-Z0-9_]+)\\s+(?<attributes>([a-zA-Z0-9_]+=\"[^\"]*\"\\s*)*\\s)?(?<attribute>[a-zA-Z0-9_]+)=$"), out m)) {
                        //Show tooltip for the attribute
                        var sub = m.Groups["element"].Value;
                        string attribute = m.Groups["attribute"].Value;
                        //replaceLength = g.Length;
                        var e = env.bases[focused];
                        if (!e.TryNameElement(sub, out e)) {
                            t.warning = (new ColoredString($"Unknown subelement {sub}"));
                            s.Clear();
                            break;
                        }

                        e = env.InitializeTemplate(e);

                        if (!e.TryNameAttribute(attribute, out var att)) {
                            t.warning = (new ColoredString($"Unknown attribute {attribute}"));
                            s.Clear();
                            break;
                        }
                        var valueType = att.Att("type");

                        t.text = new($"<{e.Att("name")}> {e.Att("desc")}\n\r{attribute}=\"{{{valueType}}}\"\n\r{att.Att("desc") ?? ""}");
                        t.help = new();
                    } else if (TryMatch(input, new Regex("^add\\s+(?<element>[a-zA-Z0-9_]+)\\s+([a-zA-Z0-9_]+=\"[^\"]+\"\\s*)*(?<attribute>[a-zA-Z0-9_]+)=\"(?<value>[^\"]*)$"), out m)) {
                        //Suggest attribute values
                        var sub = m.Groups["element"].Value;
                        string attribute = m.Groups["attribute"].Value;
                        var e = env.bases[focused];
                        if(!e.TryNameElement(sub, out e)) {
                            t.warning = (new ColoredString($"Unknown subelement {sub}"));
                            s.Clear();
                            break;
                        }

                        e = env.InitializeTemplate(e);

                        if (!e.TryNameAttribute(attribute, out var att)) {
                            t.warning = (new ColoredString($"Unknown attribute {attribute}"));
                            s.Clear();
                            break;
                        }
                        var valueType = att.Att("type");

                        t.text = new($"<{e.Att("name")}> {e.Att("desc")}\n\r{attribute}=\"{{{valueType}}}\"\n\r{att.Att("desc")??""}");
                        t.help = new();
                        //show desc for highlighted option

                        var all = env.GetAttributeValues(extension, valueType);
                        var g = m.Groups["value"];
                        var v = g.Value;
                        replaceStart = g.Index;
                        //replaceLength = g.Length;
                        result = Suggest(v, all);
                    } else if(TryMatch(input, new Regex("^add\\s+(?<element>[a-zA-Z0-9_]*)"), out m)) {
                        var sub = m.Groups["element"].Value;
                        var e = env.bases[focused];
                        if (!e.TryNameElement(sub, out e)) {
                            t.warning = (new ColoredString($"Unknown subelement {sub}"));
                            s.Clear();
                            break;
                        }
                    } else {

                    }
                    break;
                case "set": {
                        if (TryMatch(input, new Regex("^set\\s+(?<attributes>([a-zA-Z0-9_]+=\"[^\"]*\"\\s*)*\\s)?(?<attribute>[a-zA-Z0-9_]*)$"), out m)) {
                            //Suggest attribute name
                            var g = m.Groups["attribute"];
                            replaceStart = g.Index;
                            //replaceLength = g.Length;

                            var e = env.bases[focused];
                            //e = env.InitializeTemplate(e);

                            var previous = new Regex("(?<key>[a-zA-Z0-9_]+)=\"[^\"]*\"").Matches(m.Groups["attributes"].Value).Select(m => m.Groups["key"].Value);

                            t.help = e.Elements("A").ToDictionary(a => a.Att("name"), a =>
                                $"<{e.Att("name")}> {e.Att("desc")}\n\r{a.Att("name")}=\"{{{a.Att("type")}}}\"\n\r{a.Att("desc")}");

                            var choices = env.bases[focused].GetValidAttributes().Except(previous);
                            result = Suggest(g.Value, choices);
                        } else if (TryMatch(input, new Regex("^set\\s+([a-zA-Z0-9_]+=\"[^\"]+\"\\s*)*(?<attribute>[a-zA-Z0-9_]+)=$"), out m)) {


                            //Show tooltip for the attribute
                            string attribute = m.Groups["attribute"].Value;
                            //replaceLength = g.Length;
                            var e = env.bases[focused];

                            e = env.InitializeTemplate(e);

                            if (!e.TryNameAttribute(attribute, out var att)) {
                                t.warning = (new ColoredString($"Unknown attribute {attribute}"));
                                s.Clear();
                                break;
                            }
                            var valueType = att.Att("type");

                            t.text = new($"<{e.Att("name")}> {e.Att("desc")}\n\r{attribute}=\"{{{valueType}}}\"\n\r{att.Att("desc") ?? ""}");
                            t.help = new();


                        } else if (TryMatch(input, new Regex("^set\\s+([a-zA-Z0-9_]+=\"[^\"]+\"\\s*)*(?<attribute>[a-zA-Z0-9_]+)=\"(?<value>[^\"]*)$"), out m)) {
                            //Suggest attribute name

                            string attribute = m.Groups["attribute"].Value;

                            List<string> all;
                            var e = env.bases[focused];

                            if (!e.TryNameAttribute(attribute, out var att)) {
                                t.warning = (new ColoredString($"Unknown attribute {attribute}"));
                                s.Clear();
                                break;
                            }
                            var valueType = att.Att("type");

                            if ((all = env.GetAttributeValues(extension, valueType)) == null) {
                                t.warning = (new ColoredString($"Unknown valueType {valueType}"));
                                s.Clear();
                                all = new();
                            }
                            if (focused.Att(attribute, out string value)) {
                                //Remove duplicate
                                all.Remove(value);
                                //Insert at the front
                                all.Insert(0, value);
                            }

                            //e = env.InitializeTemplate(e);
                            t.text = new($"<{e.Att("name")}> {e.Att("desc")}\n\r{attribute}=\"{{{valueType}}}\"\n\r{att.Att("desc") ?? ""}");
                            t.help = new();

                            var g = m.Groups["value"];
                            var v = g.Value;
                            replaceStart = g.Index;
                            //replaceLength = g.Length;
                            result = Suggest(v, all);
                        }
                        break;
                    }
                case "createmodule": {
                        if (TryMatch(input, new Regex("^createmodule\\s+(?<extensionType>[a-zA-Z0-9_]*)$"), out m)) {
                            //Suggest attribute name
                            var g = m.Groups["extensionType"];
                            replaceStart = g.Index;
                            result = Suggest(g.Value, env.rootStructures.Select(r => r.Key));
                        }
                        break;
                    }

                case "editmodule": {
                        if (TryMatch(input, new Regex("^editmodule\\s+(?<path>[a-zA-Z0-9_]*)$"), out m)) {
                            //Suggest attribute name
                            var g = m.Groups["path"];
                            replaceStart = g.Index;
                            //replaceLength = g.Length;
                            var choices = focused.Elements().Select(e => {
                                var att = env.bases[e].Att("module");
                                if (att == null) {
                                    return null;
                                }
                                return e.Att(att);
                            }).Where(s => !string.IsNullOrWhiteSpace(s));
                            result = Suggest(g.Value, choices);
                        }
                        break;
                    }
                default:
                    result = Suggest(cmd, new List<string> { "set", "add", "remove",
                            "bind", "bindall", "save", "saveall", "expand", "collapse",
                            "moveup", "movedown", "root", "parent", "next", "prev",
                            "text", "goto", "types", "createmodule", "loadmodule",
                            "editmodule", "editparent", "exit"
                        });
                    break;
            }
            /*
            var empty = new List<string>();
            Dictionary<string, Func<List<string>>> autocomplete = new Dictionary<string, Func<List<string>>> {
                                {"", () => },
                                {"set", () => env.bases[focused].GetValidAttributes() },
                                {"add", () => env.GetAddableElements(focused, env.bases[focused]) },
                                {"remove", () => env.GetRemovableElements(focused, env.bases[focused]) },
                                {"goto", () => new GotoHandler() {
                                        state = state,
                                        screens = screens,
                                        env = env,
                                        extension = extension,
                                        c = c,
                                        focused = focused
                                    }.SuggestGoto(parts[1]) }
                                //bind
                                //bindall
                                //save
                                //saveall
                                //moveup
                                //movedown
                                //root
                                //parent
                                //next
                                //prev
                                //types
                                //createmodule
                                //loadmodule
                                //editmodule
                                //editparent
                                //exit
                            };
            //string p = autocomplete.Keys.Last(prefix => input.StartsWith((prefix + " ").TrimStart()));
            //List<string> all = autocomplete[p]();

            //var items = Global.GetSuggestions(input.Substring(p.Length).TrimStart(), all);
            //s.SetItems(items);
            */

        Done:
            s.SetItems(result, replaceStart, replaceLength);

        }
    }
}
