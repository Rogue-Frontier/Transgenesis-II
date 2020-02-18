using Microsoft.Xna.Framework;
using SadConsole;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using static Transgenesis.Global;
using ColoredString = SadConsole.ColoredString;
namespace Transgenesis {
    class ElementEditor : IComponent {
        ProgramState state;
        Stack<IComponent> screens;
        Environment env;
        TranscendenceExtension extension;
        ConsoleManager c;

        XElement focused;
        HashSet<XElement> expanded;

        Input i;
        Suggest s;
        Tooltip t;

        int scrolling = 0;

        public ElementEditor(ProgramState state, Stack<IComponent> screens, Environment env, TranscendenceExtension extension, ConsoleManager c) {
            this.state = state;
            this.screens = screens;
            this.env = env;
            this.extension = extension;
            this.focused = extension.structure;
            this.expanded = new HashSet<XElement>();
            this.c = c;
            i = new Input(c);
            s = new Suggest(i, c);
            t = new Tooltip(i, s, c, new Dictionary<string, string>() {
                {"",    "Navigate Mode" + "\r\n" +
                        "-Up arrow: Previous element" + "\r\n" +
                        "-Down arrow: Next element" + "\r\n" +
                        "-Left arrow: Parent element" + "\r\n" +
                        "-Right arrow: First child element" + "\r\n" +
                        "-Return: Expand/collapse element" + "\r\n" +
                        "-Typing: Enter a command" + "\r\n"},
                {"add", "add <subelement>\r\n" +
                        "Adds the named subelement to the current element, if allowed"},
                {"reorder", "reorder <attribute...>\r\n" +
                        "Reorders the attributes in the current element in the specified order" },
                {"set", "set <attribute> [value]\r\n" +
                        "Sets the named attribute to the specified value on the current element. If [value] is empty, then deletes the attribute from the element" },

                //{"remove", "remove <subelement>\r\n" +
                //        "Removes the named subelement from the current element"},
                {"remove", "remove\r\n" +
                        "Removes the current element from its parent, if allowed"},
                {"bind", "bind\r\n" +
                        "Updates type bindings for the current extension"},
                {"bindall", "bindall\r\n" +
                        "Updates type bindings for all loaded extensions"},
                {"save", "save\r\n" +
                        "Saves the current extension to its file"},
                {"saveall", "saveall\r\n" +
                        "Saves all loaded extensions to their files"},
                {"expand", "expand\r\n" +
                        "Expands the current element to display all of its attributes and children"},
                {"collapse", "collapse\r\n" +
                        "Collapses the current element to hide most of its attributes and children"},
                {"moveup", "moveup\r\n" +
                        "Moves the current element up in its parent's order of children"},
                {"movedown", "movedown\r\n" +
                        "Moves the current element down in its parent's order of children"},
                {"root", "root\r\n" +
                        "Selects the root as the current element"},
                {"parent", "parent\r\n" +
                        "Selects the parent of the current element"},
                {"next", "next\r\n" +
                        "Selects the next child of the current element's parent"},
                {"previous", "previous\r\n" +
                        "Selects the previous child of the current element's parent"},
                {"types", "types\r\n" +
                        "Opens the Type Editor on this extension"},
                {"exit", "exit\r\n" +
                        "Exits this XML Editor and returns to the main menu"},




            });
            //{"", () => new List<string>{ "set", "add", "remove", "bind", "bindall", "save", "saveall", "moveup", "movedown", "root", "parent", "next", "prev", "types", "exit" } },
        }
        public void Draw() {
            c.Clear();
            c.SetCursor(new Point(0, 0));
            //Console.WriteLine(extension.structure.ToString());

            HashSet<XElement> semiexpanded = new HashSet<XElement>();

            //Add this element and its ancestors to the semiexpanded list
            foreach(var expandedElement in expanded) {
                MarkAncestorsSemiExpanded(expandedElement);
            }
            MarkAncestorsSemiExpanded(focused);

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
                while(elements.Count > 0) {
                    var e = elements.Dequeue();
                    semiexpanded.Add(e);
                    foreach(var child in e.Elements()) {
                        elements.Enqueue(child);
                    }
                }
            }

            var root = focused;
            while(root.Parent != null) {
                root = root.Parent;
            }
            int tabs = 0;
            string expandedBox =    "-   ";
            string collapsedBox =   "+   ";
            string noBox =          "    ";

