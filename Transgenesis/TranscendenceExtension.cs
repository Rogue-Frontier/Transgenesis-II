using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using System.Linq;
using System.IO;
using System.Xml;

namespace Transgenesis {
    class TranscendenceExtension {
        public TranscendenceExtension parent;
        public string path;
        public TypeInfo types;
        HashSet<TranscendenceExtension> dependencies;
        HashSet<TranscendenceExtension> modules;
        public XElement structure;
        public TranscendenceExtension(string path, XElement structure) {
            parent = null;
            this.path = path;
            types = new TypeInfo();
            
            dependencies = new HashSet<TranscendenceExtension>();
            modules = new HashSet<TranscendenceExtension>();
            this.structure = structure;
        }

        public void Save() {
            StringBuilder s = new StringBuilder();
            s.AppendLine(@"<?xml version=""1.0"" encoding=""us-ascii""?>");
            s.AppendLine($"<!DOCTYPE {structure.Tag()}");
            s.AppendLine("    [");

            /*
            (_, Dictionary<string, string> entity2unid) = types.BindAll();
            foreach(string entity in entity2unid.Keys) {
                s.AppendLine($@"    {entity, -32}""{entity2unid[entity]}""");
            }
            */
            foreach(var unid in types.unidmap.Keys) {
                s.AppendLine($@"    <!ENTITY {types.unidmap[unid],-32}""{unid}"">");
            }

            //TO DO: Display bound types

            s.AppendLine("    ]>");
            //Unreplace all ampersands
            s.Append(structure.ToString().Replace("&amp;", "&"));
            File.WriteAllText(path, s.ToString());
        }

        //Get a set of all the modules in this extension
        public HashSet<TranscendenceExtension> collapseModuleChain() {
            HashSet<TranscendenceExtension> allModules = new HashSet<TranscendenceExtension>();
            allModules.Add(this);
            foreach (TranscendenceExtension module in modules) {
                allModules.UnionWith(module.collapseModuleChain());
            }
            return allModules;
        }

        //To allow overwriting dependency types without recursively binding all the extensions, we should take a list of Types from dependencies
        //If we're a TranscendenceModule, then have our parent extension bind types for us
        public void updateTypeBindingsWithModules(Environment e) {
            updateTypeBindings(e);
            foreach (TranscendenceExtension module in modules) {
                module.updateTypeBindingsWithModules(types.typemap, e);
            }
            //codes.setLastBindCode(getBindCode());
        }

        public void updateTypeBindingsWithModules(Dictionary<string, XElement> parentMap, Environment e) {
            types.typemap = new Dictionary<string, XElement>(parentMap);
            bindAccessibleTypes(e);
            foreach (TranscendenceExtension module in modules) {
                module.updateTypeBindingsWithModules(types.typemap, e);
            }
            //codes.setLastBindCode(getBindCode());
        }

        public void updateTypeBindings(Environment e) {
            //Check if anything changed since the last binding. If not, then we don't update.
            if (!isUnbound()) {
                //out.println(getConsoleMessage("[Warning] Type binding skipped; no changes"));
                return;
            }
            types.typemap.Clear();
            types.unidmap.Clear();
            types.ownedTypes.Clear();
            types.dependencyTypes.Clear();
            if (structure.Name.LocalName.Equals("TranscendenceModule")) {
                //We should have our parent extension handle this
                if (parent == null) {
                    //out.println(getConsoleMessage("[Warning] Parent Type Binding canceled; Parent Extension unknown"));
                    return;
                }
                
			    //out.println(getConsoleMessage("[General] Type Binding requested from Parent Extension"));

                //We only update Parent Extension Type Bindings if there are unbound changes
                if (parent.isUnbound()) {
                    parent.updateTypeBindings(e);
                } else {
				    //out.println(getConsoleMessage("[Warning] Parent Type Binding skipped; no changes"));
                }
			
			    //out.println(getConsoleMessage("[Success] Parent Type Binding complete; copying Types"));
                //Inherit types from our Parent Extension; we will not automatically receive them when the Parent Extension is updated
                //We make a copy of the type map from the Parent Extension since we don't want it to inherit Types/Dependencies that are exclusive to us
                types.typemap = new Dictionary<string, XElement>(parent.types.typemap);
            }

            bindAccessibleTypes(e);
            //codes.setLastBindCode(getBindCode());
		    //out.println(getConsoleMessage("[Success] Type Binding complete"));
        }

