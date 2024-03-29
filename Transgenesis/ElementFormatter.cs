﻿using SadConsole;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static SadConsole.ColoredString;
using SadRogue.Primitives;
using ArchConsole;

namespace Transgenesis {
    interface UIData {

    }
    public static class UIDataHelper {
        public static void AddRepeat<U>(this List<U> list, U item, int count) {
            list.AddRange(Enumerable.Repeat(item, count));
        }
    }
    class ElementFormatter {
        int tabs = 0;
        string expandedBox = "-   ";
        string collapsedBox = "+   ";
        string noBox = ".   ";

        public SmartString smart = new();
        public List<ColoredString> buffer => smart.colored.Split('\n');
        public HashSet<int> highlightLines = new();

        ConsoleManager c;
        public ElementFormatter(ConsoleManager c, bool showBoxes = true) {
            this.c = c;
            if(!showBoxes) {
                expandedBox = collapsedBox = noBox = "";
            }
            smart.truncate = c.width;
            smart.front = c.theme.front;
            smart.back = c.theme.back;
        }
        void AddLine(string line) {
            smart.Parse($"{line}\n");
        }
        public void ShowElementTree(XElement root, XElement focused, HashSet<XElement> expanded = null, HashSet<XElement> semiexpanded = null, HashSet<XElement> collapsed = null) {
            bool expandAll = expanded == null;
            bool semiexpandAll = semiexpanded == null;
            bool collapseNone = collapsed == null;
            ShowElementTree(root);
            void ShowElementTree(XElement element) {

                const bool expandFocused = false;
                var isFocused = (focused == element);
                var expandedCheck = (expandAll || expanded.Contains(element) || (expandFocused && isFocused)) && (collapseNone || !collapsed.Contains(element));
                var box =
                    !element.Nodes().Any() && element.Attributes().Count() < 4 ?
                        noBox :
                    expandedCheck ?
                        expandedBox :
                    collapsedBox;
                box = isFocused ? RecHighlight(box) : RecBox(box);
                var tagLeft = $"<{element.Tag()}";
                //If we have no attributes, then do not pad any space for attributes
                if (element.Attributes().Count() > 0) {
                    tagLeft = tagLeft.PadRightTab();
            }
                var index = 0;
                var root = element.AncestorsAndSelf().Last();
                var all = root.DescendantsAndSelf().ToList();
                foreach (var d in all) {
                    if(d == element) {
                        break;
                    }
                    index++;
                }

                var button = $"[c:button id:element,{index}]";
                smart.Parse(button);

                AttribToStr Str = (string key, string value) => $"[c:button id:attribute,{index},{key}]{StrPair(key, value)}[c:u]";

                if (element.Nodes().Count() > 0) {
                    Func<string, string> recTag =
                        isFocused ?
                            RecHighlight :
                        RecTag;
                    var closingTag = recTag($"</{element.Tag()}>");


                    if (expandedCheck) {
                        var openingTag = $"{box}{Tab()}{recTag($"{tagLeft}{ShowAllAttributes(element, Str)}>")}";
                        if(element.Nodes().Count() == 1 && element.FirstNode is XText text) {
                            //To do: Generate an equivalent string of metadata objects
                            var t = text.Value.Replace("\t", "    ");
                            AddLine($"{openingTag}{t}{(t.Contains("\n") ? $"\n\r{box}{Tab()}" : "")}{closingTag}");
                            var line = new List<UIData>();
                            line.AddRepeat(null, box.Length);
                        } else {
                            //show all attributes and children
                            AddLine($"{openingTag}");
                            ShowChildren();
                            AddLine($"{box}{Tab()}{closingTag}");
                        }
                    } else {
                        //show only the important attributes and (semi)expanded children
                        var openingTag = $"{box}{Tab()}{recTag($"{tagLeft}{ShowContextAttributes(element, Str)}>")}";
                        if (!semiexpandAll && !element.Elements().Intersect(semiexpanded).Any()) {
                            //We have no important children to show, so just put our whole tag on one line
                            AddLine($"{openingTag}{RecText("...")}{closingTag}");
                        } else {
                            //Show any important children and attributes
                            AddLine($"{openingTag}");
                            tabs++;
                            var skipped = 0;

                            var more = RecTag($"<{RecText("...")}/>");
                            foreach (var child in element.Nodes()) {
                                if(child is XText t) {
                                    skipped++;
                                } else if(child is XElement e) {
                                    if (semiexpandAll || semiexpanded.Contains(e)) {
                                        //Show that we have previous children not shown
                                        if (skipped > 0) {
                                            skipped = 0;
                                            AddLine($"{noBox}{Tab()}{more}");
                                        }
                                        ShowElementTree(e);
                                    } else {
                                        skipped++;
                                    }
                                }
                            }
                            //Show that we have more children not shown
                            if (skipped > 0) {
                                AddLine($"{noBox}{Tab()}{more}");
                            }
                            /*
                            if(element.Value.Length > 0) {
                                AddLine($"{noBox}{Tab()}...");
                            }
                            */
                            tabs--;
                            AddLine($"{box}{Tab()}{closingTag}");
                        }
                    }
                    void ShowChildren() {
                        tabs++;
                        foreach (var child in element.Nodes()) {
                            if (child is XText t) {
                                var text = t.Value.Replace("\t", "    ");
                                AddLine(text);
                            } else if (child is XElement e) {
                                ShowElementTree(e);
                            }
                        }
                        tabs--;
                    }
                } else {
                    Func<string, string> recTag =
                        isFocused ?
                            RecHighlight :
                        RecTag;
                    var att =
                        expandedCheck ?
                            ShowAllAttributes(element, Str) :
                            ShowContextAttributes(element, Str);
                    AddLine($"{box}{Tab()}{recTag($"{tagLeft}{att}/>")}");
                }

                smart.Parse("[c:u]");
            }
        }
        //string Indent(string text) => $"[c:i i:{tabs * 4}]{text}[c:u]";
        string Tab() => new(' ', tabs * 4);
        string ShowContextAttributes(XElement element, AttribToStr Str) {
            var attributes = new Dictionary<string, string>();
            //If we have a few attributes, just show all of them inline
            if (element.Attributes().Count() < 4) {
                foreach (var a in element.Attributes()) {
                    attributes[a.Name.LocalName] = a.Value;
                }
            } else {
                //Otherwise, just show the important ones
                foreach (var key in new string[] { "unid", "name" }) {
                    if (element.Att(key, out var value)) {
                        attributes[key] = value;
                    }
                }
                //Or the first three ones
                foreach (var a in element.Attributes()) {
                    if(attributes.Count >= 3) {
                        break;
                    }
                    attributes[a.Name.LocalName] = a.Value;
                }
            }
            var inline = attributes.Count < 4;
            var more = attributes.Count < element.Attributes().Count();
            return AttributesToString(attributes, inline, more, Str);
        }
        string ShowAllAttributes(XElement element, AttribToStr Str) {
            var attributes = new Dictionary<string, string>();
            foreach (var a in element.Attributes()) {
                attributes[a.Name.LocalName] = a.Value;
            }
            var inline = attributes.Count < 4;
            return AttributesToString(attributes, inline, false, Str);
        }
        public delegate string AttribToStr(string key, string value);
        string AttributesToString(Dictionary<string, string> attributes, bool inline, bool more, AttribToStr Str) {


            if (attributes.Count == 0) {
                return more ? RecText(" ...") : "";
            } else if (inline) {
                

                var result = new StringBuilder();
                var first = attributes.Keys.First();
                result.Append(Str(first, attributes[first]));
                foreach (var key in attributes.Keys.Skip(1)) {
                    result.Append(" ");
                    //Pad space between each attribute so that keys are aligned by tab
                    //We can't align perfectly since we don't know the global position of the string
                    while (result.Length % 4 > 0) {
                        result.Append(' ');
                    }
                    /*
                    if (result.Length%4 > 0) {
                        int aligned = (result.Length - result.Length%4) + 4;
                        result.Append(new string(' ', aligned - result.Length));
                    }
                    */
                    result.Append(Str(key, attributes[key]));
                }
                if (more) {
                    result.Append(" ");
                    result.Append(RecText("..."));
                }
                return result.ToString();
            } else {
                var result = new StringBuilder();
                var first = attributes.Keys.First();
                result.AppendLine($"{Str(first, attributes[first])}");
                tabs += 2;

                //int interval = 8;
                //int padding = (1 + attributes.Keys.Select(k => k.Length).Max() / interval) * interval;
                foreach (var key in attributes.Keys.Skip(1)) {
                    //int padding = (1 + key.Length / interval) * interval;
                    result.AppendLine($@"{noBox}{Tab()}{Str(key, attributes[key])}");
                }
                if (more) {
                    result.AppendLine($"{noBox}{Tab()}{RecText("...")}");
                }
                result.Append($"{noBox}{Tab()}");
                tabs--;
                tabs--;
                return result.ToString();
            }
        }
        public string StrPair(string key, string val, int padding = 0) => $"{RecAtt($"{key}=".PadRight(padding))}{RecQuotes($"\"{val}\"")}";

        public string RecHighlight(string text) => Recolor("LimeGreen", text);
        public string RecBox(string text) => Recolor("White", text);
        public string RecAtt(string text) => Recolor("Salmon", text);
        public string RecText(string text) => Recolor("White", text);
        public string RecEntity(string text) => Recolor("SkyBlue", text);
        public string RecQuotes(string text) => Recolor("LightBlue", text);
        public string RecTag(string text) => Recolor("LightGoldenrodYellow", text);
        public string Recolor(string color, string text) => $"[c:r f:{color}]{text}[c:u]";
    }

    class SyntaxColors {
        public static SyntaxColors std = new SyntaxColors() {
            attribute = Color.Salmon,
            text = Color.White,
            entity = Color.SkyBlue,
            quotes = Color.LightBlue,
            tag = Color.LightGoldenrodYellow,
        };
        public Color attribute, text, entity, quotes, tag, tagHighlight;

    }
}
