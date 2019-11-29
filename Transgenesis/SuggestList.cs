using System;
using System.Collections.Generic;
using System.Text;

namespace Transgenesis {
    class SuggestList {
        public string[] options;
        public void GetSuggestions(string input, in List<HighlightEntry> startWith, in List<HighlightEntry> contain) {
            foreach(var s in options) {
                if (s.StartsWith(input)) {
                    startWith.Add(new HighlightEntry() {
                        str = s,
                        highlightStart = 0,
                        highlightLength = input.Length
                    });
                } else {
                    int index = s.IndexOf(input);
                    if (index != -1) {
                        contain.Add(new HighlightEntry() {
                            str = s,
                            highlightStart = index,
                            highlightLength = input.Length
                        }); ;
                    }
                }
            }
        }
        public List<HighlightEntry> GetSuggestions(string input) {
            var r = new List<HighlightEntry>();
            GetSuggestions(input, r, r);
            return r;
        }
    }
}
