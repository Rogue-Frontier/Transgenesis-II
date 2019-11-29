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

namespace Transgenesis {
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
        public static List<string> GetAddableElements(XElement element, XElement template) {
            return template.Elements("E").Where(subtemplate => CanAddElement(element, subtemplate)).Select(subtemplate => subtemplate.Att("name")).ToList();
        }
        public static List<string> GetRemovableElements(XElement element, XElement template) {
            return template.Elements("E").Where(subtemplate => CanRemoveElement(element, subtemplate)).Select(subtemplate => subtemplate.Att("name")).ToList();
        }
        public XElement FromTemplate(XElement template) {
            XElement result = new XElement(template.Att("name"));
            foreach(XElement subtemplate in template.Elements("E").Where(e => e.Att("category") == "1" || e.Att("category") == "+")) {
                var initialized = Initialize(subtemplate);
                result.Add(FromTemplate(initialized));
            }
            return result;
        }
        //Initializes a template for actual use, handling inheritance
        public XElement Initialize(XElement original) {

            XElement result = new XElement(original.Name);
            if (original.Att("inherit", out string from)) {
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
                XElement template = baseStructures[source];

                //Handle the inheritance chain
                template = Initialize(template);

                foreach (string part in parts.Skip(1)) {
                    //template = template.Element(part);
                    template = template.Elements("E").First(e => e.Att("name") == part);

                    //Handle the inheritance chain
                    template = Initialize(template);
                }
                //Inherit base attributes
                foreach (var a in template.Attributes()) {
                    result.SetAttributeValue(a.Name, a.Value);
                }
                //Inherit base elements
                foreach (var e in template.Elements()) {
                    result.Add(e);
                }
            }
            //Handle additional/overriding attributes
            foreach (var a in original.Attributes()) {
                result.SetAttributeValue(a.Name, a.Value);
            }
            //Handle additional/overriding elements
            foreach (var e in original.Elements()) {
                if (result.NameElement(e.Att("name"), out XElement replaced)) {
                    replaced.ReplaceWith(e);
                } else {
                    result.Add(e);
                }
            }
            return result;
        }
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

            Global.SetCursor(0, 0);
            Console.WriteLine("Transgenesis II");
            Console.WriteLine();

            Console.WriteLine($"Extensions Loaded: {env.extensions.Count}");
            foreach(var e in env.extensions.Values) {
                Console.WriteLine($"{e.structure.Name.LocalName}{e.path, 18}");
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
                            XElement structure;
                            XElement template;
                            TranscendenceExtension extension;
                            switch(parts[1].ToLower()) {
                                case "extension":
                                    template = env.coreStructures["TranscendenceExtension"];
                                    break;
                                case "library":
                                    template = env.coreStructures["TranscendenceLibrary"];
                                    break;
                                case "adventure":
                                    template = env.coreStructures["TranscendenceAdventure"];
                                    break;
                                case "module":
                                    template = env.coreStructures["TranscendenceModule"];
                                    break;
                                default:
                                    goto Done;
                            }
                            template = env.Initialize(template);
                            structure = env.FromTemplate(template);

                            string path = parts[2];

                            extension = new TranscendenceExtension(
                                path: path,
                                structure: structure
                            );
                            env.extensions[path] = extension;
                            env.bases[structure] = template;

                            Done:
                            break;
                        case "load":
                            for(int i = 1; i < parts.Length; i++) {

                            }
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

                default:
                    break;
            }
            Dictionary<string, Func<List<string>>> autocomplete = new Dictionary<string, Func<List<string>>> {
                        {"", () => new List<string>{ "create", "load", "edit" } },
                        {"create", () => new List<string>{ "adventure", "extension", "library", "module" } },
                        {"load", () => Directory.GetFiles(Directory.GetCurrentDirectory(), "*.xml").ToList() },
                        {"edit", () => env.extensions.Keys.ToList() }
                    };
            string str = autocomplete.Keys.Last(prefix => input.StartsWith((prefix + " ").TrimStart()));
            List<string> all = autocomplete[str]();

            var items = Global.GetSuggestions(input.Substring(str.Length).TrimStart(), all);
            s.SetItems(items);

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
            Console.WriteLine(extension.structure.ToString());

            i.Draw();
            s.Draw();
        }

        public void Handle(ConsoleKeyInfo k) {
            i.Handle(k);
            s.Handle(k);

            string input = i.Text;

            switch (k.Key) {
                case ConsoleKey.Spacebar:

                    if(input.Length > 0) {
                        switch(input[0]) {
                            case char c when c >= 'a' && c <= 'z':
                                break;
                            case char c when c >= 'A' && c <= 'Z':
                                break;
                            case '&':

                                break;
                        }
                    }
                    break;
            }
            string[] parts = input.Split(' ');
            if(parts.Length == 3) {
                switch(parts[0]) {
                    case "set": {
                            string attribute = parts[1];
                            //Calculate values for attribute

                            //var items = Global.GetSuggestions(input.Substring(p.Length).TrimStart(), all);
                            //s.SetItems(items);
                            break;
                        }
                }
            } else {
                Dictionary<string, Func<List<string>>> autocomplete = new Dictionary<string, Func<List<string>>> {
                        {"", () => new List<string>{ "set", "add", "remove" } },
                        {"set", () => env.bases[focused].GetValidAttributes() },
                        {"add", () => Environment.GetAddableElements(focused, env.bases[focused]) },
                        {"remove", () => Environment.GetRemovableElements(focused, env.bases[focused]) }
                    };
                string p = autocomplete.Keys.Last(prefix => input.StartsWith((prefix + " ").TrimStart()));
                List<string> all = autocomplete[p]();

                var items = Global.GetSuggestions(input.Substring(p.Length).TrimStart(), all);
                s.SetItems(items);
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
                    case ConsoleKey.LeftArrow:
                        if (cursor > 0) {
                            cursor--;
                        }
                        break;
                    case ConsoleKey.RightArrow:
                        if (cursor < s.Length) {
                            cursor++;
                        }
                        break;
                }
            } else {
                switch(k.Key) {
                    case ConsoleKey.Backspace:
                        if (s.Length > 0) {
                            s.Remove(cursor - 1, 1);
                            cursor--;
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
    class Suggest : Component {
        Input i;
        int index = -1;
        public List<HighlightEntry> options;
        public Point pos = new Point(0, 25);
        public Suggest(Input i) {
            this.i = i;
            options = new List<HighlightEntry>();
        }
        public Suggest(Input i, List<HighlightEntry> options) {
            this.i = i;
            this.options = options;
        }
        public void SetItems(List<HighlightEntry> items) {
            options = items;
            index = Math.Min(index, items.Count - 1);
            if(index == -1 && items.Count > 0) {
                index = 0;
            }
        }
        public void Clear() {
            options.Clear();
            index = -1;
        }
        public void Update() {

        }
        public void Handle(ConsoleKeyInfo k) {
            switch (k.Key) {
                case ConsoleKey.UpArrow:
                    if (index > -1)
                        index--;
                    break;
                case ConsoleKey.DownArrow:
                    if (index + 1 < options.Count)
                        index++;
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
                    var item = options[index];
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
            for(int i = 0; i < options.Count; i++) {
                var o = options[i];
                if (index == i) {
                    o.Draw(ConsoleColor.Black, ConsoleColor.White);
                } else {
                    o.Draw(ConsoleColor.White, ConsoleColor.Black);
                }
                ClearAhead();
                Console.WriteLine();
            }
            
            ClearBelow(pos.Y + 6);
        }
    }

}
