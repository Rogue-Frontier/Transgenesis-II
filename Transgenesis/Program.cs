using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Drawing;
using static Transgenesis.Global;
using System.Xml;
using System.IO;
using System.Xml.Linq;
using System.Linq;
using System.Diagnostics;

namespace Transgenesis {
    class ConsoleManager {
        public ConsoleManager(Point p) {
            this.margin = p;
        }

        public Point margin;
        ConsoleColor front = ConsoleColor.White, back = ConsoleColor.Black;

        List<(Point, string)> lines = new List<(Point, string)>();
        public Point GetCursorPosition() => new Point(Console.CursorLeft, Console.CursorTop);
        public void ClearLines() => lines.Clear();
        public void ClearScreen() {
            lines.ForEach(t => {
                (Point cursor, string s) = t;
                Global.SetCursor(cursor);
                Global.Print(new string(' ', s.Length), front, back);
            });
        }
        public void ResetCursor() {
            SetCursor(margin);
        }
        public void Write(string s, ConsoleColor? front, ConsoleColor? back) {
            (Console.ForegroundColor, Console.BackgroundColor) = (front ?? this.front, back ?? this.back);
            lines.Add((GetCursorPosition(), s));
            Console.Write(s);
        }
        public void WriteLine(string s, ConsoleColor? front, ConsoleColor back) {
            Write(s, front, back);
            NextLine();
        }
        public void ResetLine() {
            Console.SetCursorPosition(margin.X, Console.CursorTop);
        }
        public void NextLine() {
            Console.CursorTop++;
            ResetLine();
        }
        public void Draw(HighlightEntry h) {
            var c = ConsoleColor.Green;
            int highlightStart = h.highlightStart;
            int highlightLength = h.highlightLength;
            string str = h.str;
            if (highlightStart != -1) {
                Write(str.Substring(0, highlightStart), front, back);
                if (highlightLength != 0) {
                    Write(str.Substring(highlightStart, highlightLength), c, back);
                    Write(str.Substring(highlightStart + highlightLength), front, back);
                } else {
                    Write(str.Substring(highlightStart), front, back);
                }
            } else {
                Write(str, front, back);
            }
        }
        public void DrawSelected(HighlightEntry h) {
            var c = ConsoleColor.Green;
            int highlightStart = h.highlightStart;
            int highlightLength = h.highlightLength;

            (ConsoleColor front, ConsoleColor back) = (this.back, this.front);
            string str = h.str;
            if (highlightStart != -1) {
                Write(str.Substring(0, highlightStart), front, back);
                if (highlightLength != 0) {
                    Write(str.Substring(highlightStart, highlightLength), c, back);
                    Write(str.Substring(highlightStart + highlightLength), front, back);
                } else {
                    Write(str.Substring(highlightStart), front, back);
                }
            } else {
                Write(str, front, back);
            }
        }
    }
    class Program {

        static void Main(string[] args) {
            new Program().Run();
        }

        public void Run() {
            Stack<Component> screens = new Stack<Component>();
            screens.Push(new Commander(screens));
            Console.CursorVisible = false;
            bool draw = true;
            while (true) {
                if (Console.KeyAvailable) {
                    var k = Console.ReadKey(true);
                    screens.Peek().Handle(k);
                    draw = true;
                }
                screens.Peek().Update();
                if (draw) {
                    screens.Peek().Draw();
                    Console.BackgroundColor = ConsoleColor.Black;
                    Console.ForegroundColor = ConsoleColor.White;
                }
                draw = false;
            }
        }
    }
    interface Component {
        void Update();
        void Handle(ConsoleKeyInfo k);
        void Draw();
    }
    class Environment {
        public XElement hierarchy;
        public Dictionary<string, XElement> coreStructures = new Dictionary<string, XElement>();
        public Dictionary<string, XElement> baseStructures = new Dictionary<string, XElement>();
        public Dictionary<XElement, XElement> bases = new Dictionary<XElement, XElement>();
        public Dictionary<string, TranscendenceExtension> extensions = new Dictionary<string, TranscendenceExtension>();
        public Dictionary<string, List<string>> customAttributeValues;


