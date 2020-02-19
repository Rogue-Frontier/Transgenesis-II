using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
namespace Transgenesis {
    class MainMenu : IComponent {
        ProgramState state;
        Stack<IComponent> screens;
        Input i;
        Suggest s;
        Tooltip t;
        Environment env;
        ConsoleManager c;



        public MainMenu(Stack<IComponent> screens) {
            c = new ConsoleManager(new Point(0, 0));
            i = new Input(c);
            s = new Suggest(i, c);
            t = new Tooltip(i, s, c, new Dictionary<string, string>() {
                { "types",  "types <extensionFile>\r\n" +
                            "Opens the Type Editor on the loaded extension with the specified file path" },
                {"theme",   "theme <blue|green|pine|orange|default>\r\n" +
                            "Sets the color scheme of the console"},
                {"create",  "create <extensionType> <file>\r\n" +
                            "Creates a new Transcendence extension with the specified file path."},
                {"load",    "load <extensionFile|extensionFolder>\r\n" +
                            "Loads an extension with the specified file path" },
                {"unload",  "unload <extensionFile>\r\n" +
                            "Unloads a loaded extension of the specified file path"},
                {"edit",    "edit <extensionFile>\r\n" +
                            "Opens the XML Editor on the loaded extension with the specified file path." },
                {"open",    "load <extensionFile>\r\n" +
                            "Loads an extension with the specified file path, and then opens the XML Editor on it."},
                {"reload",  "reload <extensionFile>\r\n" +
                            "Unloads and loads an extension at the given path" },
                {"reloadmodules", "reloadmodules <extensionFile>\r\n" +
                            "Unloads and loads the loaded extension at the specified file path along with all of its modules" },
                {"reloadall", "reloadall\r\n" +
                            "Unloads and loads all currently loaded extensions"},
                {"reloadallmodules", "reloadallmodules\r\n" +
                            "Unloads and loads all currently loaded extensions along with all of their modules" },
                {"loadmodules", "loadmodules <extensionFile|extensionFolder>\r\n" +
                            "Loads an extension at the specified file path along with all of its modules"}
            });
            this.env = new Environment();
            this.screens = screens;
            this.state = new ProgramState();
        }
        public void Draw() {
            c.Clear();
            c.SetCursor(new Point(0, 0));
            c.WriteLine("Transgenesis II");
            c.NextLine();

            c.WriteLine($"Extensions Loaded: {env.extensions.Count}");
            foreach (var e in env.extensions.Values) {
                if(e.structure.Att("name", out string name) || (e.parent != null && e.parent.structure.Att("name", out name))) {
                    name = $@"""{name}""";
                } else {
                    name = "";
                }
                c.WriteLine($"{e.structure.Name.LocalName,-24}{name,-32}{e.path}");
            }

            i.Draw();
            s.Draw();
            t.Draw();
        }