            List<ColoredString> buffer = new List<ColoredString>();
            ShowElementTree(root);
            SyntaxHighlight();
            /*
            {
                List<ColoredString> buffer2 = new List<ColoredString>();
                ColoredString splitline = new ColoredString(150);
                int index = 0;
                foreach (var line in buffer) {
                    foreach (var c in line) {
                        if (c.Glyph == '\n') {
                            buffer2.Add(splitline);
                            splitline = new ColoredString(150);
                            index = 0;
                        } else {
                            splitline[index] = c;
                            index++;
                            if (index == 150) {
                                buffer2.Add(splitline);
                                splitline = new ColoredString(150);
                                index = 0;
                            }
                        }
                    }
                    if(index > 0) {
                        buffer2.Add(splitline);
                        splitline = new ColoredString(150);
                        index = 0;
                    }
                }
                buffer = buffer2;
            }
            */

            //IMPLEMENT SCROLLING

            int screenRows = 45;
            scrolling = Math.Max(0, Math.Min(scrolling, buffer.Count - screenRows));

            
            //Print only a portion of the buffer
            //c.margin = new Point(30, 0);
            c.margin = new Point(0, 0);
            c.SetCursor(c.margin);
            var count = Math.Min(screenRows, buffer.Count);
            var lines = buffer.GetRange(scrolling, count);

            //Let user know that there's more text
            if(scrolling + count + 1 < buffer.Count) {
                lines[lines.Count - 1] = new ColoredString("...", Color.White, Color.Black);
            }
            foreach (var line in lines) {
                c.Write(line);
                //Printing to the edge of the view already moves the cursor to the next line
                if(line.Count < 150) {
                    c.NextLine();
                }
            }

            i.Draw();
            s.Draw();
            t.Draw();


            void AddLine(string line) {
                int index = 0;
                ColoredString s = new ColoredString(150);
                foreach(var ch in line) {
                    if(ch == '\n') {
                        buffer.Add(s.SubString(0, index));
                        s = new ColoredString(150);
                        index = 0;
                        continue;
                    }
                    s[index] = new SadConsole.ColoredGlyph(ch, c.theme.front, c.theme.back);
                    index++;
                    if(index == 150) {
                        buffer.Add(s);
                        s = new ColoredString(150);
                        index = 0;
                    }
                }
                if (index > 0) {
                    buffer.Add(s.SubString(0, index));
                }
            }
            void AddLineHighlight(string line) {
                int index = 0;
                ColoredString s = new ColoredString(150);
                foreach (var ch in line) {
                    if (ch == '\n') {
                        buffer.Add(s.SubString(0, index));
                        s = new ColoredString(150);
                        index = 0;
                        continue;
                    }
                    s[index] = new SadConsole.ColoredGlyph(ch, c.theme.highlight, c.theme.back);
                    index++;
                    if (index == 150) {
                        buffer.Add(s);
                        s = new ColoredString(150);
                        index = 0;
                    }
                }
                if (index > 0) {
                    buffer.Add(s.SubString(0, index));
                }
            }
            void ShowElementTree(XElement element) {
                bool expandedCheck = expanded.Contains(element);
                string box;
                if(expandedCheck) {
                    box = expandedBox;
                } else {
                    box = collapsedBox;
                }
                const bool expandFocused = false;
                var isFocused = focused == element;
                if (element.Elements().Count() > 0) {
                    Action<string> writeTag;
                    if (isFocused) {
                        writeTag = s => AddLineHighlight(s);
                    } else {
                        writeTag = s => AddLine(s);
                    }
                    if (expandedCheck || (expandFocused && isFocused)) {
                        //show all attributes and children
                        writeTag($"{box}{Tab()}<{element.Tag()}{ShowAllAttributes(element)}>");
                        ShowChildren();
                        writeTag($"{box}{Tab()}</{element.Tag()}>");
                    } else {
                        //show only the important attributes and (semi)expanded children

                        if (!element.Elements().Any(c => semiexpanded.Contains(c))) {
                            //We have no important children to show, so just put our whole tag on one line
                            writeTag($"{box}{Tab()}<{element.Tag()}{ShowContextAttributes(element)}>...</{element.Tag()}>");
                        } else {
                            //Show any important children and attributes
                            writeTag($"{box}{Tab()}<{element.Tag()}{ShowContextAttributes(element)}>");
                            tabs++;
                            int skipped = 0;

                            foreach (var child in element.Elements()) {
                                if (semiexpanded.Contains(child)) {
                                    //Show that we have previous children not shown
                                    if (skipped > 0) {
                                        skipped = 0;
                                        AddLine($"{noBox}{Tab()}...");
                                    }
                                    ShowElementTree(child);
                                } else {
                                    skipped++;
                                }
                            }
                            //Show that we have more children not shown
                            if (skipped > 0) {
                                AddLine($"{noBox}{Tab()}...");
                            }
                            tabs--;
                            writeTag($"{box}{Tab()}</{element.Tag()}>");
                        }
                    }
                    return;

                    void ShowChildren() {
                        tabs++;
                        foreach (var child in element.Elements()) {
                            ShowElementTree(child);
                        }
                        tabs--;
                    }
                } else {

                    Action<string> writeTag;
                    if (isFocused) {
                        writeTag = s => AddLineHighlight(s);
                    } else {
                        writeTag = s => AddLine(s);
                    }
                    if (expanded.Contains(element) || (expandFocused && isFocused)) {
                        //show all attributes
                        writeTag($"{box}{Tab()}<{element.Tag()}{ShowAllAttributes(element)}/>");
                    } else {
                        //show only the important attributes
                        writeTag($"{box}{Tab()}<{element.Tag()}{ShowContextAttributes(element)}/>");
                    }
                    return;
                }
                
            }