        public void bindAccessibleTypes(Environment e) {
            //Insert all of our own Types. This will allow dependencies to override them
            var bound = types.BindAll();
            foreach (string s in bound.Keys) {
                if (types.typemap.ContainsKey(s)) {
                    //System.out.println(getConsoleMessage("[Failure] Duplicate UNID: " + s));
                } else {
                    types.unidmap[s] = bound[s];
                    types.typemap[s] = null;
                }
            }
            //If we have a UNID of our own, bind it
            if (structure.Att("unid", out string unid)) {
                types.typemap[unid] = structure;
            }
            updateDependencies(e);
            bindDependencyTypes();
            updateModules(e);
            
            types.ownedTypes.UnionWith(bindInternalTypes(types.typemap));
            types.ownedTypes.UnionWith(bindModuleTypes(types.typemap, e));
        }

        //Allow modules to take external entities
        //Note: If we are a Module, we do not inherit dependencies from the Parent Extension. We will just receive the Type Map, which already includes the Parent Extension's Dependency Types
        public void updateDependencies(Environment e) {
            //String consoleName = getName() + path.getPath();
		    //out.println(getConsoleMessage("[General] Updating Dependencies"));
            dependencies.Clear();
            foreach (XElement sub in structure.Elements()) {
                String subName = sub.Name.LocalName;
                switch (subName) {
                    case "Library":
                        String library_unid = sub.Att("unid");
				        //out.println(getConsoleMessage("[General] Looking for " + subName + " " + library_unid));
                        //Make sure that Library Types are defined in our TypeManager so that they always work in-game
                        bool found = false;
                        foreach (TranscendenceExtension m in e.extensions.Values) {
                            if (
                                    (m.structure.Name.LocalName.Equals("TranscendenceLibrary") ||
                                    m.structure.Name.LocalName.Equals("CoreLibrary")) &&
                                    m.structure.Att("unid").Equals(library_unid)) {
                                dependencies.Add(m);
                                found = true;
                                break;
                            }
                        }
                        if (!found) {
					        //out.println(getConsoleMessage("[Warning] Library " + library_unid + " could not be found. It may be unloaded."));
                        } else {
					        //out.println(getConsoleMessage("[Success] Library " + library_unid + " found."));
                        }
                        break;
                    case "TranscendenceAdventure":
                    case "CoreLibrary":
                        String libraryPath = Path.Combine(Path.Combine(path, ".."), sub.Att("filename"));
				//out.println(getConsoleMessage("[General] Looking for " + subName + " " + libraryPath));
                        //Make sure that Library Types are defined in our TypeManager so that they always work in-game
                        if(e.extensions.TryGetValue(libraryPath, out TranscendenceExtension library)) {
                            dependencies.Add(library);
                        }
                        break;
                }
            }
        }
        public void bindDependencyTypes() {
            //Avoid going into a circular dependency binding loop by binding only the internal types from each dependency
            //Note: Circular dependencies are not supported
            foreach (TranscendenceExtension dependency in dependencies) {
                dependency.bindAsDependency(types);
            }
        }
        public void bindAsDependency(TypeInfo userTypes) {
            /*
            if(typeMap.get(getAttributeByName("unid")) == this) {
                return;
            }
            */
            //Initialize the entry for each type we define

            types.unidmap.Clear();
            var bound = types.BindAll();
            foreach (var s in bound.Keys) {
                types.unidmap[s] = bound[s];
                userTypes.typemap[s] = null;
            }
            //Bind our own types now
            var boundTypes = bindInternalTypes(userTypes.typemap);
            //Bind types in our modules
            foreach (TranscendenceExtension module in modules) {
                module.bindAsDependency(userTypes);
            }
            foreach(var type in boundTypes) {
                userTypes.dependencyTypes[type] = this;
            }
            /*
            updateDependencies();
            for(TranscendenceMod dependency : dependencies) {
                dependency.bindAsDependency(typeMap);
            }
            */
        }

        public void updateModules(Environment env) {
            modules.Clear();
		//out.println(getConsoleMessage("[General] Updating Modules"));


            foreach (XElement sub in structure.Elements()) {
                switch (sub.Tag()) {
                    case "Module":
                        String moduleFilename = sub.Att("filename");
                        String modulePath = Path.Combine(Path.Combine(path, ".."), moduleFilename);
				//Look for our module in the Extensions list
				//out.println(getConsoleMessage("[General] Looking for Module " + modulePath + "."));
                        if(env.extensions.TryGetValue(modulePath, out TranscendenceExtension e)) {
                            e.parent = this;
                            modules.Add(e);
                        } else {

                        }
                        //Maybe we should automatically load the module if it is not loaded already
                        break;
                }
            }
        }

