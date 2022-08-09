using System;
using ESI.NET.Models.SSO;
using System.Collections.Generic;
using System.Text;
using System.IO;
using ESI.NET.Enumerations;
using ESI.NET.Models.Market;

namespace eve_market
{
    public class MarketInterface
    {

        public TextWriter output;
        public MainEsiInterface mainInterface;
        public string defaultStation = "Jita 4 4";
        public string defaultRegion = "The Forge";
        public enum SortOrder { Ascending, Descending };
        public enum SortBy { Price, Date, Volume, Region, Station };

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
        public void HandleOrders(string[] tokens, SortBy sortByType = SortBy.Price, SortOrder sortOrder = SortOrder.Descending)
        {
            if (!mainInterface.CheckAuthorization()) return;

            var data = mainInterface.Client.Market.CharacterOrders().Result.Data;
            var buyOrders = new List<Order>();
            var sellOrders = new List<Order>();
            foreach (var order in data)
            {
                if (order.IsBuyOrder) {
                    buyOrders.Add(order);
                    continue;
                }

                sellOrders.Add(order);
            }

            switch (sortByType)
            {
                case SortBy.Price:
                    sellOrders.Sort((x, y) => x.Price.CompareTo(y.Price));
                    buyOrders.Sort((x, y) => x.Price.CompareTo(y.Price));
                    break;
                case SortBy.Date:
                    sellOrders.Sort((x, y) => x.Issued.CompareTo(y.Issued));
                    buyOrders.Sort((x, y) => x.Issued.CompareTo(y.Issued));
                    break;
                case SortBy.Volume:
                    sellOrders.Sort((x, y) => x.VolumeRemain.CompareTo(y.VolumeRemain));
                    buyOrders.Sort((x, y) => x.VolumeRemain.CompareTo(y.VolumeRemain));
                    break;
            }

            if (sortOrder == SortOrder.Descending)
            {
                buyOrders.Reverse();
                sellOrders.Reverse();
            }

        }

        async public void HandleWallet(string[] tokens)
        {
            if (!mainInterface.CheckAuthorization()) return;

            var response = await mainInterface.Client.Wallet.CharacterWallet();
            var balance = response.Message;
            output.WriteLine($"Your current personal account balance is {balance} ISK.");
            return;
        }
    }
}
