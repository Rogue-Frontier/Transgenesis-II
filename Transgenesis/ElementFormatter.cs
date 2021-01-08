﻿using SadConsole;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static SadConsole.ColoredString;
using SadRogue.Primitives;

namespace Transgenesis {
    class ElementFormatter {
        int tabs = 0;
        string expandedBox = "-   ";
        string collapsedBox = "+   ";
        string noBox = "    ";

        public List<ColoredString> buffer = new List<ColoredString>();
        public HashSet<int> highlightLines = new HashSet<int>();

        ConsoleManager c;
        public ElementFormatter(ConsoleManager c, bool showBoxes = true) {
            this.c = c;
            if(!showBoxes) {
                expandedBox = "";
                collapsedBox = "";
                noBox = "";
            }
        }
        void AddLine(string line) {
            buffer.AddRange(line.SplitMulti("\n", c.width).Select(s => new ColoredString(s, c.theme.front, c.theme.back)));
        }
        void AddLineHighlight(string line) {
            var parts = line.SplitMulti("\n", c.width).Select(s => new ColoredString(s, c.theme.highlight, c.theme.back));
            highlightLines.UnionWith(Enumerable.Range(buffer.Count, parts.Count()));
            buffer.AddRange(parts);
        }
        public void ShowElementTree(XElement root, XElement focused, HashSet<XElement> expanded = null, HashSet<XElement> semiexpanded = null, HashSet<XElement> collapsed = null) {
            bool expandAll = expanded == null;
            bool semiexpandAll = semiexpanded == null;
            bool collapseNone = collapsed == null;
            ShowElementTree(root);
            void ShowElementTree(XElement element) {
                const bool expandFocused = false;
                var isFocused = focused == element;

                bool expandedCheck = (expandAll || expanded.Contains(element) || (expandFocused && isFocused)) && (collapseNone || !collapsed.Contains(element));
                string box;
                if (expandedCheck) {
                    box = expandedBox;
                } else if(element.Nodes().Any()) {
                    box = collapsedBox;
                } else {
                    box = noBox;
                }

                string tagStart = $"<{element.Tag()}";
                //If we have no attributes, then do not pad any space for attributes
                if (element.Attributes().Count() > 0) {
                    tagStart = tagStart.PadRightTab();
                }

                if (element.Nodes().Count() > 0) {
                    Action<string> writeTag;
                    if (isFocused) {
                        writeTag = s => AddLineHighlight(s);
                    } else {
                        writeTag = s => AddLine(s);
                    }
                    if (expandedCheck) {
                        if(element.Nodes().Count() == 1 && element.FirstNode is XText text) {
                            writeTag($"{box}{Tab()}{tagStart}{ShowAllAttributes(element)}>{text.Value.Replace("\t", "    ")}</{element.Tag()}>");
                        } else {
                            //show all attributes and children
                            writeTag($"{box}{Tab()}{tagStart}{ShowAllAttributes(element)}>");
                            ShowChildren();
                            writeTag($"{box}{Tab()}</{element.Tag()}>");
                        }
                    } else {
                        //show only the important attributes and (semi)expanded children

                        if (!semiexpandAll && !element.Elements().Any(c => semiexpanded.Contains(c))) {
                            //We have no important children to show, so just put our whole tag on one line
                            writeTag($"{box}{Tab()}{tagStart}{ShowContextAttributes(element)}>...</{element.Tag()}>");
                        } else {
                            //Show any important children and attributes
                            writeTag($"{box}{Tab()}{tagStart}{ShowContextAttributes(element)}>");
                            tabs++;
                            int skipped = 0;

                            foreach (var child in element.Nodes()) {
                                if(child is XText t) {
                                    skipped++;
                                } else if(child is XElement e) {
                                    if (semiexpandAll || semiexpanded.Contains(e)) {
                                        //Show that we have previous children not shown
                                        if (skipped > 0) {
                                            skipped = 0;
                                            AddLine($"{noBox}{Tab()}<.../>");
                                        }
                                        ShowElementTree(e);
                                    } else {
                                        skipped++;
                                    }
                                }
                            }
                            
                            //Show that we have more children not shown
                            if (skipped > 0) {
                                AddLine($"{noBox}{Tab()}<.../>");
                            }
                            /*
                            if(element.Value.Length > 0) {
                                AddLine($"{noBox}{Tab()}...");
                            }
                            */
                            tabs--;
                            writeTag($"{box}{Tab()}</{element.Tag()}>");
                        }
                    }
                    return;
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

                    Action<string> writeTag;
                    if (isFocused) {
                        writeTag = s => AddLineHighlight(s);
                    } else {
                        writeTag = s => AddLine(s);
                    }
                    if (expandedCheck) {
                        //show all attributes
                        writeTag($"{box}{Tab()}{tagStart}{ShowAllAttributes(element)}/>");

                    } else {
                        //show only the important attributes
                        writeTag($"{box}{Tab()}{tagStart}{ShowContextAttributes(element)}/>");
                    }
                    return;
                }
            }

        }

