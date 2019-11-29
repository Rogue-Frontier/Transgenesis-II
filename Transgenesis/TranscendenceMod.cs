using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace Transgenesis {
    class TranscendenceExtension {
        TranscendenceExtension parent;
        public string path;
        HashSet<TranscendenceExtension> dependencies;
        HashSet<TranscendenceExtension> modules;
        public XElement structure;

    }
}