        //Bindings between Types and Designs are stored in the map
        //This only binds Types with Designs that are defined within THIS file; Module bindings happen later
        public HashSet<string> bindInternalTypes(Dictionary<string,XElement> typemap) {
            //out.println(getConsoleMessage("[General] Binding Internal Designs"));
            //Include ourself
            /*
            if(this.hasAttribute("unid")) {
                typeMap.put(getAttributeByName("unid").getValue(), this);
            }
            */
            HashSet<string> bound = new HashSet<string>();
            //Now, we bind our DesignTypes to the TypeMap
            foreach (XElement sub in structure.Elements()) {
                //We already handled Library types as dependencies
                if (!sub.Tag().Equals("Library") && sub.Att("unid", out string sub_type)) {
                    //Check if the element has been assigned a UNID
                    if (sub_type != null && sub_type.Length > 0) {
                        //Check if the UNID has been defined by the extension
                        //WARNING: THE TYPE SPECIFIED IN THE ATTRIBUTE WILL NOT MATCH BECAUSE IT IS AN XML ENTITY. REMOVE THE AMPERSAND AND SEMICOLON.
                        sub_type = sub_type.Replace("&", "").Replace(";", "");
                        if (typemap.ContainsKey(sub_type)) {
                            //Check if the UNID has not already been bound to a Design
                            if (typemap[sub_type] == null) {
                                //Bind it
                                typemap[sub_type] = sub;
                                bound.Add(sub_type);
                            } else if (typemap[sub_type] == sub) {
                                //Ignore if this element was bound earlier (such as during a Parent Type Binding).
                            } else if (typemap[sub_type].Tag().Equals(sub.Tag())) {
                                //If the UNID is bound to a Design with the same tag, then it's probably an override
                                //out.println(getConsoleMessage2(sub.getName(), String.format("%-15s %s", "[Warning] Override Type:", sub_type)));

                                //Override for now
                                typemap[sub_type] = sub;
                                bound.Add(sub_type);
                            } else {
                                //out.println(getConsoleMessage2(sub.getName(), String.format("%-15s %s", "[Warning] Duplicate Type:", sub_type)));
                                throw new Exception($"Duplicate Type: {sub_type}");
                            }
                        } else {
                            //Depending on the context, we will not be able to identify whether this is defining a completely nonexistent Type or overriding an external type
                            //out.println(getConsoleMessage2(sub.getName(), String.format("%-15s %-31s %s", "[Warning] Unknown UNID:", sub_type + ";", "It may be an override for an unloaded dependency or it may be nonexistent")));

                            //For now, we track this type
                            bool bindUnknown = true;
                            if(bindUnknown) {
                                typemap[sub_type] = sub;
                                bound.Add(sub_type);
                            }

                        }
                    } else {
                        //out.println(getConsoleMessage2(sub.getName(), "[Failure] Missing unid= attribute"));
                        throw new Exception($"Missing unid= attribute: {sub.Name.LocalName}");
                    }
                }
            }
            return bound;
        }
        private HashSet<string> bindModuleTypes(Dictionary<string,XElement> typemap, Environment e) {

            HashSet<string> boundTypes = new HashSet<string>();
            foreach (TranscendenceExtension module in modules) {
                boundTypes.UnionWith(module.bindInternalTypes(typemap));
                //Let sub-modules bind Types for us too
                module.updateModules(e);
                module.bindModuleTypes(typemap, e);
            }
            return boundTypes;
        }

        public bool isUnbound() {
            //return getBindCode() != codes.getLastBindCode();
            return true;
        }
        public bool isUnsaved() {
            //return getSaveCode() != codes.getLastSaveCode();
            return false;
        }
        /*
        public int getLastBindCode() {
            return codes.getLastBindCode();
        }
        public int getLastSaveCode() {
            return codes.getLastSaveCode();
        }
        */
    }

