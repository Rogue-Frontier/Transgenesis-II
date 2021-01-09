using SadConsole;
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
    class ElementFormatter {
        int tabs = 0;
        string expandedBox = "-   ";
        string collapsedBox = "+   ";
        string noBox = "    ";

        public List<ColoredString> buffer = new List<ColoredString>();
        public Dictionary<int, HashSet<LabelButton>> buttonBuffer = new();
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

                string tag = $"<{element.Tag()}";
                //If we have no attributes, then do not pad any space for attributes
                if (element.Attributes().Count() > 0) {
                    tag = tag.PadRightTab();
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
                            writeTag($"{box}{Tab()}{tag}{ShowAllAttributes(element)}>{text.Value.Replace("\t", "    ")}</{element.Tag()}>");
                        } else {
                            //show all attributes and children
                            writeTag($"{box}{Tab()}{tag}{ShowAllAttributes(element)}>");
                            ShowChildren();
                            writeTag($"{box}{Tab()}</{element.Tag()}>");
                        }
                    } else {
                        //show only the important attributes and (semi)expanded children

                        if (!semiexpandAll && !element.Elements().Any(c => semiexpanded.Contains(c))) {
                            //We have no important children to show, so just put our whole tag on one line
                            writeTag($"{box}{Tab()}{tag}{ShowContextAttributes(element)}>...</{element.Tag()}>");
                        } else {
                            //Show any important children and attributes
                            writeTag($"{box}{Tab()}{tag}{ShowContextAttributes(element)}>");
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
                        writeTag($"{box}{Tab()}{tag}{ShowAllAttributes(element)}/>");

                    } else {
                        //show only the important attributes
                        writeTag($"{box}{Tab()}{tag}{ShowContextAttributes(element)}/>");
                    }
                    return;
                }
            }

        }

        string Tab() => new string(' ', tabs * 4);
        string ShowContextAttributes(XElement element, Dictionary<int, HashSet<LabelButton>> buttons = null) {
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
            return AttributesToString(attributes, inline, more, buttons);
        }
        string ShowAllAttributes(XElement element, Dictionary<int, HashSet<LabelButton>> buttons = null) {
            Dictionary<string, string> attributes = new Dictionary<string, string>();
            foreach (var attribute in element.Attributes()) {
                attributes[attribute.Name.LocalName] = attribute.Value;
            }
            bool inline = attributes.Count < 4;
            return AttributesToString(attributes, inline, false, buttons);
        }
        string AttributesToString(Dictionary<string, string> attributes, bool inline, bool more, Dictionary<int, HashSet<LabelButton>> buttons = null) {
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
            type.Push(Syntax.Space);
            foreach (var line in buffer) {
                foreach (var glyph in line) {
                    switch (glyph.GlyphCharacter) {
                        case ';' when type.Peek() == Syntax.Entity:
                            glyph.Foreground = SyntaxColors.std.entity;
                            type.Pop();
                            break;
                        case '"':
                            if (type.Peek() == Syntax.Quotes) {
                                type.Pop();
                            } else {
                                type.Push(Syntax.Quotes);
                            }
                            glyph.Foreground = SyntaxColors.std.quotes;
                            break;
                        case '=' when type.Peek() == Syntax.Attribute:
                            glyph.Foreground = SyntaxColors.std.attribute;
                            type.Pop();
                            break;
                        case '+' when type.Peek() != Syntax.Quotes:
                        case '-' when type.Peek() != Syntax.Quotes:
                            break;
                        case '<':
                            if (type.Peek() != Syntax.Tag) {
                                type.Push(Syntax.Tag);
                                if (glyph.Foreground != c.theme.highlight) {
                                    glyph.Foreground = SyntaxColors.std.tag;
                                }
                            }
                            break;
                        case '/' when type.Peek() == Syntax.Tag:
                            if (glyph.Foreground != c.theme.highlight) {
                                glyph.Foreground = SyntaxColors.std.tag;
                            }
                            break;
                        case '/' when type.Peek() == Syntax.Attribute:
                            type.Pop();
                            if (glyph.Foreground != c.theme.highlight) {
                                glyph.Foreground = SyntaxColors.std.tag;
                            }
                            break;
                        case '>' when type.Peek() == Syntax.Tag:
                            type.Pop();
                            if (glyph.Foreground != c.theme.highlight) {
                                glyph.Foreground = SyntaxColors.std.tag;
                            }
                            break;
                        case '>' when type.Peek() == Syntax.Attribute:
                            type.Pop();
                            if (glyph.Foreground != c.theme.highlight) {
                                glyph.Foreground = SyntaxColors.std.tag;
                            }
                            break;
                        case '&':
                            type.Push(Syntax.Entity);
                            glyph.Foreground = SyntaxColors.std.entity;
                            break;
                        case var c when char.IsWhiteSpace(c) && type.Peek() == Syntax.Tag:
                            type.Push(Syntax.Attribute);
                            break;
                        case var c when char.IsWhiteSpace(c) && type.Peek() == Syntax.Attribute:
                            //type.Pop();
                            break;
                        default:
                            switch (type.Peek()) {
                                case Syntax.Tag:
                                    if (glyph.Foreground != c.theme.highlight) {
                                        glyph.Foreground = SyntaxColors.std.tag;
                                    }
                                    break;
                                case Syntax.Attribute:
                                    glyph.Foreground = SyntaxColors.std.attribute;
                                    break;
                                case Syntax.Entity:
                                    glyph.Foreground = SyntaxColors.std.entity;
                                    break;
                                case Syntax.Quotes:
                                    glyph.Foreground = SyntaxColors.std.quotes;
                                    break;
                            }
                            break;
                    }
                }
            }
        }
    }

    class SyntaxColors {
        public static SyntaxColors std = new SyntaxColors() {
            attribute = Color.Salmon,
            text = Color.White,
            entity = Color.SkyBlue,
            quotes = Color.MediumSlateBlue,
            tag = Color.LightGoldenrodYellow,
        };
        public Color attribute, text, entity, quotes, tag, tagHighlight;

    }
}
