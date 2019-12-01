using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using static Transgenesis.Global;
namespace Transgenesis {
    interface IComponent {
        void Update();
        void Handle(ConsoleKeyInfo k);
        void Draw();
    }
    class Commander : IComponent {
        Stack<IComponent> screens;
        Input i;
        Suggest s;
        Environment env;

        public Commander(Stack<IComponent> screens) {
            i = new Input();
            s = new Suggest(i);
            env = new Environment();
            this.screens = screens;
        }
        public void Draw() {
            Console.Clear();
            Global.SetCursor(0, 0);
            Console.WriteLine("Transgenesis II");
            Console.WriteLine();

            Console.WriteLine($"Extensions Loaded: {env.extensions.Count}");
            foreach (var e in env.extensions.Values) {
                Console.WriteLine($"{e.structure.Name.LocalName,-24}{e.structure.Att("name") ?? "",-24}{e.path}");
            }

            i.Draw();
            s.Draw();
        }

        public void Handle(ConsoleKeyInfo k) {
            i.Handle(k);
            s.Handle(k);

            string input = i.Text;
            switch (k.Key) {
                case ConsoleKey.Enter:
                    string command = i.Text;
                    i.Clear();

                    string[] parts = command.Split(' ');
                    switch (parts.First().ToLower()) {
                        case "create":
                            if (Enum.TryParse(parts[1], out ExtensionTypes ex)) {
                                env.CreateExtension(ex, parts[2]);
                            }
                            break;
                        case "unload": {
                                string path = Path.GetFullPath(string.Join(' ', parts.Skip(1)).Trim());
                                if (env.extensions.ContainsKey(path)) {
                                    env.extensions.Remove(path);
                                    //TO DO
                                    //Clear data from bases
                                }
                                break;
                            }
                        case "load": {
                                string path = Path.GetFullPath(string.Join(' ', parts.Skip(1)).Trim());
                                string xml = File.ReadAllText(path);
                                //Cheat the XML reader by escaping ampersands so we don't parse entities
                                xml = xml.Replace("&", "&amp;");
                                XmlDocument doc = new XmlDocument();
                                doc.LoadXml(xml);

                                env.LoadExtension(doc, path);

                                break;
                            }
                        case "open": {
                                string path = Path.GetFullPath(string.Join(' ', parts.Skip(1)).Trim());
                                string xml = File.ReadAllText(path);
                                //Cheat the XML reader by escaping ampersands so we don't parse entities
                                xml = xml.Replace("&", "&amp;");
                                XmlDocument doc = new XmlDocument();
                                doc.LoadXml(xml);

                                env.LoadExtension(doc, path);

                                if (env.extensions.TryGetValue(path, out TranscendenceExtension result)) {
                                    screens.Push(new ExtensionEditor(screens, env, result));
                                }

                                break;
                            }
                        case "edit":
                            if (parts.Length == 2) {
                                string ext = parts[1];
                                if (env.extensions.TryGetValue(ext, out TranscendenceExtension result)) {
                                    screens.Push(new ExtensionEditor(screens, env, result));
                                }
                            } else {

                            }
                            break;
                        case "types":
                            if (parts.Length == 2) {
                                string ext = parts[1];
                                if (env.extensions.TryGetValue(ext, out TranscendenceExtension result)) {
                                    screens.Push(new TypeEditor(screens, env, result));
                                }
                            } else {

                            }
                            break;
                    }

                    break;
                //Allow Suggest/History to handle up/down arrows
                case ConsoleKey.UpArrow:
                case ConsoleKey.DownArrow:
                    break;
                default:
                    Dictionary<string, Func<List<string>>> autocomplete = new Dictionary<string, Func<List<string>>> {
                        {"", () => new List<string>{ "create", "load", "edit", "open", "unload" } },
                        {"create", () => new List<string>{ "TranscendenceAdventure", "TranscendenceExtension", "TranscendenceLibrary", "TranscendenceModule" } },
                        {"load", () => Directory.GetFiles(Directory.GetCurrentDirectory(), "*.xml").ToList() },
                        {"unload", () => env.extensions.Keys.ToList() },
                        {"edit", () => env.extensions.Keys.ToList() },
                        {"open", () => Directory.GetFiles(Directory.GetCurrentDirectory(), "*.xml").ToList() }
                    };
                    string str = autocomplete.Keys.Last(prefix => input.StartsWith((prefix + " ").TrimStart()));
                    List<string> all = autocomplete[str]();

                    var items = Global.GetSuggestions(input.Substring(str.Length).TrimStart(), all);
                    s.SetItems(items);
                    break;
            }

        }

        public void Update() {
            i.Update();
            s.Update();
        }

    }
    class ExtensionEditor : IComponent {
        Stack<IComponent> screens;
        Environment env;
        TranscendenceExtension extension;

        XElement focused;

        Input i;
        Suggest s;

