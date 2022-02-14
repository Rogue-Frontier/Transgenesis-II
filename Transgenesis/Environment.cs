using System;
using System.Collections.Generic;
using System.Linq;
using static Transgenesis.Global;
using System.Xml;
using System.Xml.Linq;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Transgenesis {
    class Environment {
        public XElement hierarchy;
        public Dictionary<string, XElement> rootStructures = new Dictionary<string, XElement>();
        public Dictionary<string, XElement> baseStructure = new Dictionary<string, XElement>();
        public Dictionary<XElement, XElement> bases = new Dictionary<XElement, XElement>();
        public Dictionary<string, GameData> extensions = new Dictionary<string, GameData>();
        public Dictionary<string, List<string>> customAttributeValues;
        public XElement unknown = new XElement("Unknown");
        public bool allowUnknown = true;

        public static string ATT_CATEGORY = "category";

        public Environment() {

            XmlDocument doc = new XmlDocument();

            var spec = "Transgenesis.xml";
            try {
                doc.Load(spec);
            } catch {
                doc.Load("../../../"+spec);
            }
            hierarchy = XElement.Parse(doc.OuterXml);
            baseStructure["Hierarchy"] = hierarchy;
            foreach (var coreStructure in hierarchy.Elements("E")) {
                switch (coreStructure.Att(ATT_CATEGORY)) {
                    case "root":
                        var name = coreStructure.Att("name");
                        rootStructures[name] = baseStructure[name] = coreStructure;
                        break;
                    case "virtual":
                        baseStructure[coreStructure.Att("id")] = coreStructure;
                        break;
                    case var s: throw new Exception($"Unknown root element category {s}");
                }
            }

            customAttributeValues = new();
            foreach(var attributeType in hierarchy.Elements("AttributeType")) {
                customAttributeValues[attributeType.Att("name")] = new(attributeType.Value.Replace("\t", "").Split('\r', '\n').Where(s => !string.IsNullOrWhiteSpace(s)));
            }
        }
        public bool CanAddElement(XElement element, XElement template, string subelement, out XElement subtemplate) {
            if(template.TryNameElement(subelement, out subtemplate) || template.TryNameElement("*", out subtemplate)) {
                subtemplate = InitializeTemplate(subtemplate);
                return CanAddElement(element, subtemplate);
            } else {
                subtemplate = unknown;
                return allowUnknown;
            }
        }
        public bool CanRemoveElement(XElement element, XElement template, string subelement) {
            return CanRemoveElement(element, template.Elements("E").First(e => e.Att("name") == subelement));
        }
        public static bool CanAddElement(XElement element, XElement subtemplate) {
            switch(subtemplate.Att(ATT_CATEGORY)) {
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
        public bool CanRemoveElement(XElement element, XElement subtemplate) {
            if(subtemplate == unknown) {
                return true;
            }
            switch (subtemplate.Att(ATT_CATEGORY)) {
                case "*":
                case "?":
                    return true;
                case "+":
                    return element.Elements(subtemplate.Att("name")).Count() > 1;
                case "1":
                    return false;
                default:
                    return false;
            }
        }
        public List<string> GetAddableElements(XElement element, XElement template) =>
            template.Elements("E")
            .Select(subtemplate => InitializeTemplate(subtemplate))
            .Where(subtemplate => CanAddElement(element, subtemplate))
            .Select(subtemplate => subtemplate.Att("name")).ToList();
        public List<string> GetRemovableElements(XElement element, XElement template) =>
            template.Elements("E")
            .Select(subtemplate => InitializeTemplate(subtemplate))
            .Where(subtemplate => CanRemoveElement(element, subtemplate))
            .Select(subtemplate => subtemplate.Att("name")).ToList();
        public void Unload(GameData e) {
            extensions.Remove(e.path);
            //TO DO
            //Clear data from bases
            Unload(e.structure);

            void Unload(XElement element) {
                bases.Remove(element);
                foreach(var subelement in element.Elements()) {
                    Unload(subelement);
                }
            }
        }
        public bool LoadExtension(XmlDocument doc, string path, out GameData e) {
            var structure = XElement.Parse(doc.OuterXml);
            if (rootStructures.TryGetValue(structure.Tag(), out var template)) {
                template = InitializeTemplate(template);
                e = new GameData(path, structure);

                if(extensions.TryGetValue(path, out GameData existing)) {
                    Unload(existing);
                }

                extensions[path] = e;
                LoadWithTemplate(structure, template);

                //Load entities
                if(doc?.DocumentType?.Entities != null) {
                    foreach (XmlEntity entity in doc.DocumentType.Entities) {
                        e.types.elements.Add(new TypeEntry(entity.Name, uint.Parse(entity.InnerText.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber)));
                    }
                }
                e.UpdateSaveCode();
                return true;
            }
            e = null;
            return false;
        }
        public void LoadWithTemplate(XElement structure, XElement template) {
            bases[structure] = template;
            if(template == unknown) {
                foreach(XElement subelement in structure.Elements()) {
                    LoadWithTemplate(subelement, template);
                }
            }

            Dictionary<string, XElement> subtemplates = new Dictionary<string, XElement>();
            foreach(XElement subtemplate in template.Elements()) {
                var initialized = InitializeTemplate(subtemplate);
                string name = initialized.Att("name");
                subtemplates[name] = initialized;
            }
            foreach(XElement subelement in structure.Elements()) {
                string name = subelement.Tag();
                if(subtemplates.TryGetValue(name, out XElement subtemplate) || subtemplates.TryGetValue("*", out subtemplate)) {
                    //Initialize subelement base
                    LoadWithTemplate(subelement, subtemplate);
                } else {
                    //Otherwise this element has no base
                    LoadWithTemplate(subelement, unknown);
                }
            }
        }
        public XElement FromTemplate(XElement template, string name = null) {
            var result = new XElement(name ?? template.Att("name"));
            if(template == unknown) {
                goto Ready;
            }

            foreach(var subtemplate in template.Elements("E").Where(e => e.Att(ATT_CATEGORY) == "1" || e.Att(ATT_CATEGORY) == "+")) {
                var initialized = InitializeTemplate(subtemplate);
                var subelement = FromTemplate(initialized);
                bases[subelement] = initialized;
                result.Add(subelement);
            }
            //Initialize attributes to default values
            foreach (XElement attributeSpec in template.Elements("A")) {
                string key = attributeSpec.Att("name");
                string value = attributeSpec.Att("value");
                if(value != null) {
                    result.SetAttributeValue(key, value);
                }
            }
            Ready:
            bases[result] = template;
            return result;
        }
        //Initializes a template for actual use, handling inheritance
        public XElement InitializeTemplate(XElement template) {

            XElement result = new XElement(template.Name);
            if (template.Att("inherit", out string from)) {
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
                XElement inherited = unknown;
                if(baseStructure.TryGetValue(source, out XElement templateBase)) {
                    inherited = templateBase;
                } else {
                    if(allowUnknown) {
                        goto SkipInherit;
                    } else {
                        inherited = baseStructure[source];
                    }
                }

                //Handle the inheritance chain
                inherited = InitializeTemplate(inherited);

                foreach (string part in parts.Skip(1)) {
                    //template = template.Element(part);
                    inherited = inherited.Elements("E").FirstOrDefault(e => e.Att("name") == part);

                    //Handle the inheritance chain
                    inherited = InitializeTemplate(inherited);
                }
                //Inherit base attributes
                foreach (var a in inherited.Attributes()) {
                    result.SetAttributeValue(a.Name, a.Value);
                }
                //Inherit base elements
                foreach (var e in inherited.Elements()) {
                    result.Add(new XElement(e));
                }
            }
            SkipInherit:
            //Handle additional/overriding attributes
            foreach (var a in template.Attributes()) {
                result.SetAttributeValue(a.Name, a.Value);
            }
            //Handle additional/overriding elements
            foreach (var e in template.Elements()) {
                if (result.NameElement(e.Att("name"), out XElement replaced)) {
                    replaced.ReplaceWith(e);
                } else {
                    result.Add(e);
                }
            }
            /*
            if(template.Att("name") == null && from?.StartsWith("DesignTypeBase:") == true) {
                Trace.Assert(!string.IsNullOrEmpty(result.Att("name")), result.ToString());
            }
            */
            return result;
        }
        public List<string> GetAttributeValues(GameData extension, string attributeType) {
            if(customAttributeValues.TryGetValue(attributeType, out List<string> values)) {
                return values;
            } else if(Enum.TryParse(attributeType, out AttributeTypes attributeTypeEnum)) {
                IEnumerable<string> result;
                switch(attributeTypeEnum) {
                    case AttributeTypes.UNID:
                        //return extension.types.entities;

                        //Return UNIDs that have not been bound yet
                        result = from entity in extension.types.entities
                               where !extension.types.typemap.TryGetValue(entity, out XElement design) || design == null
                               select entity;
                        break;
                    case AttributeTypes.TYPE_ANY:
                    case AttributeTypes.TYPE_INHERITED:
                        result = FindTypes();
                        break;
                    case AttributeTypes.TYPE_ITEM:
                        result = FindItems();
                        break;
                    case AttributeTypes.TYPE_ITEM_ARMOR:
                        result = FindItems("Armor");
                        break;
                    case AttributeTypes.TYPE_ITEM_DEVICE:
                        var deviceTags = new List<string> {
                                        "AutoDefenseDevice",
                                        "CargoHoldDevice",
                                        "DriveDevice",
                                        "EnhancerDevice",
                                        "MiscellaneousDevice",
                                        "ReactorDevice",
                                        "RepairerDevice",
                                        "Shields",
                                        "SolarDevice",
                                        "Weapon"
                                    };
                        result = FindTypes(design =>
                            design.Tag() == "ItemType" &&
                            design.Elements().Any(
                                element => deviceTags.Contains(element.Tag())));
                        break;
                    case AttributeTypes.TYPE_ITEM_WEAPON:
                        result = FindItems("Weapon");
                        break;
                    //TO DO
                    default:
                        result = new List<string>();
                        break;
                }
                return result.Select(entity => $"&{entity};").ToList();
            } else {
                //Error: Unknown attribute type

                return new List<string>();
            }
            IEnumerable<string> FindItems(string category = null) {
                if(category != null) {
                    return FindTypes(design => design.Tag() == "ItemType" && design.Elements(category).Count() > 0);
                } else {
                    return FindTypes(design => design.Tag() == "ItemType");
                }
            }
            IEnumerable<string> FindTypes(Func<XElement, bool> predicate = null) {
                if(predicate == null) {
                    return from entity in extension.types.entities
                            where extension.types.typemap.TryGetValue(entity, out XElement design) && design != null
                            select entity;
                } else {
                    return from entity in extension.types.entities
                            where extension.types.typemap.TryGetValue(entity, out XElement design) && design != null && predicate(design)
                            select entity;
                }
            }
        }
        public void CreateExtension(string templateType, string path) {
            XElement structure;
            XElement template;
            GameData extension;
            if(rootStructures.TryGetValue(templateType, out template)) {
                template = InitializeTemplate(template);
                structure = FromTemplate(template);

                extension = new GameData(
                    path: path,
                    structure: structure
                );
                extensions[path] = extension;
                extension.Save();
            }
        }
        public void LoadFolder(string path, bool modules = false) {
            if (Directory.Exists(path)) {
                var files = Directory.GetFiles(path);
                foreach (var subpath in files) {
                    LoadFolder(subpath, modules);
                }

                var directories = Directory.GetDirectories(path);
                foreach (var subpath in directories) {
                    LoadFolder(subpath, modules);
                }
            }
            if (File.Exists(path) && Path.GetExtension(path) == ".xml") {
                Load(path, modules);
            }
        }
        public void Load(string path, bool modules = false) {


            string xml = File.ReadAllText(path);
            //Cheat the XML reader by escaping ampersands so we don't parse entities
            xml = xml.Replace("&", "&amp;");

            var removeCommentOpen = new Regex(Regex.Escape("<!--") + Regex.Escape("-") + "+");
            var removeCommentClose = new Regex(Regex.Escape("-") + "+" + Regex.Escape("-->"));
            xml = removeCommentOpen.Replace(xml, "<!--");
            xml = removeCommentClose.Replace(xml, "-->");
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            LoadExtension(doc, path, out GameData e);
            if (modules) {
                LoadModules(e);
            }
        }
        public void LoadModules(GameData e) {
            foreach (var module in e.structure.Elements()) {
                if (module.Tag() == "Module" || module.Tag() == "CoreLibrary" || module.Tag() == "TranscendenceAdventure") {
                    string filename = module.Att("filename") ?? module.Att("file");
                    //Use the full path when finding modules
                    string path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(e.path), filename));
                    Load(path, true);
                }
            }
        }
        public void BindAll() {
            foreach (var ext in extensions.Values) {
                ext.updateTypeBindingsWithModules(this);
            }
            foreach (var ext in extensions.Values) {
                ext.updateTypeBindingsWithModules(this);
            }
        }

        public void SaveState() {
            File.WriteAllText("Environment.json", JsonConvert.SerializeObject(extensions.Keys.ToList()));
        }
        public void LoadState() {
            if(File.Exists("Environment.json")) {
                var loaded = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText("Environment.json"));
                foreach(var file in loaded.Where(f => File.Exists(f))) {
                    Load(file);
                }
                BindAll();
            }
        }
    }
}
