using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using SadConsole;
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
        History h;
        Suggest s;
        Tooltip t;
        Environment env;
        ConsoleManager c;
        Scroller scroller;

        List<ConsoleKeyInfo> busyQueue;

        string extensionsFolder;



        public MainMenu(Stack<IComponent> screens) {
            c = new ConsoleManager(new Point(0, 0));
            i = new Input(c);
            h = new History(i);
            s = new Suggest(i, c);
            t = new Tooltip(i, s, c, new Dictionary<string, string>() {
                {"",    "General controls" + "\r\n" +
                        "-Up          Suggest selection up" + "\r\n" +
                        "-Down        Suggest selection up" + "\r\n" +
                        "-Space       Enter Suggest selection" + "\r\n" +
                        "-Shift+Up    History selection up" + "\r\n" +
                        "-Shift+Down  History selection down" + "\r\n" +
                        "-PageUp      Scroll up" + "\r\n" +
                        "-PageDown    Scroll down" + "\r\n" +
                        "-Return      Run command" + "\r\n" +
                        "-[Typing]    Input command" + "\r\n"},
                { "types",  "types <extensionFile>\r\n" +
                            "Opens the Type Editor on the loaded extension with the specified file path" },
                {"theme",   "theme <blue|green|pine|orange|default>\r\n" +
                            "Sets the color scheme of the console"},
                {"create",  "create <extensionType> <file>\r\n" +
                            "Creates a new Transcendence extension with the specified file path."},
                {"bindall", "bindall\r\n" +
                            "Binds all loaded extensions" },
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
            this.scroller = new Scroller(i, c);
            this.env = new Environment();
            this.screens = screens;
            this.state = new ProgramState();


            if(File.Exists("Settings.json")) {
                var f = File.ReadAllText("Settings.json");
                var settings = JsonConvert.DeserializeObject<Dictionary<string,string>>(f);
                extensionsFolder = settings["ExtensionsFolder"];
            } else {
                extensionsFolder = @"C:\Users\alexm\OneDrive\Documents\Transcendence";
            }
        }
        public void Draw() {
            c.Clear();
            c.SetCursor(new Point(0, 0));
            List<ColoredString> buffer = new List<ColoredString>();
            buffer.Add(c.CreateString("Transgenesis II"));

            buffer.Add(c.CreateString($"Extensions Loaded: {env.extensions.Count}"));
            var ext = new List<TranscendenceExtension>(env.extensions.Values);

            Comparison<TranscendenceExtension> modulesUnderParent = (TranscendenceExtension t1, TranscendenceExtension t2) => {
                if (t1 == t2.parent) {
                    return -1;
                } else if (t1.parent == t2) {
                    return 1;
                } else if (t1.parent != null && t2.parent != null) {
                    if (t1.parent == t2.parent) {
                        return Compare(t1, t2);
                    } else {
                        return Compare(t1.parent, t2.parent);
                    }
                } else if (t1.parent != null) {
                    return Compare(t1.parent, t2);
                } else if (t2.parent != null) {
                    return -Compare(t2.parent, t1);
                } else {
                    return Compare(t1, t2);
                }
            };
            Comparison<TranscendenceExtension> modulesLast = (TranscendenceExtension t1, TranscendenceExtension t2) => {
                if (t1.parent != null && t2.parent != null) {
                    if (t1.parent == t2.parent) {
                        return Compare(t1, t2);
                    } else if (t1 == t2.parent) {
                        return -1;
                    } else if (t1.parent == t2) {
                        return 1;
                    } else {
                        return Compare(t1.parent, t2.parent);
                    }
                } else if (t1.parent != null) {
                    return Compare(t1.parent, t2);
                } else if (t2.parent != null) {
                    return -Compare(t2.parent, t1);
                } else {
                    return Compare(t1, t2);
                }
            };
            int Compare(TranscendenceExtension e1, TranscendenceExtension e2) {
                return e1.unid != null && e2.unid != null && e1.unid != e2.unid ? ((uint)e1.unid).CompareTo((uint)e2.unid) :
                        e1.name != null && e2.name != null && e1.name != e2.name ? e1.name.CompareTo(e2.name) :
                        e1.type != null && e2.type != null && e1.type != e2.type ? ((ExtensionTypes)e1.type).CompareTo((ExtensionTypes)e2.type) :
                        e1.path.CompareTo(e2.path);
            }
            ext.Sort(modulesUnderParent);

            var orphans = new TranscendenceExtension(null, null);
            var modulesByExtension = (from mod in ext group mod by mod.parent into groups select groups).ToDictionary(group => group.ToList()[0].parent ?? orphans, gdc => gdc.ToList());

            ModuleMode moduleMode = ModuleMode.HideBaseGame;
            foreach(var e in ext) {
                if(e.type == ExtensionTypes.TranscendenceModule && e.parent != null &&
                    (moduleMode == ModuleMode.HideParentedModules ||
                        (moduleMode == ModuleMode.HideBaseGame && IsBaseGame(e)))
                    ) {
                    
                    continue;
                }
                

                string tag = $"{e.structure.Tag(),-24}";

                string unsaved = e.isUnsaved() ? "[S] " : "    ";
                string unbound = e.isUnbound() ? "[B] " : "    ";
                buffer.Add(c.CreateString($"{unsaved}{unbound}{tag}{e.unid?.ToUNID() ?? "Unknown",-12}{e.name,-48}{e.path}"));

                if (moduleMode != ModuleMode.HideNone && modulesByExtension.ContainsKey(e)) {
                    buffer.Add(c.CreateString($"        Modules: {modulesByExtension[e].Count}"));
                }
            }

            bool IsBaseGame(TranscendenceExtension e) {
                var unid = e.unid ?? e.parent?.unid;
                return unid != null && unid < 0xA0000000;
            }
            /*
            if(hideModules && modulesByExtension.ContainsKey(orphans)) {
                buffer.Add(c.CreateString($"Orphan Modules: {modulesByExtension[orphans].Count}"));
            }
            */

            scroller.Draw(buffer);

            if(busyQueue != null) {
                c.NextLine();
                c.WriteLine("Loading extensions...");
            }

            i.Draw();
            s.Draw();
            t.Draw();
        }

        public void Handle(ConsoleKeyInfo k) {
            if(busyQueue != null) {
                busyQueue.Add(k);
                return;
            }

            i.Handle(k);
            h.Handle(k);
            s.Handle(k);
            scroller.Handle(k);
            string input = i.Text;
            switch (k.Key) {
                case ConsoleKey.Enter:
                    string command = i.Text;
                    string[] parts = command.Split(' ');
                    switch (parts.First().ToLower()) {
                        case "theme": {
                                var theme = c.theme;
                                if (parts.Length == 1) {
                                    h.Record();
                                    Reset();
                                    break;
                                }
                                switch (parts[1]) {
                                    case "blue":
                                        theme.front = new Color(0x00, 0x69, 0xE7);
                                        theme.back = Color.Black;
                                        theme.highlight = Color.White;
                                        h.Record();
                                        break;
                                    case "green":
                                        theme.front = new Color(0xA8, 0xB7, 0x0E);
                                        theme.back = Color.Black;
                                        theme.highlight = Color.LightBlue;
                                        h.Record();
                                        break;
                                    case "pine":
                                        theme.front = new Color(0x00, 0x76, 0x6B);
                                        theme.back = Color.Black;
                                        theme.highlight = Color.Magenta;
                                        h.Record();
                                        break;
                                    case "orange":
                                        theme.front = new Color(0xFF, 0x92, 0x07);
                                        theme.back = Color.Black;
                                        theme.highlight = Color.White;
                                        h.Record();
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
                        case "bindall": {
                                env.BindAll();
                                h.Record();
                                break;
                            }
                        case "unload": {
                                if (parts.Length == 1) {
                                    break;
                                }
                                string path = Path.GetFullPath(string.Join(" ", parts.Skip(1)).Trim());
                                if (env.extensions.TryGetValue(path, out TranscendenceExtension existing)) {
                                    env.Unload(existing);
                                    h.Record();
                                } else {
                                    i.Clear();
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
                                h.Record();
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
                                h.Record();
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
                                h.Record();
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
                                h.Record();
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
                                    Task.Run(() => {
                                        busyQueue = new List<ConsoleKeyInfo>();
                                        LoadFolder(path);
                                        busyQueue = null;
                                    });
                                    //LoadFolder(path);
                                }
                                h.Record();

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
                                h.Record();

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
                                h.Record();
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
                                    h.Record();
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
                                    h.Record();
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
                        {"", () => new List<string>{ "types", "theme", "create", "bindall", "load", "unload", "edit", "open", "reload", "reloadmodules", "reloadall", "reloadallmodules", "loadmodules" } },
                        {"theme", () => new List<string>{ "blue", "green", "pine", "orange", "default"} },
                        {"create", () => new List<string>{ "TranscendenceAdventure", "TranscendenceExtension", "TranscendenceLibrary", "TranscendenceModule" } },
                        {"load", GetFiles },
                        {"loadmodules", GetFiles },
                        {"unload", GetExtensions },
                        {"edit", GetExtensions },
                        {"types", GetExtensions },
                        {"open", GetFiles },
                        {"reload", GetExtensions },
                        {"reloadmodules", GetExtensions },
                    };
                    string str = autocomplete.Keys.Last(prefix => input.StartsWith((prefix + " ").TrimStart()));
                    List<string> all = autocomplete[str]();

                    var items = Global.GetSuggestions(input.Substring(str.Length).TrimStart(), all);
                    s.SetItems(items);

                    List<string> GetExtensions() {
                        return env.extensions.Keys.ToList();
                    }
                    List<string> GetFiles() {
                        var result = new List<string>();

                        var path = string.Join(" ", i.Text.Split(' ').Skip(1));
                        if(path.Length > 0) {
                            path = Path.GetFullPath(path);
                        } else {
                            goto Done;
                        }
                        if (Directory.Exists(path)) {
                            result.Add(path);
                            foreach (var file in Directory.GetFiles(path, "*.xml")) {
                                result.Add(file);
                            }
                            foreach (var dir in Directory.GetDirectories(path)) {
                                result.Add(dir + Path.DirectorySeparatorChar); //Add path separator to the end to speed up autocomplete navigation
                            }
                        } else if(File.Exists(path)) {
                            /*
                            result.Remove(path);
                            result.Insert(0, path);
                            */
                            result.Add(path);
                        } else {
                            var dir = Path.GetDirectoryName(path);
                            if (Directory.Exists(dir)) {
                                result.Add(dir);
                                foreach (var file in Directory.GetFiles(dir, "*.xml")) {
                                    result.Add(file);
                                }
                                foreach (var subdir in Directory.GetDirectories(dir)) {
                                    result.Add(subdir + Path.DirectorySeparatorChar); //Add path separator to the end to speed up autocomplete navigation
                                }
                            }
                        }
                    Done:
                        result.Add(extensionsFolder + Path.DirectorySeparatorChar);
                        result.AddRange(Directory.GetFiles(extensionsFolder, "*.xml").ToList());

                        //Add current directory files last
                        /*
                        result.Add(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar);
                        result.AddRange(Directory.GetFiles(Directory.GetCurrentDirectory(), "*.xml").ToList());
                        */
                        return result;
                    }

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
                e.updateTypeBindingsWithModules(env);
            }
            void LoadModules(TranscendenceExtension e) {
                foreach (var module in e.structure.Elements()) {
                    if (module.Tag() == "Module" || module.Tag() == "CoreLibrary" || module.Tag() == "TranscendenceAdventure") {
                        string filename = module.Att("filename");
                        //Use the full path when finding modules
                        string path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(e.path), filename));
                        Load(path, true);
                    }
                }
            }
        }
        public void Update() {
        }

    }
}
