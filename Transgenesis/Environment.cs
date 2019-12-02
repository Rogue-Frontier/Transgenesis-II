using System;
using System.Collections.Generic;
using System.Linq;
using static Transgenesis.Global;
using System.Xml;
using System.Xml.Linq;

namespace Transgenesis {
    class Environment {
        public XElement hierarchy;
        public Dictionary<string, XElement> coreStructures = new Dictionary<string, XElement>();
        public Dictionary<string, XElement> baseStructures = new Dictionary<string, XElement>();
        public Dictionary<XElement, XElement> bases = new Dictionary<XElement, XElement>();
        public Dictionary<string, TranscendenceExtension> extensions = new Dictionary<string, TranscendenceExtension>();
        public Dictionary<string, List<string>> customAttributeValues;


        public Environment() {

            XmlDocument doc = new XmlDocument();
            try {
                doc.Load("Transcendence.xml");
            } catch {
                doc.Load("../../../Transcendence.xml");
            }
            hierarchy = XElement.Parse(doc.OuterXml);
            baseStructures["Hierarchy"] = hierarchy;
            foreach (var coreStructure in hierarchy.Elements("E").Where(e => (string)e.Attribute("category") != "virtual")) {
                coreStructures[(string)coreStructure.Attribute("name")] = coreStructure;
                baseStructures[(string)coreStructure.Attribute("name")] = coreStructure;
            }
            foreach (var baseStructure in hierarchy.Elements("E").Where(e => (string)e.Attribute("category") == "virtual")) {
                baseStructures[(string)baseStructure.Attribute("id")] = baseStructure;
            }

            customAttributeValues = new Dictionary<string, List<string>>();
            foreach(var attributeType in hierarchy.Elements("AttributeType")) {
                customAttributeValues[attributeType.Att("name")] = new List<string>(attributeType.Value.Replace("\t", "").Split('\r', '\n').Where(s => !string.IsNullOrWhiteSpace(s)));
            }
        }
        public bool CanAddElement(XElement element, XElement template, string subelement, out XElement subtemplate) {
            subtemplate = template.TryNameElement(subelement);
            if(subtemplate == null) {
                return false;
            }
            subtemplate = InitializeTemplate(subtemplate);
            return CanAddElement(element, subtemplate);
        }
        public bool CanRemoveElement(XElement element, XElement template, string subelement) {
            return CanRemoveElement(element, template.Elements("E").First(e => e.Att("name") == subelement));
        }
        public static bool CanAddElement(XElement element, XElement subtemplate) {
            switch(subtemplate.Att("category")) {
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
        public static bool CanRemoveElement(XElement element, XElement subtemplate) {
            switch (subtemplate.Att("category")) {
                case "*":
                case "?":
                    return element.Elements(subtemplate.Att("name")).Count() > 1;
                case "+":
                    return element.Elements(subtemplate.Att("name")).Count() > 1;
                case "1":
                    return false;
                default:
                    return false;
            }
        }
        public List<string> GetAddableElements(XElement element, XElement template) {
            return template.Elements("E").Select(subtemplate => InitializeTemplate(subtemplate)).Where(subtemplate => CanAddElement(element, subtemplate)).Select(subtemplate => subtemplate.Att("name")).ToList();
        }
        public List<string> GetRemovableElements(XElement element, XElement template) {
            return template.Elements("E").Select(subtemplate => InitializeTemplate(subtemplate)).Where(subtemplate => CanRemoveElement(element, subtemplate)).Select(subtemplate => subtemplate.Att("name")).ToList();
        }
        public void Unload(TranscendenceExtension e) {
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
        public bool LoadExtension(XmlDocument doc, string path, out TranscendenceExtension e) {
            var structure = XElement.Parse(doc.OuterXml);
            if (Enum.TryParse(structure.Tag(), out ExtensionTypes ex)) {
                XElement template;
                switch (ex) {
                    case ExtensionTypes.TranscendenceAdventure:
                        template = coreStructures["TranscendenceAdventure"];
                        break;
                    case ExtensionTypes.TranscendenceExtension:
                        template = coreStructures["TranscendenceExtension"];
                        break;
                    case ExtensionTypes.TranscendenceLibrary:
                        template = coreStructures["TranscendenceLibrary"];
                        break;
                    case ExtensionTypes.TranscendenceModule:
                        template = coreStructures["TranscendenceModule"];
                        break;
                    default:
                        e = null;
                        return false;
                }
                template = InitializeTemplate(template);
                var extension = new TranscendenceExtension(path, structure);

                if(extensions.TryGetValue(path, out TranscendenceExtension existing)) {
                    Unload(existing);
                }

                extensions[path] = extension;
                LoadWithTemplate(structure, template);

                //Load entities
                if(doc?.DocumentType?.Entities != null) {
                    foreach (XmlEntity entity in doc.DocumentType.Entities) {
                        extension.types.elements.Add(new TypeEntry(entity.InnerText, entity.Name));
                    }
                }
                e = extension;
                return true;
            }
            e = null;
            return false;
        }
        public void LoadWithTemplate(XElement structure, XElement template) {
            bases[structure] = template;
            Dictionary<string, XElement> subtemplates = new Dictionary<string, XElement>();
            foreach(XElement subtemplate in template.Elements()) {
                var initialized = InitializeTemplate(subtemplate);
                string name = initialized.Att("name");
                subtemplates[name] = initialized;
            }
            foreach(XElement subelement in structure.Elements()) {
                string name = subelement.Tag();
                if(subtemplates.TryGetValue(name, out XElement subtemplate) || subtemplates.TryGetValue("*", out subtemplate)) {
                    LoadWithTemplate(subelement, subtemplate);
                }
            }
        }
        public XElement FromTemplate(XElement template) {
            XElement result = new XElement(template.Att("name"));
            foreach(XElement subtemplate in template.Elements("E").Where(e => e.Att("category") == "1" || e.Att("category") == "+")) {
                var initialized = InitializeTemplate(subtemplate);
                var subelement = FromTemplate(initialized);
                bases[subelement] = initialized;
                result.Add(subelement);
            }
            //Initialize attributes to default values
            foreach (XElement attributeType in template.Elements("A")) {
                string attribute = attributeType.Att("name");
                string value = attributeType.Att("value");
                if(value != null) {
                    result.SetAttributeValue(attribute, value);
                }
            }

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
                XElement inherited = baseStructures[source];

                //Handle the inheritance chain
                inherited = InitializeTemplate(inherited);

                foreach (string part in parts.Skip(1)) {
                    //template = template.Element(part);
                    inherited = inherited.Elements("E").First(e => e.Att("name") == part);

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
        public List<string> GetAttributeValues(string attributeType) {
            if(customAttributeValues.TryGetValue(attributeType, out List<string> values)) {
                return values;
            } else if(Enum.TryParse<AttributeTypes>(attributeType, out AttributeTypes attributeTypeEnum)) {
                switch(attributeTypeEnum) {
                    //TO DO
                    default:
                        return new List<string>();
                }
            } else {
                return new List<string>();
            }
        }
        public void CreateExtension(ExtensionTypes e, string path) {
            XElement structure;
            XElement template;
            TranscendenceExtension extension;

            switch (e) {
                case ExtensionTypes.TranscendenceAdventure:
                    template = coreStructures["TranscendenceAdventure"];
                    break;
                case ExtensionTypes.TranscendenceExtension:
                    template = coreStructures["TranscendenceExtension"];
                    break;
                case ExtensionTypes.TranscendenceLibrary:
                    template = coreStructures["TranscendenceLibrary"];
                    break;
                case ExtensionTypes.TranscendenceModule:
                    template = coreStructures["TranscendenceModule"];
                    break;
                default:
                    return;
            }
            template = InitializeTemplate(template);
            structure = FromTemplate(template);

            extension = new TranscendenceExtension(
                path: path,
                structure: structure
            );
            extensions[path] = extension;
            extension.Save();
        }
    }
    enum ExtensionTypes {
        TranscendenceUniverse,
        CoreLibrary,
        TranscendenceAdventure,
        TranscendenceExtension,
        TranscendenceLibrary,
        TranscendenceModule
    }
}
