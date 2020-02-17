using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
namespace Transgenesis {
    class TypeEditor : IComponent {
        Stack<IComponent> screens;
        Environment env;
        TranscendenceExtension extension;
        XElement focused;
        ConsoleManager c;

        Input i;
        Suggest s;

        public TypeEditor(Stack<IComponent> screens, Environment env, TranscendenceExtension extension, ConsoleManager c) {
            this.screens = screens;
            this.env = env;
            this.extension = extension;
            this.focused = extension.structure;
            this.c = c;

            i = new Input(c);
            s = new Suggest(i, c);
        }
        public void Draw() {
            Console.Clear();

            foreach (TypeElement e in extension.types.elements) {
                if (e is TypeRange range) {
                    //Display unid range
                } else if (e is TypeGroup group) {
                    //TO DO: If entity is bound, display unid and design
                    //Console.WriteLine(group.comment);
                    foreach (var s in group.entities) {
                        Console.WriteLine($"{s}{(extension.typemap.TryGetValue(s, out XElement design) ? design.Tag() : ""),-32}");
                    }
                } else if (e is TypeEntry entry) {
                    Console.WriteLine($"{entry.entity,-32}{entry.unid,-32}{(extension.typemap.TryGetValue(entry.entity, out XElement design) ? design.Tag() : ""),-32}"); //{entry.comment}
                } else if (e is Type entity) {
                    Console.WriteLine($"{entity.entity,-32}{(extension.typemap.TryGetValue(entity.entity, out XElement design) ? design.Tag() : ""),-32}"); //{entity.comment}
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
}
