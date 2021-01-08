using Newtonsoft.Json;
using SadConsole;
using System.Linq;
using System.Threading.Tasks;
using SadRogue.Primitives;
using System.Collections.Generic;
using System.IO;
using System;
using System.Diagnostics;

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

        string extensionsFolder;
        Task loading;

        public MainMenu(Stack<IComponent> screens, Environment env) {
            this.state = new ProgramState();
            this.screens = screens;
            this.env = env;
            this.c = new ConsoleManager(new Point(0, 0));
            this.scroller = new Scroller(c, i);
            
            i = new Input(c);
            h = new History(i, c);
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
                            "Loads an extension at the specified file path along with all of its modules"},
                {"exit",        "exit\r\n" +
                            "Exits the current session"}
            });

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
            buffer.Add(c.Color("Transgenesis II"));

            buffer.Add(c.Color($"Extensions Loaded: {env.extensions.Count}"));
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

                var unid =     (e1.unid != null) && (e2.unid != null) && (e1.unid != e2.unid);
                var name =     (e1.name != null) && (e2.name != null) && (e1.name != e2.name);
                var category = (e1.type != null) && (e2.type != null) && (e1.type != e2.type);
                return unid ? ((uint)e1.unid).CompareTo((uint)e2.unid) :
                        name ? e1.name.CompareTo(e2.name) :
                        category ? ((ExtensionTypes)e1.type).CompareTo((ExtensionTypes)e2.type) :
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
                buffer.Add(c.Color($"{unsaved}{unbound}{tag}{e.unid?.ToUNID() ?? e.parent?.unid?.ToUNID() ?? "Unknown",-12}{e.name,-48}{e.path.TruncatePath()}"));
                if(buffer.Last().Count > c.width) {
                    buffer.Add(c.Color(""));
                }

                if (moduleMode != ModuleMode.HideNone && modulesByExtension.ContainsKey(e)) {
                    buffer.Add(c.Color($"        Modules: {modulesByExtension[e].Count}"));
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
            if (i.Text.Length == 0) {
                scroller.Draw(buffer, scroller.screenRows + s.height);
                i.Draw();
            } else {
                scroller.Draw(buffer);
                i.Draw();
                s.Draw();
            }
            t.Draw();
            var pos = c.cursor.Position;
            //h.Draw();

            c.SetCursor(pos);
            c.margin = new Point(0, c.margin.Y);
            if (loading != null) {
                c.NextLine();
                c.WriteLine("Loading extensions...");
            }
            
        }

        public void Handle(ConsoleKeyInfo k) {
            if(loading != null) {
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
                                ref var theme = ref c.theme;

                                if (parts.Length == 1) {
                                    h.Record();
                                    Reset();
                                    break;
                                }
                                switch (parts[1]) {
                                    case "blue":
                                        theme = new Theme() {
                                            front = new Color(0x00, 0x69, 0xE7),
                                            back = Color.Black,
                                            highlight = Color.LightBlue,
                                        };
                                        h.Record();
                                        break;
                                    case "green":
                                        theme = new Theme() {
                                            front = new Color(0xA8, 0xB7, 0x0E),
                                            back = Color.Black,
                                            highlight = Color.LightBlue,
                                        };
                                        h.Record();
                                        break;
                                    case "pine":
                                        theme = new Theme() {
                                            front = new Color(0x00, 0x76, 0x6B),
                                            back = Color.Black,
                                            highlight = Color.Magenta,
                                        };
                                        h.Record();
                                        break;
                                    case "orange":
                                        theme = new Theme() {
                                            front = new Color(0xFF, 0x92, 0x07),
                                            back = Color.Black,
                                            highlight = Color.White
                                        };
                                        h.Record();
                                        break;
                                    case "default":
                                        Reset();
                                        h.Record();
                                        break;
                                }
                                void Reset() {
                                    c.theme = new Theme();
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
                                env.SaveState();
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
                                env.SaveState();
                                h.Record();
                                break;
                            }
                        case "unloadall": {
                                var extensions = new List<TranscendenceExtension>(env.extensions.Values);
                                foreach (var e in extensions) {
                                    env.Unload(e);
                                }
                                env.SaveState();
                                h.Record();
                                break;
                            }
                        case "reloadall": {
                                var extensions = new List<TranscendenceExtension>(env.extensions.Values);
                                foreach (var e in extensions) {
                                    env.Unload(e);
                                }
                                foreach(var e in extensions) {
                                    env.Load(e.path);
                                }
                                env.SaveState(); 
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
                                env.Load(path);
                                env.SaveState();
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
                                env.LoadFolder(path, true);
                                env.SaveState();
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
                                    loading = Task.Run(() => {
                                        try {
                                            env.LoadFolder(path);
                                        } catch(Exception e) {
                                            Debug.Print(e.StackTrace);
                                            throw;
                                        } finally {
                                            env.SaveState();
                                            loading = null;
                                        }
                                    });
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
                                    env.LoadModules(existing);
                                } else {
                                    env.LoadFolder(path, true);
                                }
                                env.SaveState();
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
                                    screens.Push(new TypeEditor(screens, env, result, c, new GotoHandler() {
                                        state = state,
                                        screens = screens,
                                        env = env,
                                        extension = result,
                                        c = c
                                    }));
                                    h.Record();
                                }
                                break;
                            }
                        case "exit": {
                                screens.Pop();
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
                        {"", () => new List<string>{ "types", "theme", "create", "bindall", "load", "unload", "edit", "open", "reload", "reloadmodules", "reloadall", "reloadallmodules", "loadmodules", "exit" } },
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
                            if (Path.IsPathRooted(path)) {
                                path = Path.GetFullPath(path);
                                
                            }
                            var dir = new DirectoryInfo(Path.GetFullPath(path));

                            if (dir.Exists) {
                                if (!path.EndsWith(Path.DirectorySeparatorChar.ToString())) {
                                    path += Path.DirectorySeparatorChar;
                                }
                                result.Add(path);
                                var up = $"..{Path.DirectorySeparatorChar}";
                                if (path.EndsWith(up)) {
                                    result.Add(path + up);
                                }
                                foreach (var file in dir.GetFiles("*.xml")) {
                                    result.Add(path + file.Name);
                                }
                                foreach (var subdir in dir.GetDirectories()) {
                                    result.Add(path + subdir.Name + Path.DirectorySeparatorChar); //Add path separator to the end to speed up autocomplete navigation
                                }
                            } else if (File.Exists(path)) {
                                /*
                                result.Remove(path);
                                result.Insert(0, path);
                                */
                                result.Add(path);
                            } else {
                                var parent = dir.Parent;
                               if (parent?.Exists == true) {
                                    Action<string> addResult;
                                    if (Path.IsPathRooted(path)) {
                                        addResult = s => result.Add(s);

                                        addResult(parent.FullName);
                                        foreach (var file in parent.GetFiles("*.xml")) {
                                            addResult(file.FullName);
                                        }
                                        foreach (var subdir in parent.GetDirectories()) {
                                            addResult(subdir.FullName + Path.DirectorySeparatorChar); //Add path separator to the end to speed up autocomplete navigation
                                        }
                                    } else {
                                        addResult = s => result.Add(Path.GetDirectoryName(path) + Path.DirectorySeparatorChar + s);
                                        foreach (var file in parent.GetFiles("*.xml")) {
                                            addResult(file.Name);
                                        }
                                        foreach (var subdir in parent.GetDirectories()) {
                                            addResult(subdir.Name + Path.DirectorySeparatorChar); //Add path separator to the end to speed up autocomplete navigation
                                        }
                                    }
                                }
                            }


                        }
                        result.Add(extensionsFolder + Path.DirectorySeparatorChar);
                        result.AddRange(Directory.GetFiles(extensionsFolder, "*.xml").ToList());

                        //Add current directory files last
                        /*
                        result.Add(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar);
                        result.AddRange(Directory.GetFiles(Directory.GetCurrentDirectory(), "*.xml").ToList());
                        */
                        
                        return result.Distinct().ToList();
                    }

                    break;
            }
            t.Handle(k);



            
        }
        public void Update() {
        }

    }
}
