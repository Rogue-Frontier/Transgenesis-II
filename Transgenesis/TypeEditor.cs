using SadConsole;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
namespace Transgenesis {
    class TypeEditor : IComponent {
        Stack<IComponent> screens;
        Environment env;
        TranscendenceExtension extension;
        ConsoleManager c;

        int elementIndex = 0;

        Input i;
        History h;
        Suggest s;
        Tooltip t;
        Scroller scroller;

        public TypeEditor(Stack<IComponent> screens, Environment env, TranscendenceExtension extension, ConsoleManager c) {
            this.screens = screens;
            this.env = env;
            this.extension = extension;
            this.c = c;

            i = new Input(c);
            h = new History(i);
            s = new Suggest(i, c);
            t = new Tooltip(i, s, c, new Dictionary<string, string>() {
                { "add",        "add <entity>\r\n" +
                                "Adds an entity to a type range, if selected." },
                { "remove",     "remove [entity...]\r\n" +
                                "Removes the specified entities from their entries. If no entity is specified, removes the currently selected entry." },
                { "type",       "type <entity> [unid]\r\n" +
                                "Creates a type with the given entity and unid. If the unid is omitted, then it is automatically assigned following the last assigned unid." },
                { "range",      "range [unidMin] [unidMax]\r\n" +
                                "Creates a range of types starting from unidMin and ending at unidMax with a modifiable list of entities. If unidMax is omitted, then the range is unlimited. If unidMin is omitted, then it is automatically assigned following the last assigned unid."},
                { "bind",       "bind\r\n" +
                                "Binds all entities to UNIDs and DesignTypes for this extension"},
                { "bindall",    "bindall\r\n" +
                                "Binds all entities to UNIDs and DesignTypes for all loaded extensions" },
                { "exit",       "exit\r\n" +
                                "Exits the Type Editor to the main menu"},
            });
            scroller = new Scroller(i, c);
        }
        public void Draw() {
            c.Clear();
            c.SetCursor(new Microsoft.Xna.Framework.Point(0, 0));

            List<ColoredString> buffer = new List<ColoredString>();
            AddLine($"    {"Entity",-32}{"UNID",-12}{"DesignType",-32}{"Extension", -32}"); //{entry.comment}

            AddLine("<!-- Types defined by this extension -->");
            string extensionName = extension.name ?? extension.entity ?? "<!--This Extension -->";
            int index = 0;
            foreach (TypeElement e in extension.types.elements) {
                Action<string> addLine = s => AddLine(s);
                if(index == elementIndex) {
                    addLine = s => AddHighlightLine(s);
                }

                if (e is TypeEntry entry) {
                    addLine($"    {entry.entity,-32}{entry.unid?.ToUNID() ?? "Auto",-12}{(extension.types.typemap.TryGetValue(entry.entity, out XElement design) ? design?.Tag() ?? "None" : "None"),-32}{extensionName,-32}"); //{entry.comment}
                } else if (e is TypeRange group) {
                    string range = $"{group.unid_min?.ToUNID() ?? "Auto"} -- {group.unid_max?.ToUNID() ?? "Auto"}";
                    addLine($"    <!-- {range} -->");

                    int rangeIndex = 0;
                    foreach (var entity in group.entities) {
                        addLine($"    {entity, -32}{(group.unid_min != null ? ((uint)(group.unid_min+rangeIndex)).ToUNID() : "Auto"), -12}{(extension.types.typemap.TryGetValue(entity, out XElement design) ? design?.Tag() ?? "None" : "None"),-32}{extensionName, -32}");
                        rangeIndex++;
                    }
                }
                index++;
            }

            index = 0;
            AddLine("<!-- Types overridden by this extension -->");
            foreach(var owned in extension.types.overriddenTypes) {
                AddLine($"    {owned,-32}{extension.types.unidmap[owned].ToUNID(),-16}{(extension.types.typemap.TryGetValue(owned, out XElement design) ? design?.Tag() ?? "None" : "None"),-32}{extensionName, -32}"); //{entry.comment}
                index++;
            }
            AddLine("<!-- Types defined by dependencies -->");
            foreach (var dependency in extension.types.typesByDependency.Keys) {
                string dependencyName = dependency.name ?? dependency.entity ?? dependency.path ?? dependency.structure.Tag();
                foreach (var entity in extension.types.typesByDependency[dependency]) {
                    AddLine($"    {entity,-32}{extension.types.unidmap[entity].ToUNID(),-16}{(extension.types.typemap.TryGetValue(entity, out XElement design) ? design?.Tag() ?? "None" : "None"),-32}{dependencyName, -32}"); //{entry.comment}
                }
            }

            void AddLine(string s) {
                buffer.Add(c.CreateString(s));
            }
            void AddHighlightLine(string s) {
                buffer.Add(c.CreateHighlightString(s));
            }
            scroller.Draw(buffer);

            i.Draw();
            s.Draw();
            t.Draw();
        }

