using SadRogue.Primitives;
using SadConsole.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SadConsole;

namespace Transgenesis;

internal class SmartString {
    public override string ToString() => string.Join(null, text.Select(t => t.c));
    public string raw = "";
    public List<SmartChar> text = new();

    public ColoredString colored => new(text.Select(c => new ColoredGlyph(c.f, c.b, c.c)).ToArray());
    public Color front = Color.White,
          back = Color.Black;
    public int truncate = int.MaxValue,
        indent = 0,
        row = 0,
        col = 0;
    Stack<Command> commands = new();

    string buttonId = "";
    Dictionary<string, Rectangle> buttons = new();


    private void Append(char c) => text.Add(new(c, front, back));
    private void Append(string s) => text.AddRange(s.Select(c => new SmartChar(c, front, back)));
    public SmartString() { }
    public SmartString(string s) => Parse(s);

    

    public void Parse(string s) {
        var i = 0;
        bool Check(out char c) {
            bool b = i < s.Length;
            c = b ? s[i] : '\0';
            return b;
        }
        bool Read(out char c) {
            var b = Check(out c);
            i++;
            return b;
        }
        bool Read2(out char c, out bool eof) {
            eof = Eof();
            return Read(out c);
        }
        bool Eof() => i == s.Length;
        void Back() {
            i--;
        }




        while(Read(out var ch)) {
            switch(ch) {
                case '[':
                    string cmd = "";
                    bool eof;
                    while(Read2(out ch, out eof) && ch != ']') {
                        cmd += ch;
                    }
                    if (eof) {
                        Append(cmd + ch);
                    }
                    var dict = new Regex("(?<key>[a-zA-Z0-9,.]+):(?<val>[a-zA-Z0-9,.]+)").Matches(cmd).ToDictionary(m => Get(m, "key"), m => Get(m, "val"));
                    switch (dict["c"]) {
                        case "r":
                        case "recolor":
                            var c = new Recolor(front, back,
                                Sel<Color?>("f", s => ParseColor(s), null) ?? front,
                                Sel<Color?>("b", s => ParseColor(s), null) ?? back);
                            Apply(c);
                            commands.Push(c);
                            break;
                        case "t":
                        case "truncate":
                            var t = new Truncate(truncate, Sel("w", int.Parse));
                            var wrap = true;
                            Apply(t);
                            commands.Push(t);
                            break;
                        case "i":
                        case "indent":
                            var ind = new Indent(indent, Sel("i", int.Parse));
                            Apply(ind);
                            commands.Push(ind);
                            break;
                        case "button":
                            var bu = new Button(buttonId, Sel("id", s => s, null) ?? throw new Exception("id expected"));
                            Apply(bu);
                            commands.Push(bu);
                            break;
                        case "u":
                        case "undo":
                            Unapply(commands.Peek());
                            commands.Pop();
                            break;
                    }
                    Color ParseColor(string s) {
                        try {
                            if(typeof(Color).GetProperty(s)?.GetValue(null, null) is Color c) {
                                return c;
                            }
                        } catch { }
                        var d = new Dictionary<string, Color> {
                            ["White"] = Color.White,
                            ["LightBlue"] = Color.LightBlue,
                            ["LightGoldenrodYellow"] = Color.LightGoldenrodYellow,
                            ["Salmon"] = Color.Salmon,
                            ["SkyBlue"] = Color.SkyBlue,
                            ["LimeGreen"] = Color.LimeGreen
                        };
                        if(d.TryGetValue(s, out var co)) {
                            return co;
                        }
                        if (new Regex("(?<R>[0-9]+),(?<G>[0-9]+),(?<B>[0-9]+)").Match(s) is Match { Success: true } m) {
                            var p = (string k) => int.Parse(m.Groups[k].Value);
                            return new(p("R"), p("G"), p("B"));
                        }
                        throw new Exception($"color expected ### {s}");
                    }
                    void Handle(string key, Action<string> a) {
                        if(dict.TryGetValue(key, out var val)) {
                            a(val);
                        }
                    }
                    T Sel<T>(string key, Func<string, T> f, T fallback = default) {
                        if (dict.TryGetValue(key, out var val)) {
                            return f(val);
                        }
                        return fallback;
                    }
                    break;
                default:
                    if(ch == '\n') {
                        Append(ch);
                        Append(new string(' ', indent));
                        col = indent;
                        row++;
                        break;
                    }
                    if(col == truncate) {
                        Append('\n');
                        Append(new string(' ', indent));
                        col = indent;
                        row++;


                    }

                    var p = new Point(col, row);
                    if (buttonId.Any()) {
                        if (!buttons.TryGetValue(buttonId, out var rect)) {
                            rect = new(p.X, p.Y, 1, 1);
                            buttons[buttonId] = rect;
                        }
                        if (!rect.Contains(p)) {
                            if(p.X < rect.MinExtentX) {
                                rect = rect.WithMinExtentX(p.X);
                            }
                            if (p.Y < rect.MinExtentY) {
                                rect = rect.WithMinExtentY(p.Y);
                            }
                            if (p.X > rect.MaxExtentX) {
                                rect = rect.WithMaxExtentX(p.X);
                            }
                            if (p.Y > rect.MaxExtentY) {
                                rect = rect.WithMaxExtentY(p.Y);
                            }
                            buttons[buttonId] = rect;
                        }
                    }

                    Append(ch);
                    col++;

                    
                    break;
            }
        }

        void MatchAll(Regex r, string s, Action<Match> a) {
            foreach(Match m in r.Matches(s)) {
                a(m);
            }
        }
        string Get(Match m, string key) {
            return m.Groups[key].Value;
        }
        raw += s;
    }
    private void Apply(Command co) {
        switch (co) {
            case Recolor c:
                (front, back) = (c.f, c.b);
                return;
            case Truncate t:
                (truncate) = (t.w);
                return;
            case Button bu:
                buttonId = bu.id;
                return;
            case Indent i:
                indent = i.i;
                return;
        }
    }
    private void Unapply(Command co) {
        switch (co) {
            case Recolor r:
                (front, back) = (r.fPrev, r.bPrev);
                return;
            case Truncate t:
                truncate = t.wPrev;
                return;
            case Button bu:
                buttonId = bu.idPrev;
                return;
            case Indent i:
                indent = i.iPrev;
                return;
        }
    }
}
public interface Command { }
public record Truncate(int wPrev, int w) : Command { }
public record Indent(int iPrev, int i) : Command { }
public record Recolor(Color fPrev, Color bPrev, Color f, Color b) : Command { }
public record SmartChar(char c, Color f, Color b) { }
public record Button(string idPrev, string id) : Command { }