using SadRogue.Primitives;
using SadConsole.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
namespace Transgenesis;

internal class SmartString {
    public override string ToString() => string.Join(null, text.Select(t => t.c));
    string raw = "";
    List<SmartChar> text = new();
    Color f = Color.White,
          b = Color.Black;
    int lw = int.MaxValue,
        row = 0,
        col = 0;
    Stack<Command> l = new();

    string buttonId = "";
    Dictionary<string, Rectangle> buttons = new();


    private void Append(char c) => text.Add(new(c, f, b));
    private void Append(string s) => text.AddRange(s.Select(c => new SmartChar(c, f, b)));
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
                            var c = new Recolor(f, b,
                                Sel<Color?>("f", s => ParseColor(s), null) ?? f,
                                Sel<Color?>("b", s => ParseColor(s), null) ?? b);
                            Apply(c);
                            l.Push(c);
                            break;
                        case "t":
                        case "truncate":
                            var t = new Truncate(lw, Sel("w", int.Parse));
                            var wrap = true;
                            Apply(t);
                            l.Push(t);
                            break;
                        case "button":
                            var bu = new Button(buttonId, Sel("id", s => s, null) ?? throw new Exception("id expected"));
                            Apply(bu);
                            l.Push(bu);
                            break;
                        case "u":
                        case "undo":
                            Unapply(l.Peek());
                            l.Pop();
                            break;
                    }
                    Color ParseColor(string s) {
                        try {
                            if(typeof(Color).GetProperty(s)?.GetValue(null, null) is Color c) {
                                return c;
                            }
                        } catch { }
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
                    if(col == lw) {
                        Append('\n');
                        col = 0;
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
                (f, b) = (c.f, c.b);
                return;
            case Truncate t:
                (lw) = (t.w);
                return;
            case Button bu:
                buttonId = bu.id;
                return;
        }
    }
    private void Unapply(Command co) {
        switch (co) {
            case Recolor r:
                (f, b) = (r.fprev, r.bprev);
                return;
            case Truncate t:
                lw = t.wprev;
                return;
            case Button bu:
                buttonId = bu.idprev;
                return;
        }
    }
}
public interface Command { }
public record Truncate(int wprev, int w) : Command { }
public record Recolor(Color fprev, Color bprev, Color f, Color b) : Command { }
public record SmartChar(char c, Color f, Color b) {
}
public record Newline() { }
public record Button(string idprev, string id) : Command { }