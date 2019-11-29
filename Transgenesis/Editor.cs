using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
namespace Transgenesis {
    class Editor {
        Stack<Screen> history;
        public void Jump(string input) {
            if (input.StartsWith('&')) {
                string type = input.TakeWhile(c => c != '.').ToString();


            }
        }
        public void JumpType(string input) {

        }
    }

    class Screen {

    }
    class ElementScreen {

    }
    class AttributeScreen {

    }
}
