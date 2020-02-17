using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Transgenesis {
    class ProgramState {
        public Dictionary<TranscendenceExtension, ElementEditor> sessions = new Dictionary<TranscendenceExtension, ElementEditor>();
        public XElement copied;
    }
}