        public void Handle(ConsoleKeyInfo k) {
            i.Handle(k);
            h.Handle(k);
            s.Handle(k);
            scroller.Handle(k);

            string input = i.Text;
            switch (k.Key) {
                //Allow Suggest/History to handle up/down arrows
                case ConsoleKey.DownArrow when i.Text.Length == 0:
                    elementIndex++;
                    elementIndex = Math.Min(elementIndex, extension.types.elements.Count() - 1);
                    break;
                case ConsoleKey.UpArrow when i.Text.Length == 0:
                    elementIndex--;
                    elementIndex = Math.Max(0, elementIndex);
                    break;
                case ConsoleKey.Enter: {
                        string[] parts = input.Split(' ');
                        switch (parts[0]) {
                            case "add": {
                                    if(parts.Length == 2) {
                                        var entity = parts[1];
                                        EntityCheck(entity);

                                        if(extension.types.elements.Count == 0) {
                                            //Must have type elements
                                        } else if(extension.types.elements[elementIndex] is TypeRange range) {

                                            if(range.size != null && range.entities.Count < range.size) {
                                                range.entities.Add(parts[1]);
                                            } else {
                                                //Range has reached entity limit
                                            }
                                        } else {
                                            //Must be selecting a type range
                                        }
                                    } else {
                                        //Expected entity
                                    }
                                    h.Record();
                                    break;
                                }
                            case "remove": {
                                    //Remove the entire entry
                                    if(parts.Length == 1) {
                                        if(elementIndex < extension.types.elements.Count) {
                                            extension.types.elements.RemoveAt(elementIndex);
                                        }
                                    } else {
                                        foreach(var entity in parts.Skip(1)) {
                                            for (int i = 0; i < extension.types.elements.Count; i++) {
                                                var element = extension.types.elements[i];
                                                if (element is TypeEntry entry && entry.entity == entity) {
                                                    extension.types.elements.RemoveAt(i);
                                                    goto Found;
                                                } else if (element is TypeRange range && range.entities.Contains(entity)) {
                                                    range.entities.Remove(entity);
                                                    goto Found;
                                                }
                                            }

                                            //Error: did not find entity

                                            break;
                                        Found:
                                            //Success
                                            break;
                                        }
                                    }
                                    h.Record();
                                    break;
                                }
                            case "type": {
                                    TypeEntry result;
                                    if (parts.Length == 3) {
                                        string entity = parts[1];
                                        EntityCheck(entity);
                                        foreach (var element in extension.types.elements) {
                                            if(element is TypeEntry entry && entry.entity == entity) {
                                                //Error: Conflict with type
                                                break;
                                            } else if(element is TypeRange range && range.entities.Contains(entity)) {
                                                //Error: Conflict with range
                                                break;
                                            }
                                        }


                                        if (uint.TryParse(parts[2], System.Globalization.NumberStyles.HexNumber, null, out uint unid)) {
                                            result = new TypeEntry(entity, unid);
                                        } else {
                                            //Return error: Invalid UNID

                                            break;
                                        }
                                    } else {
                                        string entity = parts[1];
                                        EntityCheck(entity);

                                        result = new TypeEntry(parts[1], null);
                                    }
                                    if(elementIndex+1 >= extension.types.elements.Count) {
                                        extension.types.elements.Add(result);
                                    } else {
                                        extension.types.elements.Insert(elementIndex + 1, result);
                                    }
                                    h.Record();
                                    break;
                                }
                            case "range": {
                                    TypeRange result;
                                    if (parts.Length == 3) {
                                        if (uint.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, null, out uint min)) {
                                            if (uint.TryParse(parts[2], System.Globalization.NumberStyles.HexNumber, null, out uint max)) {
                                                result = new TypeRange(min, max);
                                            } else {
                                                //Return error: Invalid max UNID

                                                break;
                                            }
                                        } else {
                                            //Return error: Invalid min UNID

                                            break;
                                        }
                                    } else if (parts.Length == 2) {
                                        if (uint.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, null, out uint min)) {
                                            result = new TypeRange(min, null);
                                        } else {
                                            //Return error: Invalid min UNID
                                            break;
                                        }
                                    } else {
                                        result = new TypeRange(null, null);
                                    }

                                    if (elementIndex + 1 >= extension.types.elements.Count) {
                                        extension.types.elements.Add(result);
                                    } else {
                                        extension.types.elements.Insert(elementIndex + 1, result);
                                    }
                                    h.Record();
                                    break;
                                }
                            case "bindall":
                                env.BindAll();
                                h.Record();
                                break;
                            case "bind":
                                extension.updateTypeBindings(env);
                                h.Record();
                                break;
                            case "exit":
                                screens.Pop();
                                h.Record();
                                break;
                        }
                        break;
                    }
                default: {

                        string[] parts = input.Split(' ');
                        if (parts.Length > 2) {
                            switch (parts[0]) {

                            }
                        } else {
                            //Disable suggest when input is completely empty so that we can navigate aroung the UI with arrow keys
                            if (input.Length == 0) {
                                s.SetItems(new List<HighlightEntry>());
                                break;
                            }

                            var empty = new List<string>();
                            Dictionary<string, Func<List<string>>> autocomplete = new Dictionary<string, Func<List<string>>> {
                                {"", () => new List<string>{"add", "type", "range", "bind", "bindall", "exit"} },

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

            //We need a function to check whether a new TypeEntry or TypeRange will collide with existing type elements.

            //Checks whether a new entity will collide with existing type elements
            void EntityCheck(string entity) {
                //Throw an exception
                foreach (var element in extension.types.elements) {
                    if (element is TypeEntry entry && entry.entity == entity) {
                        //Error: Conflict with type
                        break;
                    } else if (element is TypeRange range && range.entities.Contains(entity)) {
                        //Error: Conflict with range
                        break;
                    }
                }
            }
        }

        public void Update() {
        }
    }
}
