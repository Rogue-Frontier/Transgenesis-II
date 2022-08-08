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

        public List<ColoredString> buffer = new List<ColoredString>();
        public Dictionary<int, HashSet<LabelButton>> buttonBuffer = new();
        public HashSet<int> highlightLines = new HashSet<int>();

        ConsoleManager c;
        public ElementFormatter(ConsoleManager c, bool showBoxes = true) {
            this.c = c;
            if(!showBoxes) {
                expandedBox = collapsedBox = noBox = "";
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
                var isFocused = (focused == element);
                var expandedCheck = (expandAll || expanded.Contains(element) || (expandFocused && isFocused)) && (collapseNone || !collapsed.Contains(element));
                string box =
                    !element.Nodes().Any() ?
                        noBox :
                    expandedCheck ?
                        expandedBox :
                    collapsedBox;
                string tag = $"<{element.Tag()}";
                //If we have no attributes, then do not pad any space for attributes
                if (element.Attributes().Count() > 0) {
                    tag = tag.PadRightTab();
                }
                if (element.Nodes().Count() > 0) {
                    Action<string> writeTag =
                        isFocused ?
                            AddLineHighlight :
                            AddLine;
                    var closingTag = $"</{element.Tag()}>";
                    if (expandedCheck) {
                        string openingTag = $"{box}{Tab()}{tag}{ShowAllAttributes(element)}>";
                        if(element.Nodes().Count() == 1 && element.FirstNode is XText text) {
                            //To do: Generate an equivalent string of metadata objects
                            var t = text.Value.Replace("\t", "    ");
                            writeTag($"{openingTag}{t}{(t.Contains("\n") ? $"\n\r{box}{Tab()}" : "")}{closingTag}");
                            var line = new List<UIData>();
                            line.AddRepeat(null, box.Length);
                        } else {
                            //show all attributes and children
                            writeTag($"{openingTag}");
                            ShowChildren();
                            writeTag($"{box}{Tab()}</{element.Tag()}>");
                        }
                    } else {
                        //show only the important attributes and (semi)expanded children
                        var openingTag = $"{box}{Tab()}{tag}{ShowContextAttributes(element)}>";
                        if (!semiexpandAll && !element.Elements().Intersect(semiexpanded).Any()) {
                            //We have no important children to show, so just put our whole tag on one line
                            writeTag($"{openingTag}...{closingTag}");
                        } else {
                            //Show any important children and attributes
                            writeTag($"{openingTag}");
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
                            writeTag($"{box}{Tab()}{closingTag}");
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
                    Action<string> writeTag =
                        isFocused ?
                            AddLineHighlight :
                        AddLine;
                    string att =
                        expandedCheck ?
                            ShowAllAttributes(element) :
                            ShowContextAttributes(element);
                    writeTag($"{box}{Tab()}{tag}{att}/>");
                    return;
                }
            }
        }

        string Tab() => new(' ', tabs * 4);
        string ShowContextAttributes(XElement element) {
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
            return AttributesToString(attributes, inline, more);
        }
        string ShowAllAttributes(XElement element) {
            var attributes = new Dictionary<string, string>();
            foreach (var a in element.Attributes()) {
                attributes[a.Name.LocalName] = a.Value;
            }
            var inline = attributes.Count < 4;
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
                var result = new StringBuilder();
                var first = attributes.Keys.First();
                result.AppendLine($@"{first}=""{attributes[first]}""");
                tabs += 2;

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
                        case '.' when type.Peek() != Syntax.Quotes:
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
            quotes = Color.LightBlue,
            tag = Color.LightGoldenrodYellow,
        };
        public Color attribute, text, entity, quotes, tag, tagHighlight;

    }
}
