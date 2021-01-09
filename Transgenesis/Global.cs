using Microsoft.Xna.Framework;
using SadConsole;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Transgenesis {
    public static class Global {

        public static void Add<U, T>(this Dictionary<U, HashSet<T>> dict, U key, params T[] item) {
            if(dict.TryGetValue(key, out var v)) {
                v.UnionWith(item);
            } else {
                dict[key] = new HashSet<T>();
            }
        }
        public static List<string> SplitMulti(this string str, string separator, int length) {
            List<string> result = new List<string>();
            foreach(var l in str.Split(separator)) {
                result.AddRange(l.Split(length));
            }
            return result;
        }
        public static List<ColoredString> SplitMulti(this ColoredString str, char separator, int length) {
            List<ColoredString> result = new List<ColoredString>();
            foreach (var l in str.Split(separator)) {
                
                result.AddRange(l.Split(length));
            }
            return result;
        }
        public static string[] Split(this string str, int length) {
            string[] result = new string[(str.Length + length - 1) / length];
            int i = 0;
            while (str.Length > length) {
                result[i] = str.Substring(0, length);
                str = str.Substring(length);
                i++;
            }
            result[i] = str;
            return result;
        }
        public static ColoredString[] Split(this ColoredString str, int length) {
            ColoredString[] result = new ColoredString[(str.Count + length - 1) / length];
            int i = 0;
            while (str.Count > length) {
                result[i] = str.SubString(0, length);
                str = str.SubString(length);
                i++;
            }
            result[i] = str;
            return result;
        }

        public static List<ColoredString> Split(this ColoredString str, char s) {
            List<ColoredString> result = new List<ColoredString>();
            result.Add(new ColoredString());
            foreach(var cg in str) {
                if(cg.GlyphCharacter == s) {
                    result.Add(new ColoredString());
                } else {
                    result[result.Count - 1] += str.SubString(0, 1);
                }
            }
            result.RemoveAll(s => s.Count == 0);
            return result;
        }

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

        public static string PadRightTab(this string s, int tabSize = 4) {
            return s.PadRight(((s.Length + 1) / tabSize) * tabSize + tabSize);
        }
        public static string TruncatePath(this string path) {
            return path.Substring(new DirectoryInfo(path).Parent.Parent.Parent.FullName.Length);
        }
        public static string Truncate(this string s, int length) {
            return s.Length <= length ? s : s.Substring(0, length - 3) + "...";
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
        public static string ToUNID(this uint i) {
            return $"0x{i.ToString("X").PadLeft(8, '0')}";
        }
    }
}
