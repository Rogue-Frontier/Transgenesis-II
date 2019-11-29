using System;
using static Transgenesis.Global;
namespace Transgenesis {
    public class HighlightEntry {
        public int highlightStart = -1;
        public int highlightLength = 0;
        public string str;
        public void Draw(ConsoleColor front = ConsoleColor.White, ConsoleColor back = ConsoleColor.Black) {
            var c = ConsoleColor.Green;
            if (highlightStart != -1) {
                Print(str.Substring(0, highlightStart), front, back);
                if(highlightLength != 0) {
                    Print(str.Substring(highlightStart, highlightLength), c, back);
                    Print(str.Substring(highlightStart + highlightLength), front, back);
                } else {
                    Print(str.Substring(highlightStart), front, back);
                }
            } else {
                Print(str);
            }
        }
    }
}
