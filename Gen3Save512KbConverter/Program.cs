using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gen3Save512KbConverter {
    class Program {
        static int Main( string[] args ) {
            return HyoutaTools.Pokemon.Gen3.Save.Execute( new List<string>( args ) );
        }
    }
}
