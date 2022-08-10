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

        /// <summary>
        /// Sets default values for region or station used when displaying market listings.
        /// </summary>
        /// <param name="tokens">Tokens from the input, including the original command</param>
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

        /// <summary>
        /// Sorts orders based on various fields defined by SortBy enum and in ascending or descending order
        /// </summary>
        /// <param name="orders"></param>
        /// <param name="sortByType"></param>
        /// <param name="sortOrder"></param>
        public void SortOrders(List<Order> orders, SortBy sortByType, SortOrder sortOrder)
        {
            switch (sortByType)
            {
                case SortBy.Price:
                    orders.Sort((x, y) => x.Price.CompareTo(y.Price));
                    break;
                case SortBy.Date:
                    orders.Sort((x, y) => x.Issued.CompareTo(y.Issued));
                    break;
                case SortBy.Volume:
                    orders.Sort((x, y) => x.VolumeRemain.CompareTo(y.VolumeRemain));
                    break;
            }

            if (sortOrder == SortOrder.Descending)
            {
                orders.Reverse();
            }
        }

        public void HandleOrders(string[] tokens, SortBy sortByType = SortBy.Price, SortOrder sortOrder = SortOrder.Descending)
        {
            if (!mainInterface.CheckAuthorization()) return;

            // Gather the orders
            var data = mainInterface.Client.Market.CharacterOrders().Result.Data;
            if (data.Count == 0)
            {
                output.WriteLine("No orders to print.");
                return;
            }

            // Split them into buy and sell orders
            var buyOrders = new List<Order>();
            var sellOrders = new List<Order>();
            foreach (var order in data)
            {
                if (order.IsBuyOrder)
                {
                    buyOrders.Add(order);
                    continue;
                }

                sellOrders.Add(order);
            }

            // Sort as desired
            SortOrders(sellOrders, sortByType, sortOrder);
            SortOrders(buyOrders, sortByType, sortOrder);

            var fields = new List<string> { "issued", "price", "type_id", "location_id", "region_id", "volume_remain", "volume_total" };

            // Print to output
            output.WriteLine("Sell Orders");
            output.WriteLine();
            mainInterface.printer.PrintJsonList<Order>(sellOrders, 20, 20, fields);
            output.WriteLine();
            output.WriteLine("Buy Orders");
            output.WriteLine();
            mainInterface.printer.PrintJsonList<Order>(sellOrders, 20, 20, fields);
        }

        /// <summary>
        /// Gathers orders from the API, sorts them and prints them out to output
        /// </summary>
        /// <param name="tokens">Contains tokens from the command line.</param>
        /// <param name="sortByType">Enum which determines the field by which to sort the orders</param>
        /// <param name="sortOrder">Enum for ascending/descending sort order</param>
        public void HandleMyOrders(string[] tokens, SortBy sortByType = SortBy.Date, SortOrder sortOrder = SortOrder.Descending)
        {
            if (!mainInterface.CheckAuthorization()) return;

            // Gather the orders
            var data = mainInterface.Client.Market.CharacterOrders().Result.Data;
            if (data.Count == 0) 
            { 
                output.WriteLine("No orders to print."); 
                return;
            }

            // Split them into buy and sell orders
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

            // Check for parameters
            if (tokens.Length > 1)
            {
                var tokenSet = new HashSet<string>(tokens);

                // Check for different sort order
                if (tokenSet.Contains("-a"))
                {
                    sortOrder = SortOrder.Ascending;
                }

                // Check for field determination
                if (tokenSet.Contains("-d"))
                {
                    sortByType = SortBy.Date;
                }

                if (tokenSet.Contains("-r"))
                {
                    sortByType = SortBy.Region;
                }

                else if (tokenSet.Contains("-s"))
                {
                    sortByType = SortBy.Station;
                }

                else if (tokenSet.Contains("-v"))
                {
                    sortByType = SortBy.Volume;
                }

                else if (tokenSet.Contains("-p"))
                {
                    sortByType = SortBy.Price;
                }
                else if (tokenSet.Contains("-s"))
                {
                    sortByType = SortBy.Station;
                }
            }
                // Sort as desired
            SortOrders(sellOrders, sortByType, sortOrder);
            SortOrders(buyOrders, sortByType, sortOrder);

            var fields = new List<string> { "issued", "price", "type_id", "location_id", "region_id", "volume_remain", "volume_total" };

            // Print to output
            output.WriteLine("Sell Orders");
            output.WriteLine();
            mainInterface.printer.PrintJsonList<Order>(sellOrders, 20, 20, fields);
            output.WriteLine();
            output.WriteLine("Buy Orders");
            output.WriteLine();
            mainInterface.printer.PrintJsonList<Order>(sellOrders, 20, 20, fields);
        }

        public void HandleOrderHistory(string[] tokens, SortBy sortByType = SortBy.Date, SortOrder sortOrder = SortOrder.Descending)
        {
            if (!mainInterface.CheckAuthorization()) return;

            // Gather the orders
            var orders = mainInterface.Client.Market.CharacterOrderHistory().Result.Data;
            if (orders.Count == 0)
            {
                output.WriteLine("No orders to print.");
                return;
            }

            // Check for parameters
            if (tokens.Length > 1)
            {
                var tokenSet = new HashSet<string>(tokens);

                // Check for different sort order
                if (tokenSet.Contains("-a"))
                {
                    sortOrder = SortOrder.Ascending;
                }

                // Check for field determination
                if (tokenSet.Contains("-d"))
                {
                    sortByType = SortBy.Date;
                }

                if (tokenSet.Contains("-r"))
                {
                    sortByType = SortBy.Region;
                }

                else if (tokenSet.Contains("-s"))
                {
                    sortByType = SortBy.Station;
                }

                else if (tokenSet.Contains("-v"))
                {
                    sortByType = SortBy.Volume;
                }

                else if (tokenSet.Contains("-p"))
                {
                    sortByType = SortBy.Price;
                }
                else if (tokenSet.Contains("-s"))
                {
                    sortByType = SortBy.Station;
                }
            }

            // Sort as desired
            SortOrders(orders, sortByType, sortOrder);

            var fields = new List<string> { "issued", "price", "type_id", "location_id", "region_id", "volume_remain", "volume_total" };

            // Print to output
            output.WriteLine("Order History");
            output.WriteLine();
            mainInterface.printer.PrintJsonList<Order>(orders, 20, 20, fields);
            output.WriteLine();
        }


        /// <summary>
        /// Gathers information about the authorized characters' wallet and prints it out to output
        /// </summary>
        /// <param name="tokens">Included for the uniformity of API, not used</param>
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
