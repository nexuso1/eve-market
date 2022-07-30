using System;
using ESI.NET.Models.SSO;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace eve_market
{
    public class MarketInterface
    {

        public TextWriter output;
        public MainEsiInterface mainInterface;
        public string defaultStation = "Jita 4 4";
        public string defaultRegion = "The Forge";

        public MarketInterface(MainEsiInterface @interface, TextWriter textWriter)
        {
            output = textWriter;
            mainInterface = @interface;
        }

        public void HandleDefaults(string[] tokens)
        {
            if (tokens.Length < 3)
            {
                output.WriteLine("Invalid command format.");
            }

            // Slice out the rest of the tokens, make them into an array and join them with ' '.
            var name = string.Join(' ', new ArraySegment<string>(tokens, 2, tokens.Length - 2).ToArray());
            switch (tokens[1])
            {
                case "region":
                    defaultRegion = name;
                    return;
                case "station":
                    defaultStation = name;
                    return;
                default:
                    output.WriteLine("invalid name of a default setting");
                    return;
            }
        }
    }
}