            string Tab() => new string(' ', tabs * 4);
            string ShowContextAttributes(XElement element) {
                Dictionary<string, string> attributes = new Dictionary<string, string>();

                //If we have a few attributes, just show all of them inline
                if (element.Attributes().Count() < 4) {
                    foreach (var attribute in element.Attributes()) {
                        attributes[attribute.Name.LocalName] = attribute.Value;
                    }
                } else {
                    //Otherwise, just show the important ones
                    foreach (var key in new string[] { "unid", "name" }) {
                        if (element.Att(key, out string value)) {
                            attributes[key] = value;
                        }
                    }

                    //Or the first three ones
                    foreach (var attribute in element.Attributes()) {
                        if(attributes.Count < 3) {
                            attributes[attribute.Name.LocalName] = attribute.Value;
                        } else {
                            break;
                        }
                    }
                }


                bool inline = attributes.Count < 4;
                bool more = attributes.Count < element.Attributes().Count();
                return AttributesToString(attributes, inline, more);
            }
            string ShowAllAttributes(XElement element) {
                Dictionary<string, string> attributes = new Dictionary<string, string>();
                foreach (var attribute in element.Attributes()) {
                    attributes[attribute.Name.LocalName] = attribute.Value;
                }
                bool inline = attributes.Count < 4;
                return AttributesToString(attributes, inline, false);
            }
            string AttributesToString(Dictionary<string, string> attributes, bool inline, bool more) {
                if (attributes.Count == 0) {
                    return more ? " ..." : "";
                }
                if (inline) {
                    StringBuilder result = new StringBuilder();
                    foreach (string key in attributes.Keys) {
                        result.Append(" ");
                        //Pad space between each attribute so that keys are aligned by tab
                        //We can't align perfectly since we don't know the global position of the string
                        while (result.Length % 4 > 0) {
                            result.Append(' ');
                        }
                        /*
                        if (result.Length%4 > 0) {
                            int aligned = (result.Length - result.Length%4) + 4;
                            result.Append(new string(' ', aligned - result.Length));
                        }
                        */
                        result.Append($@"{key}=""{attributes[key]}""");
                    }
                    if (more) {
                        result.Append(" ");
                        result.Append("...");
                    }
                    return result.ToString();
                } else {
                    StringBuilder result = new StringBuilder();
                    result.Append(" ");
                    string first = attributes.Keys.First();
                    result.AppendLine($@"{first}=""{attributes[first]}""");
                    tabs++;

                    int interval = 8;
                    //int padding = (1 + attributes.Keys.Select(k => k.Length).Max() / interval) * interval;
                    foreach (string key in attributes.Keys.Skip(1)) {
                        int padding = (1 + key.Length / interval) * interval;
                        result.AppendLine($@"{noBox}{Tab()}{$"{key}=".PadRight(padding)}""{attributes[key]}""");
                    }
                    if (more) {
                        result.AppendLine($"{noBox}{Tab()}...");
                    }
                    result.Append($"{noBox}{Tab()}");
                    tabs--;
                    return result.ToString();
                }
            }
            void SyntaxHighlight() {
                Stack<Syntax> type = new Stack<Syntax>();
                foreach(var line in buffer) {
                    foreach(var glyph in line) {
                        if(type.Count == 0 || type.Peek() == Syntax.Space) {
                            if(char.IsWhiteSpace(glyph.GlyphCharacter)) {
                                continue;
                            } else if(type.Count > 0 && type.Peek() == Syntax.Space) {
                                type.Pop();
                            }
                            switch(glyph.GlyphCharacter) {
                                case var c when char.IsLetterOrDigit(c):
                                    type.Push(Syntax.Attribute);
                                    break;
                                case '"':
                                    //Since we pop upon seeing the opening quote
                                    type.Push(Syntax.Quotes);
                                    type.Push(Syntax.Quotes);
                                    break;
                                case '<':
                                    if(glyph.Foreground == c.theme.highlight) {
                                        type.Push(Syntax.FocusedTag);
                                    } else {
                                        type.Push(Syntax.Tag);
                                    }
                                    
                                    break;
                                case '&':
                                    type.Push(Syntax.Entity);
                                    break;
                                default:
                                    continue;
                            }
                        }

                        CheckType:
                        switch(type.Peek()) {
                            case Syntax.Attribute:
                                glyph.Foreground = Color.Salmon;
                                if(glyph.GlyphCharacter == '=') {
                                    //If we've found the end of the attribute name, add a space so that we can catch if the value is immediately in front of it.
                                    type.Pop();
                                    type.Push(Syntax.Space);
                                }
                                break;
                            case Syntax.Entity:
                                glyph.Foreground = Color.White;
                                if(glyph.GlyphCharacter == ';') {
                                    type.Pop();
                                }
                                break;
                            case Syntax.Quotes:
                                //If we encounter a space within quotes, we treat it as part of the quotes
                                glyph.Foreground = Color.SlateBlue;

                                if(glyph.GlyphCharacter == '&') {
                                    type.Push(Syntax.Entity);
                                    goto CheckType;
                                } else if (glyph.GlyphCharacter == '"') {
                                    type.Pop();
                                }
                                break;
                            case Syntax.Tag:
                                glyph.Foreground = Color.SkyBlue;
                                if(glyph.GlyphCharacter == '>') {
                                    type.Pop();
                                } else if (char.IsWhiteSpace(glyph.GlyphCharacter)) {
                                    type.Push(Syntax.Space);
                                }
                                break;
                            case Syntax.FocusedTag:
                                //Keep the highlight color
                                if (glyph.GlyphCharacter == '>') {
                                    type.Pop();
                                } else if (char.IsWhiteSpace(glyph.GlyphCharacter)) {
                                    type.Push(Syntax.Space);
                                }
                                break;
                        }
                    }
                }
            }
        }

