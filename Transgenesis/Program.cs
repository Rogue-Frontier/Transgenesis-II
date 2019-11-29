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
            Input i = new Input();
            Suggest s = new Suggest(i);
            Commander cc = new Commander(i, s);
            List<Component> l = new List<Component> {
                i, s, cc
            };
            Console.CursorVisible = false;
            bool draw = true;
            while (true) {
                if (Console.KeyAvailable) {
                    var k = Console.ReadKey(true);
                    l.ForEach(c => c.Handle(k));
                    draw = true;
                }
                l.ForEach(c => c.Update());
                if(draw) {
                    l.ForEach(c => c.Draw());
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

    }
    class Commander : Component {
        Input i;
        Suggest s;
        public XElement hierarchy;
        public Dictionary<string, XElement> coreStructures = new Dictionary<string, XElement>();
        public Dictionary<string, XElement> baseStructures = new Dictionary<string, XElement>();
        public Dictionary<XElement, XElement> bases = new Dictionary<XElement, XElement>();
        public HashSet<TranscendenceExtension> extensions = new HashSet<TranscendenceExtension>();

        public Commander(Input i, Suggest s) {
            this.i = i;
            this.s = s;

            XmlDocument doc = new XmlDocument();
            try {
                doc.Load("Transcendence.xml");
            } catch {
                doc.Load("../../../Transcendence.xml");
            }
            hierarchy = XElement.Parse(doc.OuterXml);
            baseStructures["Hierarchy"] = hierarchy;
            foreach(var coreStructure in hierarchy.Elements("E").Where(e => (string)e.Attribute("category") != "virtual")) {
                coreStructures[(string)coreStructure.Attribute("name")] = coreStructure;
                baseStructures[(string)coreStructure.Attribute("name")] = coreStructure;
            }
            foreach (var baseStructure in hierarchy.Elements("E").Where(e => (string)e.Attribute("category") == "virtual")) {
                baseStructures[(string)baseStructure.Attribute("id")] = baseStructure;
            }
        }
        public void Draw() {

            Global.SetCursor(0, 0);
            Console.WriteLine("Transgenesis II");
            Console.WriteLine();

            Console.WriteLine($"Extensions Loaded: {extensions.Count}");
            foreach(var e in extensions) {
                Console.WriteLine($"{e.structure.Name.LocalName}{e.path, 18}");
            }
        }

        public void Handle(ConsoleKeyInfo k) {
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
                                    structure = new XElement("TranscendenceExtension");
                                    template = coreStructures["TranscendenceExtension"];
                                    break;
                                case "library":
                                    structure = new XElement("TranscendenceLibrary");
                                    template = coreStructures["TranscendenceLibrary"];
                                    break;
                                case "adventure":
                                    structure = new XElement("TranscendenceAdventure");
                                    template = coreStructures["TranscendenceAdventure"];
                                    break;
                                case "module":
                                    structure = new XElement("TranscendenceModule");
                                    template = coreStructures["TranscendenceModule"];
                                    break;
                                default:
                                    goto Done;
                            }
                            template = Initialize(template);

                            string path = parts[2];

                            extension = new TranscendenceExtension() {
                                path = path,
                                structure = structure
                            };
                            extensions.Add(extension);
                            bases[structure] = template;

                            Done:
                            break;
                        case "load":
                            for(int i = 1; i < parts.Length; i++) {

                            }
                            break;
                        case "edit":

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
                        {"edit", () => extensions.Select(e => e.path).ToList() }
                    };
            string str = autocomplete.Keys.Last(prefix => input.StartsWith((prefix + " ").TrimStart()));
            List<string> all = autocomplete[str]();

            var items = Global.GetSuggestions(input.Substring(str.Length).TrimStart(), all);


            s.SetItems(items);

        }

        public void Update() {
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
                foreach (string part in parts.Skip(1)) {
                    //template = template.Element(part);
                    template = template.Elements("E").First(e => e.Attribute("name")?.Value == part);
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
    class Input : Component {
        private StringBuilder s = new StringBuilder();
        public int cursor = 0;
        public string Text {
            get => s.ToString();
            set {
                s.Clear();
                s.Append(value);
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
                        s.Insert(cursor, k.KeyChar);
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
                    if (item.highlightLength == item.str.Length)
                        break;

                    string input = i.Text;
                    string selected = options[index].str;
                    i.Text = input.Substring(0, input.Length - item.highlightLength - 1) + selected;
                    i.cursor = i.Text.Length;
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
