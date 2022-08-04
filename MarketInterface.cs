using System;
using ESI.NET.Models.SSO;
using System.Collections.Generic;
using System.Text;
using System.IO;
using ESI.NET.Enumerations;

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
            if (!mainInterface.CheckAuthorization()) return;

            var data = mainInterface.Client.Market.CharacterOrders().Result.Data;
            var buyOrders = new List<ESI.NET.Models.Market.Order>;
            foreach (var order in data)
            {
                if (order.IsBuyOrder) {
                    buyOrders.Add(order);
                    continue;
                }

                // TODO: Implement this
                // typeName = mainInterface.universeInterface.SearchId(order.TypeId, SearchCategory.InventoryType);
            }
        }

        public void HandleWallet(string[] tokens)
        {
            if (!mainInterface.CheckAuthorization()) return;

            var data = mainInterface.Client.Wallet.CharacterWallet().Result.Data;
            output.WriteLine($"Your current personal account balance is {data} ISK.");
            return;
        }
    }
}
