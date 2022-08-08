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
        public string stateFile => $"Environment-{Path.GetFileNameWithoutExtension(schemaFile)}.json";
        public string schemaFile;
        public XElement schema;
        public Dictionary<string, XElement> rootStructures = new();
        public Dictionary<string, XElement> baseStructure = new();
        public Dictionary<XElement, XElement> bases = new();
        public Dictionary<string, GameData> extensions = new();
        public Dictionary<string, List<string>> customAttributeValues;
        public XElement unknown = new("Unknown");
        public bool allowUnknown = true;

        public static string ATT_COUNT = "count";

        public Environment(string schemaFile = "Transgenesis.xml") {
            this.schemaFile = schemaFile;
            var doc = new XmlDocument();
            try {
                doc.Load(schemaFile);
            } catch {
                doc.Load("../../../"+schemaFile);
            }
            schema = XElement.Parse(doc.OuterXml);
            baseStructure["Schema"] = schema;
            foreach (var coreStructure in schema.Elements("E")) {
                switch (coreStructure.Att("class")) {
                    case "root":
                        var name = coreStructure.Att("name");
                        rootStructures[name] = baseStructure[name] = coreStructure;
                        break;
                    case "virtual":
                        //note id=
                        baseStructure[coreStructure.Att("name")] = coreStructure;
                        break;
                    case var s: throw new Exception($"Unknown root element class {s}");
                }
            }

            customAttributeValues = new();
            foreach(var attributeType in schema.Elements("Enum")) {
                customAttributeValues[attributeType.Att("name")] = new(attributeType.Value.Replace("\t", "").Split('\r', '\n').Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)));
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
            switch(subtemplate.Att(ATT_COUNT)) {
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
            switch (subtemplate.Att(ATT_COUNT)) {
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
        public List<XElement> GetAddableElements(XElement element, XElement template) =>
            template.Elements("E")
            .Select(InitializeTemplate)
            .Where(subtemplate => CanAddElement(element, subtemplate)).ToList();
        public List<string> GetRemovableElements(XElement element, XElement template) =>
            template.Elements("E")
            .Select(InitializeTemplate)
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
                e = new(path, structure);

                if(extensions.TryGetValue(path, out var existing)) {
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
                foreach(var sub in structure.Elements()) {
                    LoadWithTemplate(sub, template);
                }
            }
            var subtemplates = new Dictionary<string, XElement>();
            foreach(var subtemplate in template.Elements()) {
                var initialized = InitializeTemplate(subtemplate);
                var name = initialized.Att("name");
                subtemplates[name] = initialized;
            }
            foreach(var subelement in structure.Elements()) {
                var name = subelement.Tag();
                if(subtemplates.TryGetValue(name, out var subtemplate) || subtemplates.TryGetValue("*", out subtemplate)) {
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
            if(template != unknown) {
                foreach (var subtemplate in template.Elements("E").Where(e => e.Att(ATT_COUNT) == "1" || e.Att(ATT_COUNT) == "+")) {
                    var initialized = InitializeTemplate(subtemplate);
                    var subelement = FromTemplate(initialized);
                    bases[subelement] = initialized;
                    result.Add(subelement);
                }
                //Initialize attributes to default values
                foreach (XElement attributeSpec in template.Elements("A")) {
                    string key = attributeSpec.Att("name");
                    string value = attributeSpec.Att("value");
                    if (value != null) {
                        result.SetAttributeValue(key, value);
                    }
                }
            }
            bases[result] = template;
            return result;
        }
        //Initializes a template for actual use, handling inheritance
        public XElement InitializeTemplate(XElement template) {

            var result = new XElement(template.Name);
            if (template.Att("inherit", out var from)) {
                var parts = from.Split(':');
                var source = parts.First();
                //XElement template = original;
                /*
                while (template.Name.LocalName != source) {
                    template = template.Parent;
                    Console.WriteLine(template.Name.LocalName);
                }
                */
                //Start with the root and navigate to the base element
                var inherited = unknown;
                if (baseStructure.TryGetValue(source, out var templateBase) || rootStructures.TryGetValue(source, out templateBase)) {
                    inherited = templateBase;
                } else if (allowUnknown) {
                    goto SkipInherit;
                } else {
                    inherited = baseStructure[source];
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
                if (result.NameElement(e.Att("name"), out var replaced)) {
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
            if(attributeType == null) {
                return null;
            }
            if(attributeType == "E_VIRTUAL") {
                return extension.structure.Elements("E")
                    .Where(e => e.Att("class") == "virtual")
                    .Select(e => e.Att("name")).Where(id => id != null).ToList();
            }

            if(customAttributeValues.TryGetValue(attributeType, out var values)) {
                return values;
            }
            if(Enum.TryParse(attributeType, out AttributeTypes attributeTypeEnum)) {
                return GetEntities().Select(entity => $"&{entity};").ToList();
                IEnumerable<string> GetEntities() {
                    switch (attributeTypeEnum) {
                        case AttributeTypes.UNID:
                            //return extension.types.entities;

                            //Return UNIDs that have not been bound yet
                            return extension.types.entities.Where(e =>
                                !extension.types.typemap.TryGetValue(e, out XElement design) || design == null
                            );
                        case AttributeTypes.TYPE_ANY:
                        case AttributeTypes.TYPE_INHERITED:
                            return FindTypes();
                        case AttributeTypes.TYPE_ITEM:
                            return FindItems();
                        case AttributeTypes.TYPE_ITEM_ARMOR:
                            return FindItems("Armor");
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
                            return FindTypes(design =>
                                design.Tag() == "ItemType" && design.Elements().Any(
                                    element => deviceTags.Contains(element.Tag())));
                        case AttributeTypes.TYPE_ITEM_WEAPON:
                            return FindItems("Weapon");
                        //TO DO
                        default:
                            return new List<string>();
                    }
                }
            }
            return null;
            IEnumerable<string> FindItems(string category = null) {
                Func<XElement, bool> filter = d => d.Tag() == "ItemType";
                if(category != null) {
                    filter = d => filter(d) && d.Elements(category).Count() > 0;
                }
                return FindTypes(filter);
            }
            IEnumerable<string> FindTypes(Func<XElement, bool> predicate = null) {
                Func<string, bool> filter =
                    predicate == null ?
                        entity => extension.types.typemap.TryGetValue(entity, out XElement design) && design != null :
                        entity => extension.types.typemap.TryGetValue(entity, out XElement design) && design != null && predicate(design);
                return extension.types.entities.Where(filter);
            }
        }
        public bool CreateExtension(string templateType, string path) {
            if(rootStructures.TryGetValue(templateType, out var template)) {
                template = InitializeTemplate(template);
                var structure = FromTemplate(template);

                var extension = new GameData(
                    path: path,
                    structure: structure
                );
                extensions[path] = extension;
                extension.Save();
                return true;
            }
            return false;
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
            var xml = File.ReadAllText(path);
            //Cheat the XML reader by escaping ampersands so we don't parse entities
            xml = xml.Replace("&", "&amp;");
            var d = new Dictionary<string, Regex>() {
                ["<!--  -->"] = new("<!-(-*)->"),
                ["<!--"] = new Regex("<!--(-+)"),
                ["-->"] = new Regex("(-+)-->"),
            };
            foreach((var str, var reg) in d) {
                xml = reg.Replace(xml, str);
            }

            var doc = new XmlDocument();
            doc.LoadXml(xml);

            LoadExtension(doc, path, out var e);
            if (modules) {
                LoadModules(e);
            }
        }
        public void LoadModules(GameData e) {
            foreach (var module in e.structure.Elements()) {
                var template = bases[module];
                if (template.Att("module", out var key)) {
                    var filename = module.Att(key);
                    //Use the full path when finding modules
                    var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(e.path), filename));
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
            File.WriteAllText(stateFile, JsonConvert.SerializeObject(extensions.Keys.ToList()));
            File.WriteAllText("Schema.json", schemaFile);
        }
        public void LoadState() {
            if(File.Exists(stateFile)) {
                var loaded = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(stateFile));
                foreach(var file in loaded.Where(f => File.Exists(f))) {
                    Load(file);
                }
                BindAll();
            }
        }
    }
}