        string Tab() => new string(' ', tabs * 4);
        string ShowContextAttributes(XElement element) {
            Dictionary<string, string> attributes = new Dictionary<string, string>();

            //If we have a few attributes, just show all of them inline
            if (element.Attributes().Count() < 4) {
                foreach (var attribute in element.Attributes()) {
                    attributes[attribute.Name.LocalName] = attribute.Value;
                }
            } else {
                //Otherwise, just show the important ones
                foreach (var key in new string[] { "unid", "name" }) {
                    if (element.Att(key, out string value)) {
                        attributes[key] = value;
                    }
                }

                //Or the first three ones
                foreach (var attribute in element.Attributes()) {
                    if (attributes.Count < 3) {
                        attributes[attribute.Name.LocalName] = attribute.Value;
                    } else {
                        break;
                    }
                }
            }


            bool inline = attributes.Count < 4;
            bool more = attributes.Count < element.Attributes().Count();
            return AttributesToString(attributes, inline, more);
        }
        string ShowAllAttributes(XElement element) {
            Dictionary<string, string> attributes = new Dictionary<string, string>();
            foreach (var attribute in element.Attributes()) {
                attributes[attribute.Name.LocalName] = attribute.Value;
            }
            bool inline = attributes.Count < 4;
            return AttributesToString(attributes, inline, false);
        }
        string AttributesToString(Dictionary<string, string> attributes, bool inline, bool more) {
            if (attributes.Count == 0) {
                return more ? " ..." : "";
            } else if (inline) {
                StringBuilder result = new StringBuilder();
                var first = attributes.Keys.First();
                result.Append($@"{first}=""{attributes[first]}""");
                foreach (string key in attributes.Keys.Skip(1)) {
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
                    result.Append($@"{key}=""{attributes[key]}""");
                }
                if (more) {
                    result.Append(" ");
                    result.Append("...");
                }
                return result.ToString();
            } else {
                StringBuilder result = new StringBuilder();
                string first = attributes.Keys.First();
                result.AppendLine($@"{first}=""{attributes[first]}""");
                tabs++;
                tabs++;

                int interval = 8;
                //int padding = (1 + attributes.Keys.Select(k => k.Length).Max() / interval) * interval;
                foreach (string key in attributes.Keys.Skip(1)) {
                    int padding = (1 + key.Length / interval) * interval;
                    result.AppendLine($@"{noBox}{Tab()}{$"{key}=".PadRight(padding)}""{attributes[key]}""");
                }
                if (more) {
                    result.AppendLine($"{noBox}{Tab()}...");
                }
                result.Append($"{noBox}{Tab()}");
                tabs--;
                tabs--;
                return result.ToString();
            }
        }

        public void SyntaxHighlight() {
            Stack<Syntax> type = new Stack<Syntax>();
            foreach (var line in buffer) {
                foreach (var glyph in line) {
                    if (type.Count == 0 || type.Peek() == Syntax.Space) {
                        if (char.IsWhiteSpace(glyph.GlyphCharacter)) {
                            continue;
                        } else if (type.Count > 0 && type.Peek() == Syntax.Space) {
                            type.Pop();
                        }
                        PushGlyph:
                        switch (glyph.GlyphCharacter) {
                            /*
                            case var c when char.IsLetterOrDigit(c):
                                if(type.Any() && (type.Peek() == Syntax.Tag || type.Peek() == Syntax.FocusedTag)) {
                                    type.Push(Syntax.Attribute);
                                } else {
                                    type.Push(Syntax.Text);
                                }
                                break;
                                */
                            case var c when char.IsLetterOrDigit(c) && type.Any() && type.Peek() == Syntax.Tag:
                                type.Push(Syntax.Attribute);
                                break;
                            case '"':
                                //Since we pop upon seeing the opening quote
                                type.Push(Syntax.Quotes);
                                type.Push(Syntax.Quotes);
                                break;
                            case '<':
                                type.Push(Syntax.Tag);
                                break;
                            case '&':
                                type.Push(Syntax.Entity);
                                break;
                            default:
                                type.Push(Syntax.Text);
                                break;
                        }
                    }

                CheckType:
                    switch (type.Peek()) {
                        case Syntax.Attribute:
                            glyph.Foreground = Color.Salmon;
                            if (glyph.GlyphCharacter == '=') {
                                //If we've found the end of the attribute name, add a space so that we can catch if the value is immediately in front of it.
                                type.Pop();
                                type.Push(Syntax.Space);
                            }
                            break;
                        case Syntax.Text:
                            glyph.Foreground = Color.White;
                            switch (glyph.GlyphCharacter) {
                                case '"':
                                    //Since we pop upon seeing the opening quote
                                    type.Push(Syntax.Quotes);
                                    type.Push(Syntax.Quotes);
                                    goto CheckType;
                                case '<':
                                    type.Pop();
                                    type.Push(Syntax.Tag);
                                    goto CheckType;
                                case '&':
                                    type.Push(Syntax.Entity);
                                    goto CheckType;
                            }
                            break;
                        case Syntax.Entity:
                            glyph.Foreground = Color.SkyBlue;
                            if (glyph.GlyphCharacter == ';') {
                                type.Pop();
                            }
                            break;
                        case Syntax.Quotes:
                            //If we encounter a space within quotes, we treat it as part of the quotes
                            glyph.Foreground = Color.MediumSlateBlue;

                            if (glyph.GlyphCharacter == '&') {
                                type.Push(Syntax.Entity);
                                goto CheckType;
                            } else if (glyph.GlyphCharacter == '"') {
                                type.Pop();
                            }
                            break;
                        case Syntax.Tag:
                            if(glyph.Foreground != c.theme.highlight) {
                                glyph.Foreground = Color.LightGoldenrodYellow;
                            }
                            if (glyph.GlyphCharacter == '>') {
                                type.Pop();
                            } else if (char.IsWhiteSpace(glyph.GlyphCharacter)) {
                                type.Push(Syntax.Space);
                            }
                            break;
                    }
                }
            }
        }
    }
}
