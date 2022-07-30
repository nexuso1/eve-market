using System;
using System.Collections.Generic;
using System.IO;

namespace eve_market
{
    public class UniverseInterface
    {
        public TextWriter output;
        public MainEsiInterface mainInterface;

        public UniverseInterface(MainEsiInterface @interface, TextWriter textWriter)
        {
            output = textWriter;
            mainInterface = @interface;
        }
    }
}
