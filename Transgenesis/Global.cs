using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Transgenesis {
    public static class Global {
        public static XElement NameElement(this XElement e, string name) {
            return e.Elements("E").First(s => s.Attribute("name")?.Value == name);
        }
        public static XElement TryNameElement(this XElement e, string name) {
            return e.Elements("E").FirstOrDefault(s => s.Attribute("name")?.Value == name);
        }
        public static List<string> GetValidAttributes(this XElement e) {
            return e.Elements("A").Select(a => a.Att("name")).ToList();
        }
        public static string Tag(this XElement e) => e.Name.LocalName;
        public static List<string> GetValidSubelements(this XElement e) {
            return e.Elements("E").Select(a => a.Att("name")).ToList();
        }
        public static bool NameElement(this XElement e, string name, out XElement result) {
            return (result = e.Elements("E").FirstOrDefault(s => s.Attribute("name")?.Value == name)) != null;
        }
        public static string Att(this XElement e, string attribute) {
            return e.Attribute(attribute)?.Value;
        }
        public static bool Att(this XElement e, string attrib, out string result) {
            return (result = e.Attribute(attrib)?.Value) != null;
        }

        public static List<HighlightEntry> GetSuggestions(string input, IEnumerable<string> items) {
            var startsWith = new List<HighlightEntry>();
            var contains = new List<HighlightEntry>();
            foreach (var s in items) {
                if (s.StartsWith(input)) {
                    startsWith.Add(new HighlightEntry() {
                        str = s,
                        highlightStart = 0,
                        highlightLength = input.Length
                    });
                } else {
                    int index = s.IndexOf(input);
                    if (index != -1) {
                        contains.Add(new HighlightEntry() {
                            str = s,
                            highlightStart = index,
                            highlightLength = input.Length
                        }); ;
                    }
                }
            }
            var result = new List<HighlightEntry>();
            result.AddRange(startsWith);
            result.AddRange(contains);
            return result;
        }
        public static T Initialize<S, T>(this Dictionary<S, T> dict, S key, T value) {
            if(dict.ContainsKey(key)) {
                return dict[key];
            } else {
                dict[key] = value;
                return value;
            }
        }
        public static void Break() {
           return;
        }
    }
}
