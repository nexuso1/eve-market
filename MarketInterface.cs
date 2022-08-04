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
            var name = mainInterface.StringFromSlice(tokens, 2, tokens.Length - 2);

            switch (tokens[1])
            {
                case "region":
                    defaultRegion = name;
                    return;
                case "station":
                    defaultStation = name;
                    return;
                default:
                    output.WriteLine("Invalid name of a default setting");
                    return;
            }
        }
        public void HandleOrders(string[] tokens)
        {
            return;
        }

        public void HandleWallet(string[] tokens)
        {
            if (!mainInterface.IsAuthorized)
            {
                output.WriteLine("No character authorized. Please use command \"authorize\" to authorize one of your characters.");
                return;
            }

            var response = mainInterface.Client.Wallet.CharacterWallet().Result.Data;
            output.WriteLine($"Your current personal account balance is {response} ISK.");
            return;
        }
    }
}