    class BindContext {
        public Dictionary<uint, string> unid2entity = new Dictionary<uint, string>();
        public Dictionary<string, uint> entity2unid = new Dictionary<string, uint>();
        public HashSet<uint> definedUNIDs = new HashSet<uint>();
        public HashSet<uint> generatedUNIDs = new HashSet<uint>();
        public uint lastAssigned = 0;
    }
    class TypeInfo {
        public List<TypeElement> elements = new List<TypeElement>();  //TO DO: Make sure that entries and ranges are correctly sorted at extension loading time if no working metadata file is available
        public Dictionary<string, uint> unidmap = new Dictionary<string, uint>();
        public Dictionary<string, XElement> typemap = new Dictionary<string, XElement>();    //Binds entities to designs
        public HashSet<string> ownedTypes = new HashSet<string>();         //Types that we have defined
        public Dictionary<string, TranscendenceExtension> dependencyTypes = new Dictionary<string, TranscendenceExtension>();

        public Dictionary<string, uint> BindAll() {
            BindContext context = new BindContext();
            foreach (TypeElement e in elements) {
                e.BindAll(context);
            }
            return context.entity2unid;
        }
    }

    static class Types {
        public static void BindEntry(BindContext context, uint? unid, string entity) {

            if (!entity.All(c => char.IsLetterOrDigit(c)) || entity.Contains("-") || entity.Contains("_")) {
                //JOptionPane.showMessageDialog(null, "Invalid Type: " + type + "[" + unid + "]");
            } else if (context.entity2unid.ContainsKey(entity)) {
                //JOptionPane.showMessageDialog(null, "Type Conflict: " + type + " [" + unid + " " + entryMap.getKey(type) + "]");
            } else {
                if(unid == null) {
                    unid = context.lastAssigned;

                    if(unid == 0) {
                        //Return an error
                        return;
                    } else {
                        while(context.unid2entity.ContainsKey((uint) unid)) {
                            unid++;
                        }
                    }
                }
                context.unid2entity[(uint)unid] = entity;
                context.entity2unid[entity] = (uint)unid;
                context.lastAssigned = (uint)unid;
            }
        }
    }

    interface TypeElement {
        XElement GetXMLOutput();
        void BindAll(BindContext context);
	}

    //Specifies a single type bound to a UNID
    class TypeEntry : TypeElement {
        public string entity;
        public uint? unid;
        public TypeEntry(string entity, uint? unid = null) {
            this.entity = entity;
            this.unid = unid;
        }
        public void BindAll(BindContext context) {
            Types.BindEntry(context, unid, entity);
        }
        public XElement GetXMLOutput() {
            XElement result = new XElement("TypeEntry");
            //result.SetAttributeValue("comment", comment);
            result.SetAttributeValue("unid", unid);
            result.SetAttributeValue("entity", entity);
            return result;
        }
    }
	//Specifies a group of types bound to a range of UNIDs on an interval
	class TypeRange : TypeElement {

        public uint? size => unid_min != null && unid_max != null ? (unid_max - unid_min) : null;
        public uint? unid_min, unid_max;
        public List<string> entities;
        public TypeRange(uint? unid_min = null, uint? unid_max = null, params string[] entities) : base() {
            this.unid_min = unid_min;
            this.unid_max = unid_max;
            this.entities = new List<string>(entities);
        }
        public XElement GetXMLOutput() {
            XElement result = new XElement("TypeRange");
            result.SetAttributeValue("unid_min", unid_min);
            result.SetAttributeValue("unid_max", unid_max);
            result.SetAttributeValue("entities", string.Join(" ", entities));
            return result;
        }
        public void BindAll(BindContext context) {
            var min = unid_min;
            var max = unid_max;
            if (min != null && max != null) {
                uint maxCount = ((uint)max - (uint)min);
                if (entities.Count > maxCount) {
                    //JOptionPane.showMessageDialog(null, "Not enough UNIDs within range");
                }
                int i = 0;
                foreach(var entity in entities) {
                    Types.BindEntry(context, ((uint)(min + i)), entity);
                    i++;
                }
            } else if(min != null) {
                int i = 0;
                foreach(var entity in entities) {
                    Types.BindEntry(context, ((uint)(min + i)), entity);
                    i++;
                }
            } else if(max != null) {
                int i = 1;
                foreach (var entity in entities) {
                    Types.BindEntry(context, ((uint)(max - i)), entity);
                    i++;
                }
            } else {
                foreach (var entity in entities) {
                    Types.BindEntry(context, null, entity);
                }
            }
        }
	}
}