        public ExtensionEditor(Stack<IComponent> screens, Environment env, TranscendenceExtension extension) {
            this.screens = screens;
            this.env = env;
            this.extension = extension;
            this.focused = extension.structure;

            i = new Input();
            s = new Suggest(i);
        }
        public void Draw() {
            Console.Clear();
            //Console.WriteLine(extension.structure.ToString());

            LinkedList<XElement> ancestors = new LinkedList<XElement>();
            XElement e = focused.Parent;
            while (e != null) {
                ancestors.AddFirst(e);
                e = e.Parent;
            }
            int tabs = 0;
            foreach (XElement ancestor in ancestors) {
                Global.PrintLine($"{Tab()}<{ancestor.Tag()}{ShowContextAttributes(ancestor)}>", ConsoleColor.White, ConsoleColor.Black);
                tabs++;
            }
            if (focused.ElementsBeforeSelf().Count() > 0) {
                Global.PrintLine($"{Tab()}...", ConsoleColor.White, ConsoleColor.Black);
            }


            //See if we need to print any children
            if (focused.HasElements) {
                Global.PrintLine($"{Tab()}<{focused.Tag()}{ShowAttributes(focused)}>", ConsoleColor.Green, ConsoleColor.Black);
                tabs++;
                foreach (XElement child in focused.Elements()) {
                    Global.PrintLine($"{Tab()}<{child.Tag()}{ShowContextAttributes(child)}/>", ConsoleColor.White, ConsoleColor.Black);
                }
                tabs--;
                Global.PrintLine($"{Tab()}</{focused.Tag()}>", ConsoleColor.Green, ConsoleColor.Black);
            } else {
                Global.PrintLine($"{Tab()}<{focused.Tag()}{ShowAttributes(focused)}/>", ConsoleColor.Green, ConsoleColor.Black);
            }



            if (focused.ElementsAfterSelf().Count() > 0) {
                Global.PrintLine($"{Tab()}...", ConsoleColor.White, ConsoleColor.Black);
            }
            tabs--;
            foreach (XElement ancestor in ancestors.Reverse()) {
                Global.PrintLine($"{Tab()}</{ancestor.Name.LocalName}>", ConsoleColor.White, ConsoleColor.Black);
                tabs--;
            }


            i.Draw();
            s.Draw();

            string Tab() => new string('\t', tabs);
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
                }