        public Environment() {

            XmlDocument doc = new XmlDocument();
            try {
                doc.Load("Transcendence.xml");
            } catch {
                doc.Load("../../../Transcendence.xml");
            }
            hierarchy = XElement.Parse(doc.OuterXml);
            baseStructures["Hierarchy"] = hierarchy;
            foreach (var coreStructure in hierarchy.Elements("E").Where(e => (string)e.Attribute("category") != "virtual")) {
                coreStructures[(string)coreStructure.Attribute("name")] = coreStructure;
                baseStructures[(string)coreStructure.Attribute("name")] = coreStructure;
            }
            foreach (var baseStructure in hierarchy.Elements("E").Where(e => (string)e.Attribute("category") == "virtual")) {
                baseStructures[(string)baseStructure.Attribute("id")] = baseStructure;
            }

            customAttributeValues = new Dictionary<string, List<string>>();
            foreach(var attributeType in hierarchy.Elements("AttributeType")) {
                customAttributeValues[attributeType.Att("name")] = new List<string>(attributeType.Value.Replace("\t", "").Split('\r', '\n').Where(s => !string.IsNullOrWhiteSpace(s)));
            }
        }
        public bool CanAddElement(XElement element, XElement template, string subelement, out XElement subtemplate) {
            subtemplate = template.TryNameElement(subelement);
            if(subtemplate == null) {
                return false;
            }
            subtemplate = InitializeTemplate(subtemplate);
            return CanAddElement(element, subtemplate);
        }
        public bool CanRemoveElement(XElement element, XElement template, string subelement) {
            return CanRemoveElement(element, template.Elements("E").First(e => e.Att("name") == subelement));
        }
        public static bool CanAddElement(XElement element, XElement subtemplate) {
            switch(subtemplate.Att("category")) {
                case "+":
                case "*":
                    return true;
                case "?":
                case "1":
                    return element.Elements(subtemplate.Att("name")).Count() == 0;
                default:
                    return false;
            }
        }
        public static bool CanRemoveElement(XElement element, XElement subtemplate) {
            switch (subtemplate.Att("category")) {
                case "*":
                case "?":
                    return element.Elements(subtemplate.Att("name")).Count() > 1;
                case "+":
                    return element.Elements(subtemplate.Att("name")).Count() > 1;
                case "1":
                    return false;
                default:
                    return false;
            }
        }
        public List<string> GetAddableElements(XElement element, XElement template) {
            return template.Elements("E").Select(subtemplate => InitializeTemplate(subtemplate)).Where(subtemplate => CanAddElement(element, subtemplate)).Select(subtemplate => subtemplate.Att("name")).ToList();
        }
        public List<string> GetRemovableElements(XElement element, XElement template) {
            return template.Elements("E").Select(subtemplate => InitializeTemplate(subtemplate)).Where(subtemplate => CanRemoveElement(element, subtemplate)).Select(subtemplate => subtemplate.Att("name")).ToList();
        }
        public bool LoadExtension(XElement structure, string path) {
            if (Enum.TryParse(structure.Tag(), out ExtensionTypes ex)) {
                XElement template;
                switch (ex) {
                    case ExtensionTypes.TranscendenceAdventure:
                        template = coreStructures["TranscendenceAdventure"];
                        break;
                    case ExtensionTypes.TranscendenceExtension:
                        template = coreStructures["TranscendenceExtension"];
                        break;
                    case ExtensionTypes.TranscendenceLibrary:
                        template = coreStructures["TranscendenceLibrary"];
                        break;
                    case ExtensionTypes.TranscendenceModule:
                        template = coreStructures["TranscendenceModule"];
                        break;
                    default:
                        return false;
                }
                template = InitializeTemplate(template);
                var extension = new TranscendenceExtension(path, structure);
                extensions[path] = extension;
                LoadWithTemplate(structure, template);

                return true;
            }
            return false;
        }
        public void LoadWithTemplate(XElement structure, XElement template) {
            bases[structure] = template;
            Dictionary<string, XElement> subtemplates = new Dictionary<string, XElement>();
            foreach(XElement subtemplate in template.Elements()) {
                var initialized = InitializeTemplate(subtemplate);
                string name = initialized.Att("name");
                subtemplates[name] = initialized;
            }
            foreach(XElement subelement in structure.Elements()) {
                string name = subelement.Tag();
                if(subtemplates.TryGetValue(name, out XElement subtemplate) || subtemplates.TryGetValue("*", out subtemplate)) {
                    LoadWithTemplate(subelement, subtemplate);
                }
            }
        }
        public XElement FromTemplate(XElement template) {
            XElement result = new XElement(template.Att("name"));
            foreach(XElement subtemplate in template.Elements("E").Where(e => e.Att("category") == "1" || e.Att("category") == "+")) {
                var initialized = InitializeTemplate(subtemplate);
                var subelement = FromTemplate(initialized);
                bases[subelement] = initialized;
                result.Add(subelement);
            }
            //Initialize attributes to default values
            foreach (XElement attributeType in template.Elements("A")) {
                string attribute = attributeType.Att("name");
                string value = attributeType.Att("value");
                if(value != null) {
                    result.SetAttributeValue(attribute, value);
                }
            }

            bases[result] = template;
            return result;
        }
        //Initializes a template for actual use, handling inheritance
        public XElement InitializeTemplate(XElement template) {

            XElement result = new XElement(template.Name);
            if (template.Att("inherit", out string from)) {
                var parts = from.Split(':');
                string source = parts.First();
                //XElement template = original;
                /*
                while (template.Name.LocalName != source) {
                    template = template.Parent;
                    Console.WriteLine(template.Name.LocalName);
                }
                */
                //Start with the root and navigate to the base element
                XElement inherited = baseStructures[source];

                //Handle the inheritance chain
                inherited = InitializeTemplate(inherited);

                foreach (string part in parts.Skip(1)) {
                    //template = template.Element(part);
                    inherited = inherited.Elements("E").First(e => e.Att("name") == part);

                    //Handle the inheritance chain
                    inherited = InitializeTemplate(inherited);
                }
                //Inherit base attributes
                foreach (var a in inherited.Attributes()) {
                    result.SetAttributeValue(a.Name, a.Value);
                }
                //Inherit base elements
                foreach (var e in inherited.Elements()) {
                    result.Add(new XElement(e));
                }
            }
            //Handle additional/overriding attributes
            foreach (var a in template.Attributes()) {
                result.SetAttributeValue(a.Name, a.Value);
            }
            //Handle additional/overriding elements
            foreach (var e in template.Elements()) {
                if (result.NameElement(e.Att("name"), out XElement replaced)) {
                    replaced.ReplaceWith(e);
                } else {
                    result.Add(e);
                }
            }
            /*
            if(template.Att("name") == null && from?.StartsWith("DesignTypeBase:") == true) {
                Trace.Assert(!string.IsNullOrEmpty(result.Att("name")), result.ToString());
            }
            */
            return result;
        }
        public List<string> GetAttributeValues(string attributeType) {
            if(customAttributeValues.TryGetValue(attributeType, out List<string> values)) {
                return values;
            } else if(Enum.TryParse<AttributeTypes>(attributeType, out AttributeTypes attributeTypeEnum)) {
                switch(attributeTypeEnum) {
                    //TO DO
                    default:
                        return new List<string>();
                }
            } else {
                return new List<string>();
            }
        }
        public void CreateExtension(ExtensionTypes e, string path) {
            XElement structure;
            XElement template;
            TranscendenceExtension extension;

            switch (e) {
                case ExtensionTypes.TranscendenceAdventure:
                    template = coreStructures["TranscendenceAdventure"];
                    break;
                case ExtensionTypes.TranscendenceExtension:
                    template = coreStructures["TranscendenceExtension"];
                    break;
                case ExtensionTypes.TranscendenceLibrary:
                    template = coreStructures["TranscendenceLibrary"];
                    break;
                case ExtensionTypes.TranscendenceModule:
                    template = coreStructures["TranscendenceModule"];
                    break;
                default:
                    return;
            }
            template = InitializeTemplate(template);
            structure = FromTemplate(template);

            extension = new TranscendenceExtension(
                path: path,
                structure: structure
            );
            extensions[path] = extension;
            extension.Save();
        }
    }
    enum ExtensionTypes {
        TranscendenceUniverse,
        CoreLibrary,
        TranscendenceAdventure,
        TranscendenceExtension,
        TranscendenceLibrary,
        TranscendenceModule
    }
    class Commander : Component {
        Stack<Component> screens;
        Input i;
        Suggest s;
        Environment env;

