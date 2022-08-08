using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Transgenesis {
    class GotoHandler {
        public ProgramState state;
        public Stack<IScreen> screens;
        public Environment env;
        public GameData extension;
        public ConsoleManager c;
        public XElement focused;

        public List<string> SuggestGoto(string dest) {
            string pattern = $"({Regex.Escape(".")}|{Regex.Escape("#")})";
            List<string> result = Regex.Split(dest, pattern).ToList();

            var arg = result[0];


            //We start with a UNID.
            //This can either be an extension UNID, in which case we jump to the extension first
            //Otherwise it is a type UNID, either in this extension or in some dependency
            GameData destExtension;
            GameData destModule;
            XElement destElement;
            List<string> suggest = new List<string>();

            bool expectElementName = false;
            if (extension.types.entity2unid.TryGetValue(arg, out uint unid)) {
                destExtension = env.extensions.Values.ToList().Find(e => e.unid == unid);
                //Our first argument is an extension UNID
                if (destExtension != null) {
                    suggest.Add(arg);
                    suggest.AddRange(destExtension.types.moduleTypes.Keys.Select(entity => $"{arg}.{entity}"));
                    //We found a destination extension, so we advance to the next argument
                    result = result.GetRange(1, result.Count - 1);
                    if (result.Count > 1 && result[0] == ".") {
                        result = result.GetRange(1, result.Count - 1);
                        arg = result[0];
                        if (arg == "") {
                            goto Done;
                        }
                        goto FindModule;
                    } else if (result.Count > 0) {
                        arg = result[0];
                        goto FindModule;
                    } else {
                        //We're out of arguments, and we only have an extension
                        goto Done;
                    }
                } else {
                    //We have a UNID but it's not an extension UNID, so we look within the current extension
                    destExtension = extension;
                    suggest.AddRange(destExtension.types.moduleTypes.Keys);
                    goto FindModule;
                }
            FindModule:
                //Now the next argument should be type UNID within the destination extension or an element name
                //If it's a UNID, then we will find which module it is in. Otherwise, we are looking for some kind of element
                if (destExtension.types.moduleTypes.TryGetValue(arg, out destModule)) {
                    //It is a UNID
                    //Since we know which module it is in, we also know that it is bound to a Design in the module
                    destElement = destExtension.types.typemap[arg];

                    //It is a UNID, so we advance to the next argument
                    result = result.GetRange(1, result.Count - 1);
                    if (result.Count > 0) {
                        arg = result[0];
                        goto ElementPath;
                    } else {
                        //We have no arguments left, so we just show the Design
                        goto ElementShow;
                    }
                } else if (destExtension.types.dependencyTypes.TryGetValue(arg, out GameData destDependency)) {
                    //Otherwise, we are looking at a UNID defined in a dependency

                    //Convert this to an entity in the local extension and convert that back to an entity in the destination extension
                    //And use that entity to find the module where it is defined

                    arg = destDependency.types.unid2entity[extension.types.entity2unid[arg]];
                    destModule = destDependency.types.moduleTypes[arg];
                    destElement = destDependency.types.typemap[arg];

                    //It is a UNID, so we advance to the next argument
                    result = result.GetRange(1, result.Count - 1);
                    if (result.Count > 0) {
                        arg = result[0];
                        goto ElementPath;
                    } else {
                        //We have no arguments left, so we just show the Design
                        goto ElementShow;
                    }
                } else {
                    //If it's not a UNID, then it's either an incomplete UNID name or the name of an element
                    destModule = destExtension;

                    destElement = destModule.structure;
                    expectElementName = true;
                    goto ElementPath;
                }
            } else {
                //If it's not a UNID, then it's either an incomplete UNID name or the name of an element in the focused extension
                suggest = new List<string>();
                suggest.AddRange(extension.types.moduleTypes.Keys);
                //suggest.AddRange(extension.types.dependencyTypes.Select(pair => $"{extension.types.unid2entity[(uint)pair.Value.unid]}.{pair.Key}"));

                if(focused != null) {
                    destExtension = extension;
                    destModule = extension;
                    destElement = focused;
                    expectElementName = true;
                    goto ElementPath;
                }
                goto Done;
            }

        //Now find the element
        ElementPath:
            for (int i = 0; i < result.Count; i++) {
            Check:
                if (result[i] == ".") {
                    if (++i < result.Count) {
                        expectElementName = true;
                        goto Check;
                    } else {
                        //Otherwise return a list of subelements we could go to
                        suggest.AddRange(destElement.Elements().Select(sub => $"{dest}{sub.Tag()}"));
                        goto Done;
                    }
                } else if (expectElementName) {

                    expectElementName = false;
                    var tag = result[i];
                    if (tag == "") {
                        break;
                    }

                    var subelements = destElement.Elements(tag);
                    if (subelements.Count() == 0) {
                        //Return a list of subelements we could go to
                        string prefix2 = string.Join("", result.Take(result.LastIndexOf(".")));
                        prefix2 = prefix2.Length > 0 ? $"{prefix2}." : prefix2;
                        suggest.AddRange(destElement.Elements().Select(sub => $"{prefix2}{sub.Tag()}"));
                        goto Done;
                    } else if (subelements.Count() == 1) {
                        //We take the first one
                        destElement = subelements.First();
                    } else {
                        if (++i < result.Count && result[i] == "#" && ++i < result.Count && int.TryParse(result[i], out int index)) {
                            destElement = subelements.Skip(index).First();
                        } else {
                            //Error: We need to specify an index
                            suggest.AddRange(Enumerable.Range(0, subelements.Count()).Select(n => $"{dest}#{n}"));
                            goto Done;
                        }
                    }
                } else {
                    goto Done;
                }
            }
        ElementShow:
            string prefix = (dest.Length > 0) ? $"{dest}." : "";
            suggest.AddRange(destElement.Elements().Select(sub => $"{prefix}{sub.Tag()}"));
        Done:
            return suggest.Distinct().ToList();
        }
        public void HandleGoto(string[] parts) {
            //TO DO: We should know about any types defined by our parent

            //Go to the specified element
            if (parts.Length == 1)
                return;
            string dest = parts[1];

            string pattern = $"({Regex.Escape(".")}|{Regex.Escape("#")})";
            List<string> result = Regex.Split(dest, pattern).ToList();

            var arg = result[0];

            //We start with a UNID.
            //This can either be an extension UNID, in which case we jump to the extension first
            //Otherwise it is a type UNID, either in this extension or in some dependency
            GameData destExtension;
            GameData destModule;
            XElement destElement;
            if (extension.types.entity2unid.TryGetValue(arg, out uint unid)) {
                destExtension = env.extensions.Values.ToList().Find(e => e.unid == unid);
                //Our first argument is an extension UNID
                if (destExtension != null) {
                    //We found a destination extension, so we advance to the next argument
                    result = result.GetRange(1, result.Count - 1);
                    if (result.Count > 0) {
                        arg = result[0];
                        if (arg == ".") {
                            result = result.GetRange(1, result.Count - 1);
                            arg = result[0];
                        }
                        goto FindModule;
                    } else {
                        //We're out of arguments, so we assume it's in the parent's structure
                        destModule = destExtension;
                        destElement = destModule.structure;
                        goto ElementShow;
                    }
                } else {
                    //Otherwise we don't have an extension UNID, so we look at the current extension
                    destExtension = extension;
                    goto FindModule;
                }

            //Now the argument should be type UNID within the destination extension, or the name of an element within the current focused element
            FindModule:
                //If it's a UNID, then we will find which module it is in. Otherwise, we are looking for some kind of element
                if (destExtension.types.moduleTypes.TryGetValue(arg, out destModule)) {
                    //It is a UNID
                    //Since we know which module it is in, we also know that it is bound to a Design in the module
                    destElement = destExtension.types.typemap[arg];

                    //It is a UNID, so we advance to the next argument
                    result = result.GetRange(1, result.Count - 1);
                    if (result.Count > 0) {
                        arg = result[0];
                    } else {
                        //We have no arguments left, so we just show the Design
                        goto ElementShow;
                    }
                } else if (destExtension.types.dependencyTypes.TryGetValue(arg, out GameData destDependency)) {
                    //Otherwise, we are looking at a UNID defined in a dependency

                    //Convert this to an entity in the local extension and convert that back to an entity in the destination extension
                    //And use that entity to find the module where it is defined

                    arg = destDependency.types.unid2entity[extension.types.entity2unid[arg]];
                    destModule = destDependency.types.moduleTypes[arg];
                    destElement = destDependency.types.typemap[arg];

                    //It is a UNID, so we advance to the next argument
                    result = result.GetRange(1, result.Count - 1);
                    if (result.Count > 0) {
                        arg = result[0];
                    } else {
                        //We have no arguments left, so we just show the Design
                        goto ElementShow;
                    }
                } else {
                    //It's not a UNID, so it's within the current module
                    destModule = destExtension;
                    //We're looking for an element, so we start from the base
                    destElement = destModule.structure;
                    if (arg != ".") {
                        result.Insert(0, ".");
                    }
                    goto ElementPath;
                }
            } else if(focused != null) {
                //If it's not a UNID, then we're definitely looking for an element within this extension in the focused element
                destExtension = extension;
                destModule = extension;
                destElement = focused;
                if (arg != ".") {
                    result.Insert(0, ".");
                }
                goto ElementPath;
            } else {
                //Error
                return;
            }

        //Now find the element
        ElementPath:
            for (int i = 0; i < result.Count; i++) {
                if (result[i] == ".") {
                    if (++i < result.Count) {
                        var tag = result[i];
                        var subelements = destElement.Elements(tag);
                        if (subelements.Count() == 0) {

                        } else if (subelements.Count() == 1) {
                            //We take the first one
                            destElement = subelements.First();
                        } else {
                            if (++i < result.Count && result[i] == "#" && ++i < result.Count && int.TryParse(result[i], out int index)) {
                                destElement = subelements.Skip(index).First();
                            } else {
                                //Error: We need to specify an index
                            }
                        }
                    }
                } else {

                }
            }
        ElementShow:
            screens.Push(new ElementEditor(state, screens, env, destModule, c, destElement));
        }
    }
}