                bool inline = attributes.Count < 4;
                bool more = attributes.Count < element.Attributes().Count();
                return AttributesToString(attributes, inline, more);
            }
            string ShowAttributes(XElement element) {
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
                    foreach (string key in attributes.Keys.Skip(1)) {
                        result.AppendLine($@"{Tab()}{key}=""{attributes[key]}""");
                    }
                    if (more) {
                        result.AppendLine($"{Tab()}...");
                    }
                    result.Append(Tab());
                    tabs--;
                    return result.ToString();
                }
            }
        }

        public void Handle(ConsoleKeyInfo k) {
            i.Handle(k);
            s.Handle(k);

            string input = i.Text;
            switch (k.Key) {

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

                case ConsoleKey.Enter: {
                        string[] parts = input.Split(' ');
                        switch (parts[0]) {
                            case "add": {
                                    string elementName = parts[1];
                                    if (env.CanAddElement(focused, env.bases[focused], elementName, out XElement subtemplate)) {
                                        var subelement = env.FromTemplate(subtemplate);
                                        focused.Add(subelement);
                                        i.Clear();
                                    }
                                    break;
                                }
                            case "set": {
                                    string attribute = parts[1];
                                    string value = string.Join(' ', parts.Skip(2));
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
                                    var parent = focused.Parent;
                                    if (parent != null && Environment.CanRemoveElement(parent, env.bases[focused])) {
                                        focused.Remove();
                                        focused = parent;
                                    }
                                    break;
                                }
                            case "moveup": {
                                    var before = focused.ElementsBeforeSelf().LastOrDefault();
                                    if (before != null) {
                                        focused.Remove();
                                        before.AddBeforeSelf(focused);
                                    }
                                    break;
                                }
                            case "movedown": {
                                    var after = focused.ElementsBeforeSelf().LastOrDefault();
                                    if (after != null) {
                                        focused.Remove();
                                        after.AddAfterSelf(focused);
                                    }
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
                                    screens.Push(new TypeEditor(screens, env, extension));
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
                                        string attributeType = env.bases[focused].Elements("A").FirstOrDefault(e => e.Att("name") == attribute).Att("valueType");
                                        var all = env.GetAttributeValues(attributeType);
                                        if (focused.Att(attribute, out string value)) {
                                            all.Insert(0, value);
                                        }
                                        string rest = string.Join(' ', parts.Skip(2));
                                        var items = Global.GetSuggestions(rest, all);
                                        s.SetItems(items);
                                        break;
                                    }
                            }
                        } else {
                            var empty = new List<string>();
                            Dictionary<string, Func<List<string>>> autocomplete = new Dictionary<string, Func<List<string>>> {
                                {"", () => new List<string>{ "set", "add", "remove", "bind", "bindall", "save", "saveall", "moveup", "movedown", "root", "parent", "next", "prev", "types", "exit" } },
                                {"set", () => env.bases[focused].GetValidAttributes() },
                                {"add", () => env.GetAddableElements(focused, env.bases[focused]) },
                                {"remove", () => env.GetRemovableElements(focused, env.bases[focused]) },

                            };
                            string p = autocomplete.Keys.Last(prefix => input.StartsWith((prefix + " ").TrimStart()));
                            List<string> all = autocomplete[p]();

                            var items = Global.GetSuggestions(input.Substring(p.Length).TrimStart(), all);
                            s.SetItems(items);
                        }
                        break;
                    }
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
        }

        public void Update() {
            i.Update();
            s.Update();
        }
    }
    class TypeEditor : IComponent {
        Stack<IComponent> screens;
        Environment env;
        TranscendenceExtension extension;
        XElement focused;

        Input i;
        Suggest s;

        public TypeEditor(Stack<IComponent> screens, Environment env, TranscendenceExtension extension) {
            this.screens = screens;
            this.env = env;
            this.extension = extension;
            this.focused = extension.structure;

            i = new Input();
            s = new Suggest(i);
        }
        public void Draw() {
            Console.Clear();

            foreach (TypeElement e in extension.types.elements) {
                if (e is TypeRange range) {
                    //Display unid range
                } else if (e is TypeGroup group) {
                    //TO DO: If entity is bound, display unid and design
                    Console.WriteLine(group.comment);
                    foreach (var s in group.entities) {
                        Console.WriteLine($"{s}{(extension.typemap.TryGetValue(s, out XElement design) ? design.Tag() : ""),-32}");
                    }
                } else if (e is TypeEntry entry) {
                    Console.WriteLine($"{entry.entity,-32}{entry.unid,-32}{(extension.typemap.TryGetValue(entry.entity, out XElement design) ? design.Tag() : ""),-32}{entry.comment}");
                } else if (e is Type entity) {
                    Console.WriteLine($"{entity.entity,-32}{(extension.typemap.TryGetValue(entity.entity, out XElement design) ? design.Tag() : ""),-32}{entity.comment}");
                }
            }

            i.Draw();
            s.Draw();

        }

        public void Handle(ConsoleKeyInfo k) {
            i.Handle(k);
            s.Handle(k);

            string input = i.Text;
            switch (k.Key) {


                case ConsoleKey.Enter: {
                        string[] parts = input.Split(' ');
                        switch (parts[0]) {
                            case "entity":

                                break;
                            case "exit":
                                screens.Pop();
                                break;
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

                            }
                        } else {
                            var empty = new List<string>();
                            Dictionary<string, Func<List<string>>> autocomplete = new Dictionary<string, Func<List<string>>> {
                                {"", () => new List<string>{ "entity", "type", "group", "range" } },

                            };
                            string p = autocomplete.Keys.Last(prefix => input.StartsWith((prefix + " ").TrimStart()));
                            List<string> all = autocomplete[p]();

                            var items = Global.GetSuggestions(input.Substring(p.Length).TrimStart(), all);
                            s.SetItems(items);
                        }
                        break;
                    }
            }

        }

        public void Update() {
            i.Update();
            s.Update();
        }
    }
    class Input : IComponent {
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
        public Point pos = new Point(0, 24);
        public void Clear() {
            s.Clear();
            cursor = 0;
        }
        public void Update() {
        }
        public void Handle(ConsoleKeyInfo k) {
            if (k.KeyChar == 0) {
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
                }
            } else {
                switch (k.Key) {
                    case ConsoleKey.Backspace:
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
                            if (s.Length > 0) {
                                cursor--;
                                s.Remove(cursor, 1);
                            }
                        }
                        break;
                    case ConsoleKey.Enter:

                        break;
                    default:
                        if (cursor == s.Length) {
                            s.Append(k.KeyChar);
                        } else {
                            s.Insert(cursor, k.KeyChar);
                        }
                        cursor++;
                        break;
                }
            }
        }
        public void Draw() {
            Global.SetCursor(pos);

            if (cursor == s.Length) {
                Print(Text, ConsoleColor.White, ConsoleColor.Black);
                Print(" ", ConsoleColor.Black, ConsoleColor.White);
            } else {
                string text = Text;
                for (int i = 0; i < text.Length; i++) {
                    if (i == cursor) {
                        Print(text[i], ConsoleColor.Black, ConsoleColor.White);
                    } else {
                        Print(text[i], ConsoleColor.White, ConsoleColor.Black);
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
    class Tooltip : IComponent {
        public void Draw() {
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
    class Suggest : IComponent {
        Input i;
        int index = -1;
        public List<HighlightEntry> items;
        public Point pos = new Point(0, 25);
        ConsoleManager c;
        public Suggest(Input i) {
            this.i = i;
            items = new List<HighlightEntry>();

            c = new ConsoleManager(pos);
        }
        public Suggest(Input i, List<HighlightEntry> options) {
            this.i = i;
            this.items = options;

            c = new ConsoleManager(pos);
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
            Global.SetCursor(pos);
            c.ClearLines();
            c.ResetCursor();
            for (int i = 0; i < items.Count; i++) {
                var o = items[i];
                if (index == i) {
                    c.DrawSelected(o);
                } else {
                    c.Draw(o);
                }
                c.NextLine();

                //Begin a new column of items
                if (i % 16 == 15) {
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
}