        public void Handle(ConsoleKeyInfo k) {
            i.Handle(k);
            s.Handle(k);
            string input = i.Text;
            switch (k.Key) {
                case ConsoleKey.Enter:
                    string command = i.Text;
                    i.Clear();
                    s.Clear();
                    string[] parts = command.Split(' ');
                    switch (parts.First().ToLower()) {
                        case "theme": {
                                var theme = c.theme;
                                if (parts.Length == 1) {
                                    Reset();
                                    break;
                                }
                                switch (parts[1]) {
                                    case "blue":
                                        theme.front = new Color(0x00, 0x69, 0xE7);
                                        theme.back = Color.Black;
                                        theme.highlight = Color.White;
                                        break;
                                    case "green":
                                        theme.front = new Color(0xA8, 0xB7, 0x0E);
                                        theme.back = Color.Black;
                                        theme.highlight = Color.LightBlue;
                                        break;
                                    case "pine":
                                        theme.front = new Color(0x00, 0x76, 0x6B);
                                        theme.back = Color.Black;
                                        theme.highlight = Color.Magenta;
                                        break;
                                    case "orange":
                                        theme.front = new Color(0xFF, 0x92, 0x07);
                                        theme.back = Color.Black;
                                        theme.highlight = Color.White;
                                        break;
                                    default:
                                        Reset();
                                        break;
                                }
                                void Reset() {
                                    theme.front = Color.White;
                                    theme.back = Color.Black;
                                    theme.highlight = Color.Green;
                                }
                                break;
                            }
                        case "create": {
                                if (parts.Length < 3) {
                                    break;
                                }
                                if (Enum.TryParse(parts[1], out ExtensionTypes ex)) {
                                    //Always use full-path so that we can easily find this
                                    env.CreateExtension(ex, Path.GetFullPath(parts[2]));
                                }
                                break;
                            }
                        case "unload": {
                                if (parts.Length == 1) {
                                    break;
                                }
                                string path = Path.GetFullPath(string.Join(" ", parts.Skip(1)).Trim());
                                if (env.extensions.TryGetValue(path, out TranscendenceExtension existing)) {
                                    env.Unload(existing);
                                }
                                break;
                            }
                        case "reloadall": {
                                var extensions = new List<TranscendenceExtension>(env.extensions.Values);
                                foreach (var e in extensions) {
                                    env.Unload(e);
                                }
                                foreach(var e in extensions) {
                                    Load(e.path);
                                }
                                break;
                            }
                        case "reloadallmodules": {
                                var extensions = env.extensions.Values;
                                foreach (var e in extensions) {
                                    env.Unload(e);
                                }
                                foreach(var e in extensions) {
                                    //If this extension was already reloaded due to a parent extension, skip it
                                    if(env.extensions.ContainsKey(e.path)) {
                                        continue;
                                    }
                                    Load(e.path, true);
                                }
                                break;
                            }
                        case "reload": {
                                if (parts.Length == 1) {
                                    break;
                                }
                                string path = Path.GetFullPath(string.Join(" ", parts.Skip(1)).Trim());
                                if (env.extensions.TryGetValue(path, out TranscendenceExtension existing)) {
                                    env.Unload(existing);
                                }
                                Load(path);
                                break;
                            }
                        case "reloadmodules": {
                                if (parts.Length == 1) {
                                    break;
                                }
                                string path = Path.GetFullPath(string.Join(" ", parts.Skip(1)).Trim());
                                if (env.extensions.TryGetValue(path, out TranscendenceExtension existing)) {
                                    env.Unload(existing);
                                    var modules = env.extensions.Values.Where(e => e.parent == existing);
                                    foreach (var module in modules) {
                                        env.Unload(module);
                                    }
                                }
                                LoadFolder(path, true);
                                break;
                            }
                        case "load": {
                                if (parts.Length == 1) {
                                    break;
                                }
                                string path;
                                /*
                                if(parts.Count() == 1) {
                                    using (OpenFileDialog openFileDialog = new OpenFileDialog()) {
                                        openFileDialog.InitialDirectory = "c:\\";
                                        openFileDialog.Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*";
                                        openFileDialog.FilterIndex = 2;
                                        openFileDialog.RestoreDirectory = true;

                                        if (openFileDialog.ShowDialog() == DialogResult.OK) {
                                            //Get the path of specified file
                                            path = openFileDialog.FileName;
                                        } else {
                                            break;
                                        }
                                    }
                                } else {
                                    path = Path.GetFullPath(string.Join(" ", parts.Skip(1)).Trim());
                                }
                                */
                                path = Path.GetFullPath(string.Join(" ", parts.Skip(1)).Trim());
                                //Don't reload the extension
                                if (env.extensions.TryGetValue(path, out TranscendenceExtension existing)) {
                                    //env.Unload(existing);
                                } else {
                                    //Global.Break();

                                    //Note: This starts a new thread. We should indicate some way that this is running
                                    Task.Run(() => LoadFolder(path));

                                }
                                
                                break;
                            }
                        case "loadmodules": {
                                if (parts.Length == 1) {
                                    break;
                                }
                                string path = Path.GetFullPath(string.Join(" ", parts.Skip(1)).Trim());
                                //Don't reload the extension
                                if (env.extensions.TryGetValue(path, out TranscendenceExtension existing)) {
                                    //env.Unload(existing);
                                    LoadModules(existing);
                                } else {
                                    LoadFolder(path, true);
                                }
                                

                                break;
                            }
                        case "open": {
                                if(parts.Length == 1) {
                                    break;
                                }

                                string path = Path.GetFullPath(string.Join(" ", parts.Skip(1)).Trim());
                                //Don't reload the extension
                                if (env.extensions.TryGetValue(path, out TranscendenceExtension existing)) {
                                    //env.Unload(existing);
                                } else {
                                    LoadFolder(path);
                                }
                                if (env.extensions.TryGetValue(path, out TranscendenceExtension result)) {
                                    screens.Push(state.sessions.Initialize(result, new ElementEditor(state, screens, env, result, c)));
                                }

                                break;
                            }
                        case "edit": {
                                if (parts.Length == 1) {
                                    break;
                                }
                                string path = Path.GetFullPath(string.Join(" ", parts.Skip(1)).Trim());
                                //Global.Break();
                                if (env.extensions.TryGetValue(path, out TranscendenceExtension result)) {
                                    screens.Push(state.sessions.Initialize(result, new ElementEditor(state, screens, env, result, c)));
                                }
                                break;
                            }
                        case "types": {
                                if (parts.Length == 1) {
                                    break;
                                }
                                string path = Path.GetFullPath(string.Join(" ", parts.Skip(1)).Trim());
                                if (env.extensions.TryGetValue(path, out TranscendenceExtension result)) {
                                    screens.Push(new TypeEditor(screens, env, result, c));
                                }
                                break;
                            }
                    }
                    break;
                //Allow Suggest/History to handle up/down arrows
                case ConsoleKey.UpArrow:
                case ConsoleKey.DownArrow:
                    break;
                default:
                    //Disable suggest when input is completely empty so that we can navigate aroung the UI with arrow keys
                    if (input.Length == 0) {
                        s.SetItems(new List<HighlightEntry>());
                        break;
                    }

                    Dictionary<string, Func<List<string>>> autocomplete = new Dictionary<string, Func<List<string>>> {
                        {"", () => new List<string>{ "types", "theme", "create", "load", "unload", "edit", "open", "reload", "reloadmodules", "reloadall", "reloadallmodules", "loadmodules" } },
                        {"theme", () => new List<string>{ "blue", "green", "pine", "orange", "default"} },
                        {"create", () => new List<string>{ "TranscendenceAdventure", "TranscendenceExtension", "TranscendenceLibrary", "TranscendenceModule" } },
                        {"load", () => Directory.GetFiles(Directory.GetCurrentDirectory(), "*.xml").ToList() },
                        {"loadmodules", () => Directory.GetFiles(Directory.GetCurrentDirectory(), "*.xml").ToList() },
                        {"unload", () => env.extensions.Keys.ToList() },
                        {"edit", () => env.extensions.Keys.ToList() },
                        {"types", () => env.extensions.Keys.ToList() },
                        {"open", () => Directory.GetFiles(Directory.GetCurrentDirectory(), "*.xml").ToList() },
                        {"reload", () => env.extensions.Keys.ToList() },
                        {"reloadmodules", () => env.extensions.Keys.ToList() },
                    };
                    string str = autocomplete.Keys.Last(prefix => input.StartsWith((prefix + " ").TrimStart()));
                    List<string> all = autocomplete[str]();

                    var items = Global.GetSuggestions(input.Substring(str.Length).TrimStart(), all);
                    s.SetItems(items);

                    break;
            }
            t.Handle(k);



            void LoadFolder(string path, bool modules = false) {
                if(Directory.Exists(path)) {
                    var files = Directory.GetFiles(path);
                    foreach (var subpath in files) {
                        LoadFolder(subpath, modules);
                    }

                    var directories = Directory.GetDirectories(path);
                    foreach (var subpath in directories) {
                        LoadFolder(subpath, modules);
                    }
                }
                if(File.Exists(path) && Path.GetExtension(path)==".xml") {
                    Load(path, modules);
                }
            }
            void Load(string path, bool modules = false) {
                string xml = File.ReadAllText(path);
                //Cheat the XML reader by escaping ampersands so we don't parse entities
                xml = xml.Replace("&", "&amp;");
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);

                env.LoadExtension(doc, path, out TranscendenceExtension e);
                if(modules) {
                    LoadModules(e);
                }
            }
            void LoadModules(TranscendenceExtension e) {
                foreach (var module in e.structure.Elements()) {
                    if (module.Tag() == "Module" || module.Tag() == "CoreLibrary") {
                        string filename = module.Att("filename");
                        string path = Path.Combine(Directory.GetParent(e.path).FullName, filename);
                        Load(path, true);
                    }
                }
            }
        }

        public void Update() {
            i.Update();
            s.Update();
        }

    }
}