        public Commander(Stack<Component> screens) {
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
            foreach(var e in env.extensions.Values) {
                Console.WriteLine($"{e.structure.Name.LocalName, -24}{e.structure.Att("name") ?? "", -24}{e.path}");
            }

            i.Draw();
            s.Draw();
        }

        public void Handle(ConsoleKeyInfo k) {
            i.Handle(k);
            s.Handle(k);

            string input = i.Text;
            switch(k.Key) {
                case ConsoleKey.Enter:
                    string command = i.Text;
                    i.Clear();

                    string[] parts = command.Split(' ');
                    switch(parts.First().ToLower()) {
                        case "create":
                            if(Enum.TryParse(parts[1], out ExtensionTypes ex)) {
                                env.CreateExtension(ex, parts[2]);
                            }
                            break;
                        case "load":
                            string path = string.Join(' ', parts.Skip(1));
                            XmlDocument doc = new XmlDocument();
                            doc.Load(path);
                            var structure = XElement.Parse(doc.OuterXml);
                            env.LoadExtension(structure, path);
                            break;
                        case "edit":
                            if(parts.Length == 2) {
                                string ext = parts[1];
                                if(env.extensions.TryGetValue(ext, out TranscendenceExtension result)) {
                                    screens.Push(new ExtensionEditor(screens, env, result));
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
                        {"", () => new List<string>{ "create", "load", "edit" } },
                        {"create", () => new List<string>{ "TranscendenceAdventure", "TranscendenceExtension", "TranscendenceLibrary", "TranscendenceModule" } },
                        {"load", () => Directory.GetFiles(Directory.GetCurrentDirectory(), "*.xml").ToList() },
                        {"edit", () => env.extensions.Keys.ToList() }
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
    class ExtensionEditor : Component {
        Stack<Component> screens;
        Environment env;
        TranscendenceExtension extension;

        XElement focused;

        Input i;
        Suggest s;

        public ExtensionEditor(Stack<Component> screens, Environment env, TranscendenceExtension extension) {
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
            while(e != null) {
                ancestors.AddFirst(e);
                e = e.Parent;
            }
            int tabs = 0;
            foreach(XElement ancestor in ancestors) {
                Global.PrintLine($"{Tab()}<{ancestor.Name.LocalName}{ShowContextAttributes(ancestor)}>", ConsoleColor.White, ConsoleColor.Black);
                tabs++;
            }
            if(focused.ElementsBeforeSelf().Count() > 0) {
                Global.PrintLine($"{Tab()}...", ConsoleColor.White, ConsoleColor.Black);
            }


            //See if we need to print any children
            if(focused.HasElements) {
                Global.PrintLine($"{Tab()}<{focused.Name.LocalName}{ShowAttributes(focused)}>", ConsoleColor.Green, ConsoleColor.Black);
                tabs++;
                foreach (XElement child in focused.Elements()) {
                    Global.PrintLine($"{Tab()}<{child.Name.LocalName}/>", ConsoleColor.White, ConsoleColor.Black);
                }
                tabs--;
                Global.PrintLine($"{Tab()}</{focused.Name.LocalName}>", ConsoleColor.Green, ConsoleColor.Black);
            } else {
                Global.PrintLine($"{Tab()}<{focused.Name.LocalName}{ShowAttributes(focused)}/>", ConsoleColor.Green, ConsoleColor.Black);
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

                foreach(var key in new string[] { "unid"}) {
                    if(element.Att(key, out string value)) {
                        attributes[key] = value;
                    }
                }
                bool inline = attributes.Count > 1;
                bool more = attributes.Count < element.Attributes().Count();
                return AttributesToString(attributes, inline, more);
            }
            string ShowAttributes(XElement element) {
                Dictionary<string, string> attributes = new Dictionary<string, string>();
                foreach(var attribute in element.Attributes()) {
                    attributes[attribute.Name.LocalName] = attribute.Value;
                }
                bool inline = attributes.Count < 3;
                return AttributesToString(attributes, inline, false);
            }
            string AttributesToString(Dictionary<string,string> attributes, bool inline, bool more) {
                if (attributes.Count == 0) {
                    return "";
                }
                if (inline) {
                    StringBuilder result = new StringBuilder();
                    foreach (string key in attributes.Keys) {
                        result.Append(" ");
                        result.Append($@"{key}=""{attributes[key]}""");
                    }
                    if(more) {
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
                    if(more) {
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
                                    } else if(!string.IsNullOrEmpty(attribute)) {
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
                                    foreach(var extension in env.extensions.Values) {
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
                                        if(focused.Att(attribute, out string value)) {
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
    class Input : Component {
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
            if(k.KeyChar == 0) {
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
                switch(k.Key) {
                    case ConsoleKey.Backspace:
                        if((k.Modifiers & ConsoleModifiers.Control) != 0) {
                            //Make sure we have characters to delete
                            if(cursor == 0) {
                                break;
                            }
                            //If we are at a space, just delete it
                            if(s[cursor-1] == ' ') {
                                cursor--;
                                s.Remove(cursor, 1);
                            } else {
                                //Otherwise, delete characters until we reach a space
                                int length = 0;
                                while(cursor > 0 && s[cursor - 1] != ' ') {
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
                        if(cursor == s.Length) {
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
            for(int i = s.Length + 1; i < Console.WindowWidth; i++) {
                Print(' ', ConsoleColor.White, ConsoleColor.Black);
            }
        }
    }
    class Tooltip : Component {
        public void Draw() {
        }
        public void Handle(ConsoleKeyInfo k) {
        }
        public void Update() {
        }
    }
    class History : Component {
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
    class Suggest : Component {
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
            if(index == -1 && items.Count > 0) {
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
