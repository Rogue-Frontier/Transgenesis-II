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
        public TypeManager types;
        public Dictionary<string, XElement> typemap;
        HashSet<TranscendenceExtension> dependencies;
        HashSet<TranscendenceExtension> modules;
        public XElement structure;
        public TranscendenceExtension(string path, XElement structure) {
            parent = null;
            this.path = path;
            types = new TypeManager();
            typemap = new Dictionary<string, XElement>();
            
            dependencies = new HashSet<TranscendenceExtension>();
            modules = new HashSet<TranscendenceExtension>();
            this.structure = structure;
        }

        public void Save() {
            StringBuilder s = new StringBuilder();
            s.AppendLine(@"<?xml version=""1.0"" encoding=""us-ascii""?>");
            s.AppendLine($"!DOCTYPE {structure.Tag()}");
            s.AppendLine("    [");

            /*
            (_, Dictionary<string, string> entity2unid) = types.BindAll();
            foreach(string entity in entity2unid.Keys) {
                s.AppendLine($@"    {entity, -32}""{entity2unid[entity]}""");
            }
            */
            //TO DO: Display type elements
            foreach(TypeElement e in types.elements) {
                if(e is TypeEntry entry) {
                    s.AppendLine($@"    {entry.entity,-32}""{entry.unid}""");
                }
                //TO DO: Handle other element types
            }

            //TO DO: Display bound types

            s.AppendLine("    ]>");
            s.Append(structure.ToString());
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
                module.updateTypeBindingsWithModules(typemap, e);
            }
            //codes.setLastBindCode(getBindCode());
        }

        public void updateTypeBindingsWithModules(Dictionary<string, XElement> parentMap, Environment e) {
            typemap = new Dictionary<string, XElement>(parentMap);
            bindAccessibleTypes(typemap, e);
            foreach (TranscendenceExtension module in modules) {
                module.updateTypeBindingsWithModules(typemap, e);
            }
            //codes.setLastBindCode(getBindCode());
        }

        public void updateTypeBindings(Environment e) {
            //Check if anything changed since the last binding. If not, then we don't update.
            if (!isUnbound()) {
                //out.println(getConsoleMessage("[Warning] Type binding skipped; no changes"));
                return;
            }
            //String consoleName = getName() + path.getPath();
            typemap.Clear();
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
                typemap = new Dictionary<string, XElement>(parent.typemap);
            }

            bindAccessibleTypes(typemap, e);
            //codes.setLastBindCode(getBindCode());
		    //out.println(getConsoleMessage("[Success] Type Binding complete"));
        }

        public void bindAccessibleTypes(Dictionary<string, XElement> typemap, Environment e) {
            //Insert all of our own Types. This will allow dependencies to override them
            foreach (string s in types.BindAll().Item1.Values) {
                if (typemap.ContainsKey(s)) {
                    //System.out.println(getConsoleMessage("[Failure] Duplicate UNID: " + s));
                } else {
                    typemap[s] = null;
                }
            }
            //If we have a UNID of our own, bind it
            if (structure.Att("unid", out string unid)) {
                typemap[unid] = structure;
            }
            updateDependencies(e);
            bindDependencyTypes(typemap);
            updateModules(e);
            bindInternalTypes(typemap);
            bindModuleTypes(typemap, e);
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
        public void bindDependencyTypes(Dictionary<string, XElement> typemap) {
            //Avoid going into a circular dependency binding loop by binding only the internal types from each dependency
            //Note: Circular dependencies are not supported
            foreach (TranscendenceExtension dependency in dependencies) {
                dependency.bindAsDependency(typemap);
            }
        }
        public void bindAsDependency(Dictionary<string, XElement> typemap) {
            /*
            if(typeMap.get(getAttributeByName("unid")) == this) {
                return;
            }
            */
            foreach (String s in types.BindAll().Item1.Values) {
                typemap[s] = null;
            }
            bindInternalTypes(typemap);
            foreach (TranscendenceExtension module in modules) {
                module.bindAsDependency(typemap);
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
        public void bindInternalTypes(Dictionary<string,XElement> typemap) {
		//out.println(getConsoleMessage("[General] Binding Internal Designs"));
            //Include ourself
            /*
            if(this.hasAttribute("unid")) {
                typeMap.put(getAttributeByName("unid").getValue(), this);
            }
            */
            foreach (XElement sub in structure.Elements()) {
                //We already handled Library types as dependencies
                if (!sub.Tag().Equals("Library") && sub.Att("unid", out string sub_type)) {
                    //Check if the element has been assigned a UNID
                    if (!(sub_type == null || sub_type.Length > 0)) {
                        //Check if the UNID has been defined by the extension
                        //WARNING: THE TYPE SPECIFIED IN THE ATTRIBUTE WILL NOT MATCH BECAUSE IT IS AN XML ENTITY. REMOVE THE AMPERSAND AND SEMICOLON.
                        sub_type = sub_type.Replace("&", "").Replace(";", "");
                        if (typemap.ContainsKey(sub_type)) {
                            //Check if the UNID has not already been bound to a Design
                            if (typemap[sub_type] == null) {
                                typemap[sub_type] = sub;
                            } else if (typemap[sub_type] == sub) {
                                //Ignore if this element was bound earlier (such as during a Parent Type Binding).
                            } else if (typemap[sub_type].Tag().Equals(sub.Tag())) {
                                //If the UNID is bound to a Design with the same tag, then it's probably an override
                                //out.println(getConsoleMessage2(sub.getName(), String.format("%-15s %s", "[Warning] Override Type:", sub_type)));

                                //Override for now
                                typemap[sub_type] = sub;
                            } else {
							    //out.println(getConsoleMessage2(sub.getName(), String.format("%-15s %s", "[Warning] Duplicate Type:", sub_type)));
                            }
                        } else {
                            //Depending on the context, we will not be able to identify whether this is defining a completely nonexistent Type or overriding an external type
                            //out.println(getConsoleMessage2(sub.getName(), String.format("%-15s %-31s %s", "[Warning] Unknown UNID:", sub_type + ";", "It may be an override for an unloaded dependency or it may be nonexistent")));

                            //For now, we track this type
                            bool bindUnknown = true;
                            if(bindUnknown) {
                                typemap[sub_type] = sub;
                            }
                        }
                    } else {
					    //out.println(getConsoleMessage2(sub.getName(), "[Failure] Missing unid= attribute"));

                    }
                }
            }
        }
        private void bindModuleTypes(Dictionary<string,XElement> typemap, Environment e) {

            foreach (TranscendenceExtension module in modules) {
                module.bindInternalTypes(typemap);
                //Let sub-modules bind Types for us too
                module.updateModules(e);
                module.bindModuleTypes(typemap, e);
            }
            /*
            //Let modules inherit a copy of our bindings
            for(TranscendenceMod module : modules) {
                System.out.println(getConsoleMessage("[General] Copying Type Bindings to Module " + module.getPath().getAbsolutePath()));
                module.typeMap = new TreeMap<String, DesignElement>(typeMap);
            }
            */
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

    class TypeManager {
        public List<TypeElement> elements = new List<TypeElement>();  //TO DO: Make sure that entries and ranges are correctly sorted at extension loading time if no working metadata file is available

        public (Dictionary<string, string>, Dictionary<string,string>) BindAll() {
            Dictionary<string, string> unid2entity = new Dictionary<string, string>();
            Dictionary<string, string> entity2unid = new Dictionary<string, string>();
            List<TypeElement> definedUNIDs = new List<TypeElement>();
            List<TypeElement> generatedUNIDs = new List<TypeElement>();
            foreach (TypeElement e in elements) {
                if (e is TypeEntry || e is TypeRange) {
                    definedUNIDs.Add(e);
                } else {
                    generatedUNIDs.Add(e);
                }
            }
            definedUNIDs.ForEach(e => e.BindAll(unid2entity, entity2unid));
            //TO DO: Type and TypeGroup need to auto-generate UNIDs
            generatedUNIDs.ForEach(e => e.BindAll(unid2entity, entity2unid));
            return (unid2entity, entity2unid);
        }
    }

    static class Types {
        public static void BindEntry(Dictionary<string, string> unid2entity, Dictionary<string, string> entity2unid, string unid, string entity) {
            //Attempt to make the unid into a hex string, if it is not already one.
            try {
                //8-digit hex is too cool for Integer
                //unid = fixHex(Long.toHexString(Long.decode(unid.toLowerCase())));
                unid = $"0x{long.Parse(unid.ToLower().Replace("0x", ""), System.Globalization.NumberStyles.HexNumber).ToString("X8")}";
            } catch (Exception e) {
                //JOptionPane.showMessageDialog(null, "Invalid UNID: " + unid + "[" + type + "]");
                return;
            }
            if (!entity.All(c => char.IsLetterOrDigit(c)) || entity.Contains("-") || entity.Contains("_")) {
                //JOptionPane.showMessageDialog(null, "Invalid Type: " + type + "[" + unid + "]");
            } else if (unid2entity.ContainsValue(entity)) {
                //JOptionPane.showMessageDialog(null, "Type Conflict: " + type + " [" + unid + " " + entryMap.getKey(type) + "]");
            } else {
                unid2entity[unid] = entity;
                entity2unid[entity] = unid;
            }
            /*
            if(entryMap.containsKey(unid)) {
                JOptionPane.showMessageDialog(null, "UNID Conflict: " + unid + " [" + type + " " + entryMap.get(unid) + "]");
            }
            */
        }
        public static String COMMENT_DEFAULT = "[Comment]";
        public static String UNID_DEFAULT = "[UNID]";
        public static String ENTITY_DEFAULT = "[Entity]";
    }

    interface TypeElement {
        XElement GetXMLOutput();
        void BindAll(Dictionary<string,string> unid2entity, Dictionary<string,string> entity2unid);
	}
	//Specifies a single type that will get an automatically-generated UNID
	class Type : TypeElement {

        //public string comment;
        public string entity;
        /*
        public Type(String comment, String type) {
            this.comment = comment;
            this.entity = type;
        }
        */
        public Type(string type) {
            this.entity = type;
        }
        public void BindAll(Dictionary<string, string> unid2entity, Dictionary<string, string> entity2unid) {}
        public XElement GetXMLOutput() {
            XElement result = new XElement("Type");
            //result.SetAttributeValue("comment", comment);
            result.SetAttributeValue("entity", entity);
            return result;
        }
	}

    //Specifies a single type bound to a UNID
    class TypeEntry : TypeElement {
        public string unid, entity;
        public TypeEntry(string unid, string entity) {
            this.unid = unid;
            this.entity = entity;
        }
        public void BindAll(Dictionary<string, string> unid2entity, Dictionary<string, string> entity2unid) {
            Types.BindEntry(unid2entity, entity2unid, unid, entity);
        }
        public XElement GetXMLOutput() {
            XElement result = new XElement("TypeEntry");
            //result.SetAttributeValue("comment", comment);
            result.SetAttributeValue("unid", unid);
            result.SetAttributeValue("entity", entity);
            return result;
        }
    }
	//Specifies a group of types that will get automatically-generated UNIDs
	class TypeGroup : TypeElement {

        //public string comment;
        public List<string> entities;
        /*
        public TypeGroup() : this(Types.COMMENT_DEFAULT, new List<string>()) {
        }
        public TypeGroup(String comment, List<string> entities) {
            this.comment = comment;
            this.entities = new List<string>();
            this.entities.AddRange(entities);
        }
        */
        public TypeGroup(List<string> entities) {
            //this.comment = comment;
            this.entities = new List<string>(entities);
        }
        public void BindAll(Dictionary<string, string> unid2entity, Dictionary<string, string> entity2unid) { }
        public XElement GetXMLOutput() {
            XElement result = new XElement("TypeGroup");
            //result.SetAttributeValue("comment", comment);
            result.SetAttributeValue("entities", string.Join(" ", entities));
            return result;
        }
	}
	        //Specifies a group of types bound to a range of UNIDs on an interval
	class TypeRange : TypeElement {

        public string unid_min, unid_max;
        List<string> entities;
        public TypeRange() :
            this("[Min UNID]", "[Max UNID]") {
        }
        public TypeRange(string unid_min, string unid_max) : base() {
            this.unid_min = unid_min;
            this.unid_max = unid_max;
            entities = new List<string>();
        }
        public TypeRange(string comment, string unid_min, string unid_max, List<string> entities) {
            this.unid_min = unid_min;
            this.unid_max = unid_max;
        }
        public XElement GetXMLOutput() {
            XElement result = new XElement("TypeRange");
            //result.SetAttributeValue("comment", comment);
            result.SetAttributeValue("unid_min", unid_min);
            result.SetAttributeValue("unid_max", unid_max);
            result.SetAttributeValue("entities", string.Join(" ", entities));
            return result;
        }
        public void BindAll(Dictionary<string, string> unid2entity, Dictionary<string, string> entity2unid) {
            int? min = null, max = null;
            try {
                min = int.Parse(unid_min);
            } catch (Exception e) {
                //JOptionPane.showMessageDialog(null, "Invalid Minimum UNID: " + unid_min);
            }
            try {
                max = int.Parse(unid_max);
            } catch (Exception e) {
                //JOptionPane.showMessageDialog(null, "Invalid Maximum UNID: " + unid_max);
            }
            if (min != null || max != null) {
                int maxCount = ((int)max - (int)min) + 1;
                if (entities.Count > maxCount) {
                    //JOptionPane.showMessageDialog(null, "Not enough UNIDs within range");
                }
                for (int i = 0; i < entities.Count && i < maxCount; i++) {
                    Types.BindEntry(unid2entity, entity2unid, ((int)(min + i)).ToString("X"), entities[i]);
                }
            }
        }
	}
}
