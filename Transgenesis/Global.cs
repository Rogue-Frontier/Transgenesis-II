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
            if(!dict.TryGetValue(key, out var v)) {
                v = dict[key] = new(item);
            }
            v.UnionWith(item);
        }
        public static List<string> SplitMulti(this string str, string separator, int length) =>
            new(str.Split(separator).SelectMany(l => l.Split(length)));
        public static List<ColoredString> SplitMulti(this ColoredString str, char separator, int length) =>
            new(str.Split(separator).SelectMany(l => l.Split(length)));
        public static string[] Split(this string str, int length) {
            if (str.Length < length) {
                return new[] { str };
            }
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
            if (str.Length < length) {
                return new[] { str };
            }
            ColoredString[] result = new ColoredString[(str.Length + length - 1) / length];
            int i = 0;
            while (str.Length > length) {
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
            result.RemoveAll(s => s.Length == 0);
            return result;
        }
        public static XElement NameElement(this XElement e, string name) =>
            e.Elements("E").First(s => s.Attribute("name")?.Value == name);
        public static bool TryNameElement(this XElement e, string name, out XElement result) =>
            (result = e.Elements("E").FirstOrDefault(s => s.Attribute("name")?.Value == name)) != null;
        public static XElement NameAttribute(this XElement e, string name) =>
            e.Elements("A").First(s => s.Attribute("name")?.Value == name);
        public static bool TryNameAttribute(this XElement e, string name, out XElement result) =>
            (result = e.Elements("A").FirstOrDefault(s => s.Attribute("name")?.Value == name)) != null;
        public static bool TryGetValueType(this XElement e, string name, out string result) =>
            (result = e.Elements("A").FirstOrDefault(s => s.Attribute("name")?.Value == name)?.Att("type")) != null;

        public static string GetDesc(this XElement e, string name) =>
            e.Elements("A").FirstOrDefault(s => s.Attribute("name")?.Value == name).Att("desc");
        public static List<string> GetValidAttributes(this XElement e) =>
            e.Elements("A").Select(a => a.Att("name")).ToList();
        public static string Tag(this XElement e) => e.Name.LocalName;
        public static List<string> GetValidSubelements(this XElement e) =>
            e.Elements("E").Select(a => a.Att("name")).ToList();
        public static bool NameElement(this XElement e, string name, out XElement result) =>
            (result = e.Elements("E").FirstOrDefault(s => s.Attribute("name")?.Value == name)) != null;
        public static string Att(this XElement e, string attribute) =>
            e.Attribute(attribute)?.Value;
        public static bool Att(this XElement e, string attrib, out string result) =>
            (result = e.Attribute(attrib)?.Value) != null;
        public static string PadRightTab(this string s, int tabSize = 4) =>
            s.PadRight(((s.Length + 1) / tabSize) * tabSize + tabSize);
        public static string TruncatePath(this string path) =>
            path.Substring(new DirectoryInfo(path).Parent.Parent.Parent.FullName.Length);
        public static string Truncate(this string s, int length) =>
            s.Length <= length ? s : s.Substring(0, length - 3) + "...";
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
        public static T Initialize<S, T>(this Dictionary<S, T> dict, S key, T value) =>
            dict.ContainsKey(key) ? dict[key] : dict[key] = value;
        public static void Break() {
           return;
        }
        public static string ToUNID(this uint i) =>
            $"0x{i.ToString("X").PadLeft(8, '0')}";
    }
}