using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Transgenesis {
    class ProgramState {
        public Dictionary<GameData, ElementEditor> sessions = new Dictionary<GameData, ElementEditor>();
        public XElement copied;
    }
}
