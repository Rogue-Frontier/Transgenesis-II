using SadRogue.Primitives;
using SadConsole;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SadConsole.ColoredString;

namespace Transgenesis {
    class TextFormatter {
        public List<ColoredString> buffer;
        public HashSet<int> highlightLines;
        int width;
        Color highlight;
        Color front;
        Color back;
        public TextFormatter(ConsoleManager c) {
            this.buffer = new List<ColoredString>();
            this.highlightLines = new HashSet<int>();
            this.width = c.width;
            this.highlight = c.theme.highlight;
            this.front = c.theme.front;
            this.back = c.theme.back;
        }
        public static TextFormatter Format(ConsoleManager c, string text) {
            TextFormatter t = new TextFormatter(c);
            t.AddLine(text);
            return t;
        }
        public ColoredGlyph this[int index] {
            get {
                int lineIndex = 0;
                while (index >= buffer[lineIndex].Length) {
                    index -= buffer[lineIndex].Length;
                    lineIndex++;
                }
                var line = buffer[lineIndex];
                return line[index];
            }
            set {
                int lineIndex = 0;
                while (index >= buffer[lineIndex].Length) {
                    index -= buffer[lineIndex].Length;
                    lineIndex++;
                }
                var line = buffer[lineIndex];
                line[index] = (ColoredGlyphEffect)value;
            }
        }
        public void AddChar(ColoredGlyph c) {
            if(buffer.Count > 0) {
                if (buffer.Last().Length + 1 < width) {
                    buffer[buffer.Count- 1] = buffer.Last() + new ColoredString(c);
                }
            } else {
                buffer.Add(new ColoredString(c));
            }
            
        }
        public void AddLine(string line) {
            int index = 0;
            ColoredString s = new ColoredString(width);
            bool newline = false;
            foreach (var ch in line) {
                if (ch == '\n') {
                    newline = true;
                    buffer.Add(s.SubString(0, index));
                    s = new ColoredString(width);
                    index = 0;
                    continue;
                } else {
                    newline = false;
                }
                s[index] = (ColoredGlyphEffect) new ColoredGlyph(back, front, ch);
                index++;
                if (index == width) {
                    buffer.Add(s);
                    s = new ColoredString(width);
                    index = 0;
                }
            }
            if (index > 0 || newline) {
                buffer.Add(s.SubString(0, index));
            }
        }
        public void AddLineHighlight(string line) {
            int index = 0;
            ColoredString s = new ColoredString(width);
            bool newline = false;
            foreach (var ch in line) {
                if (ch == '\n') {
                    newline = true;
                    highlightLines.Add(buffer.Count);
                    buffer.Add(s.SubString(0, index));
                    s = new ColoredString(width);
                    index = 0;
                    continue;
                } else {
                    newline = false;
                }
                s[index] = (ColoredGlyphEffect) new ColoredGlyph(back, highlight, ch);
                index++;
                if (index == width) {
                    highlightLines.Add(buffer.Count);
                    buffer.Add(s);
                    s = new ColoredString(width);
                    index = 0;
                }
            }
            if (index > 0 || newline) {
                highlightLines.Add(buffer.Count);
                buffer.Add(s.SubString(0, index));
            }
        }
    }
}