        public void Handle(ConsoleKeyInfo k) {
            i.Handle(k);
            s.Handle(k);

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
                    env.bases[duplicate] = env.bases[focused];
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
                        env.bases[copy] = env.bases[state.copied];
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
                    break;
                case ConsoleKey.RightArrow when i.Text.Length == 0:
                    focused = focused.Elements().FirstOrDefault() ?? focused;
                    break;
                case ConsoleKey.DownArrow when i.Text.Length == 0:
                    focused = focused.ElementsAfterSelf().FirstOrDefault() ?? focused;
                    break;
                case ConsoleKey.UpArrow when i.Text.Length == 0:
                    focused = focused.ElementsBeforeSelf().LastOrDefault() ?? focused;
                    break;
                case ConsoleKey.Enter: {
                        if(input.Length == 0) {
                            if(expanded.Contains(focused)) {
                                expanded.Remove(focused);
                            } else {
                                expanded.Add(focused);
                            }

                            break;
                        }

                        string[] parts = input.Split(' ');
                        switch (parts[0]) {
                            case "add": {
                                    string elementName = parts[1];
                                    if (env.CanAddElement(focused, env.bases[focused], elementName, out XElement subtemplate)) {
                                        var subelement = env.FromTemplate(subtemplate, elementName);
                                        focused.Add(subelement);
                                        i.Clear();
                                    }
                                    break;
                                }
                            case "set": {
                                    string attribute = parts[1];
                                    string value = string.Join(" ", parts.Skip(2));
                                    if (value.Length > 0) {
                                        //Set the value
                                        focused.SetAttributeValue(attribute, value);
                                    } else if (!string.IsNullOrEmpty(attribute)) {
                                        //Delete the attribute if we enter no value
                                        focused.Attribute(attribute)?.Remove();
                                    }
                                    i.Clear();
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
                                    break;
                                }
                            case "bind": {
                                    extension.updateTypeBindings(env);
                                    break;
                                }
                            case "bindall": {
                                    foreach (var ext in env.extensions.Values) {
                                        ext.updateTypeBindings(env);
                                    }
                                    break;
                                }
                            case "save": {
                                    extension.Save();
                                    break;
                                }
                            case "saveall": {
                                    foreach (var extension in env.extensions.Values) {
                                        extension.Save();
                                    }
                                    break;
                                }
                            case "remove": {
                                    //TO DO

                                    //For now, this just removes the current element if it's not the root
                                    RemoveFocused();
                                    break;
                                }
                            case "expand": {
                                    expanded.Add(focused);
                                    break;
                                }
                            case "collapse": {
                                    expanded.Remove(focused);
                                    break;
                                }
                            case "moveup": {
                                    MoveUp();
                                    break;
                                }
                            case "movedown": {
                                    MoveDown();
                                    break;
                                }
                            case "goto": {
                                    //Go to the specified element
                                    break;
                                }
                            case "root": {
                                    while (focused.Parent != null) {
                                        focused = focused.Parent;
                                    }
                                    break;
                                }
                            case "parent": {
                                    focused = focused.Parent ?? focused;
                                    break;
                                }
                            /*
                            case "child":
                                focused = focused.Elements().FirstOrDefault() ?? focused;
                                break;
                            */
                            case "find":
                                //Start from the focused element and find elements matching this criteria
                                break;
                            case "next": {
                                    focused = focused.ElementsAfterSelf().FirstOrDefault() ?? focused;
                                    break;
                                }
                            case "prev": {
                                    focused = focused.ElementsBeforeSelf().LastOrDefault() ?? focused;
                                    break;
                                }
                            case "types": {
                                    screens.Push(new TypeEditor(screens, env, extension, c));
                                    break;
                                }
                            case "exit": {
                                    screens.Pop();
                                    break;
                                }
                        }
                        break;
                    }
                //Allow Suggest/History to handle up/down arrows
                case ConsoleKey.UpArrow:
                case ConsoleKey.DownArrow:
                    break;
                default: {

                        string[] parts = input.Split(' ');
                        if (parts.Length > 2) {
                            switch (parts[0]) {
                                case "set": {
                                        //Suggest values for the attribute
                                        string attribute = parts[1];
                                        string attributeType = env.bases[focused].Elements("A").FirstOrDefault(e => e.Att("name") == attribute)?.Att("valueType");
                                        if(attributeType == null) {
                                            s.Clear();
                                            break;
                                        }
                                        var all = env.GetAttributeValues(attributeType);
                                        if (focused.Att(attribute, out string value)) {
                                            all.Insert(0, value);
                                        }
                                        string rest = string.Join(" ", parts.Skip(2));
                                        var items = Global.GetSuggestions(rest, all);
                                        s.SetItems(items);
                                        break;
                                    }
                            }
                        } else {
                            //Disable suggest when input is completely empty so that we can navigate aroung the UI with arrow keys
                            if (input.Length == 0) {
                                s.SetItems(new List<HighlightEntry>());
                                break;
                            }

                            var empty = new List<string>();
                            Dictionary<string, Func<List<string>>> autocomplete = new Dictionary<string, Func<List<string>>> {
                                {"", () => new List<string>{ "set", "add", "remove", "bind", "bindall", "save", "saveall", "expand", "collapse", "moveup", "movedown", "root", "parent", "next", "prev", "types", "exit" } },
                                {"set", () => env.bases[focused].GetValidAttributes() },
                                {"add", () => env.GetAddableElements(focused, env.bases[focused]) },
                                {"remove", () => env.GetRemovableElements(focused, env.bases[focused]) },
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
                                //exit
                            };
                            string p = autocomplete.Keys.Last(prefix => input.StartsWith((prefix + " ").TrimStart()));
                            List<string> all = autocomplete[p]();

                            var items = Global.GetSuggestions(input.Substring(p.Length).TrimStart(), all);
                            s.SetItems(items);
                        }
                        break;
                    }
            }
            t.Handle(k);

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
                if (parent != null && Environment.CanRemoveElement(parent, env.bases[focused])) {
                    var before = focused.ElementsBeforeSelf().LastOrDefault();
                    focused.Remove();
                    //focused = parent;
                    focused = before ?? parent;
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

        public void Update() {
            i.Update();
            s.Update();
        }
    }
}
