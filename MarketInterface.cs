using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using ESI.NET.Enumerations;
using ESI.NET.Models.Market;
using ESI.NET.Models.Assets;

namespace eve_market
{
    public class MarketInterface
    {

        public TextWriter output;
        public MainEsiInterface mainInterface;
        public long defaultRegionId = 10000002;
        public long defaultStationId = 60003760;
        public enum SortOrder { Ascending, Descending };
        public enum SortBy { Price, Date, Volume, Region, Station, CustomName, TypeName };

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
                    defaultRegionId = mainInterface.universeInterface.NameToId(name, SearchCategory.Region);
                    return;
                case "station":
                    defaultStationId = mainInterface.universeInterface.NameToId(name, SearchCategory.Station);
                    return;
                default:
                    output.WriteLine("Invalid name of a default setting");
                    return;
            }
        }
        /// <summary>
        /// Extracts type and potentially location from the tokens. If both are found, returns
        /// them as a List { typeName, locationName }
        /// </summary>
        /// <param name="tokens">command line tokens</param>
        /// <param name="specifiedLocation">Bool if location was found</param>
        /// <returns></returns>
        private List<long> GetTypeAndLocationId(string[] tokens, ref bool specifiedLocation)
        {
            var pattern = '"' + @"([\w ]+)" + '"';
            var matches = Regex.Matches(String.Join(' ', tokens), pattern);
            bool specifiedRegion = false;
            var regionName = "";
            var typeName = "";
            var regionId = defaultRegionId;

            if (matches.Count == 2)
            {
                specifiedRegion = true;
                regionName = matches[1].Groups[1].Value;
            }

            typeName = matches[0].Groups[1].Value;

            var typeId = mainInterface.universeInterface.NameToId(typeName, SearchCategory.InventoryType);
            if (typeId == -1)
            {
                throw new ArgumentException("Couldnt find the type specified.");
            }

            if (specifiedRegion)
            {
                regionId = mainInterface.universeInterface.NameToId(regionName, SearchCategory.Region);
                if (regionId == -1)
                {
                    throw new ArgumentException("Couldnt find the region specified.");
                }
            }

            return new List<long> { typeId, regionId };
        }
        
        /// <summary>
        /// Displays all of the recent transactions of the authorized characters
        /// </summary>
        /// <param name="tokens">Command line tokens (included for uniformity of API, not used)</param>
        public void HandleTransactions(string[] tokens)
        {
            if (!mainInterface.CheckAuthorization()) return;
            int page = 1;
            var transactions = new List<ESI.NET.Models.Wallet.Transaction>();

            // Gather transactions
            while (true)
            {
                try
                {
                    var temp = mainInterface.Client.Wallet.CharacterTransactions(page).Result.Data;
                    if (temp.Count == 0) break;
                    transactions.AddRange(temp);

                    // There is a bug in ESI it seems, every page returns the same transactions...
                    // so just break out of the loop after the first one
                    break;
                    page++;
                }
                catch (ArgumentException)
                {
                    break;
                }
            }

            if (transactions.Count == 0)
            {
                output.WriteLine("No recent transactions to display.");
                return;
            }

            transactions.Sort((x, y) => x.Date.CompareTo(y.Date));
            var fields = new List<string> { "date", "is_buy", "unit_price", "type_id", "quantity", "client_id", "location_id" };
            var fieldDesc = new List<string> { "Data", "Is Buy", "Unit Price", "Item", "Quantity", "Beneficiary", "Location" };

            mainInterface.printer.PrintTableHeader(fieldDesc, 20);
            mainInterface.printer.PrintLine(fields, 20);
            mainInterface.printer.PrintJsonList(transactions, 20, transactions.Count, fields);
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
        /// <summary>
        /// Sorts a list of items according to the given field in the given order
        /// </summary>
        /// <param name="items">List of items to sort</param>
        /// <param name="sortByType">Field to sort by</param>
        /// <param name="sortOrder">Order to sort by</param>
        public void SortItems(List<Item> items, SortBy sortByType, SortOrder sortOrder)
        {
            switch (sortByType)
            {
                case SortBy.Station:
                case SortBy.Region:
                    items.Sort((x, y) => x.LocationId.CompareTo(y.LocationId));
                    break;
                case SortBy.CustomName:
                    items.Sort((x, y) => x.ItemId.CompareTo(y.ItemId));
                    break;
                case SortBy.TypeName:
                    items.Sort((x, y) => x.TypeId.CompareTo(y.TypeId));
                    break;
            }

            if (sortOrder == SortOrder.Descending)
            {
                items.Reverse();
            }
        }

        /// <summary>
        /// Prints type history in a given region.
        /// </summary>
        /// <param name="tokens">Command line tokens</param>
        public void HandleHistory(string[] tokens)
        {
            var pattern = '"' + @"([\w ]+)" + '"';
            var matches = Regex.Matches(String.Join(' ', tokens), pattern);
            bool specifiedRegion = false;
            var regionName = "";
            var typeName = "";
            var regionId = defaultRegionId;

            if (matches.Count == 2)
            {
                specifiedRegion = true;
                regionName = matches[1].Groups[1].Value;
            }

            typeName = matches[0].Groups[1].Value;

            var typeId = mainInterface.universeInterface.NameToId(typeName, SearchCategory.InventoryType);
            if (typeId == -1)
            {
                output.WriteLine("Couldn't find the item specified.");
                return;
            }

            if (specifiedRegion)
            {
                regionId = mainInterface.universeInterface.NameToId(regionName, SearchCategory.Region);
                if (regionId == -1)
                {
                    output.WriteLine("Invalid region name given.");
                    return;
                }
            }

            var history = mainInterface.Client.Market.TypeHistoryInRegion((int)regionId, (int)typeId).Result.Data;
            if (history.Count == 0)
            {
                output.WriteLine("No history to display.");
            }

            history.Sort((x, y) => x.Date.CompareTo(y.Date));
            history.Reverse();

            var fields = new List<string> { "date", "average", "highest", "lowest", "order_count", "volume" };
            var fieldDesc = new List<string> { "Date", "Average", "Highest", "Lowest", "Order Count", "Volume" };

            mainInterface.printer.PrintTableHeader(fieldDesc, 20);
            mainInterface.printer.PrintJsonList(history, 20, 60, fields);

        }

        /// <summary>
        /// Displays all assets of the authorized character as a table.
        /// </summary>
        /// <param name="tokens">Command line tokens</param>
        /// <param name="sortByType">Which field to sort by</param>
        /// <param name="sortOrder">Ascending/Descending</param>
        public void HandleAssets(string[] tokens, SortBy sortByType = SortBy.Station, SortOrder sortOrder = SortOrder.Descending)
        {
            if (!mainInterface.CheckAuthorization()) return;

            int page = 1;
            var assets = new List<Item>();

            // Gather all pages of assets
            while (true)
            {
                try
                {
                    var data = mainInterface.Client.Assets.ForCharacter(page).Result.Data;
                    assets.AddRange(data);
                    page++;
                }
                catch (ArgumentException)
                {
                    break;
                }
            }

            if (tokens.Length > 1)
            {
                var tokenSet = new HashSet<string>(tokens);

                // Check for different sort order
                if (tokenSet.Contains("-a"))
                {
                    sortOrder = SortOrder.Ascending;
                }

                // Check for field determination

                if (tokenSet.Contains("-r"))
                {
                    sortByType = SortBy.Region;
                }

                else if (tokenSet.Contains("-s"))
                {
                    sortByType = SortBy.Station;
                }

                else if (tokenSet.Contains("-n"))
                {
                    sortByType = SortBy.CustomName;
                }
            }

            var fields = new List<string> { "type_id", "item_id", "location_id", "quantity" };
            var fieldDesc = new List<string> { "Type", "Custom Name", "Location", "Quantity" };
            // Sort the assets
            SortItems(assets, sortByType, sortOrder);
            mainInterface.printer.PrintTableHeader(fieldDesc, 20);
            mainInterface.printer.PrintLine(fields, 20);
            mainInterface.printer.PrintJsonList(assets, 20, assets.Count, fields);
        }

        private bool GetLocIdAndType(string locationName, ref long regionId, ref long locationId, ref bool stationSpecified)
        {
            var res = mainInterface.universeInterface.NameToId(locationName, SearchCategory.Region);
            if (res == -1)
            {
                res = mainInterface.universeInterface.NameToId(locationName, SearchCategory.Station);
                if (res == -1)
                {
                    res = mainInterface.universeInterface.NameToId(locationName, SearchCategory.Structure);
                    if (res == -1)
                    {
                        return false;
                    }

                    locationId = res;
                    regionId = mainInterface.universeInterface.FindRegion(locationId, SearchCategory.Structure);
                    stationSpecified = true;
                    return true;
                }
                locationId = res;
                regionId = mainInterface.universeInterface.FindRegion(locationId, SearchCategory.Station);
                stationSpecified = true;
                return true;
            }
            // Location was a region
            regionId = (int)res;
            return true;
        }
        

        /// <summary>
        /// Finds all orders of the given type in the given location. If not location is given, the default region will be used
        /// </summary>
        /// <param name="tokens"></param>
        /// <param name="sortByType"></param>
        /// <param name="sortOrder"></param>
        public void HandleOrders(string[] tokens, SortBy sortByType = SortBy.Price, SortOrder sortOrder = SortOrder.Descending)
        {
            bool specifiedLocation = false;
            string locationName = "";
            string itemName = "";
            var pattern = '"' + @"([\w ]+)" + '"';
            var matches = Regex.Matches(String.Join(' ', tokens), pattern);
            if (matches.Count == 2)
            {
                specifiedLocation = true;
                locationName = matches[1].Groups[1].Value;
            }

            itemName = matches[0].Groups[1].Value;
            if (tokens.Length == 1)
            {
                output.WriteLine("No item name provided");
            }

            var typeId = mainInterface.universeInterface.NameToId(itemName, SearchCategory.InventoryType);
            if (typeId == -1)
            {
                output.Write("Invalid item name.");
                return;
            }
            long regionId = defaultRegionId;
            long locationId = defaultStationId;
            bool stationSpecified = false;

            // Find the id of the location and its type
            if (specifiedLocation)
            {
                // Sets the corresponding variables aswell
                if (!GetLocIdAndType(locationName, ref regionId, ref locationId, ref stationSpecified))
                {
                    output.WriteLine("Couldn't find the location specified. Please check if you have written the location name correctly");
                    return;
                }
            }

            // Gather the orders
            var allOrders = new List<Order>();
            int page = 1;

            while (true)
            {
                try
                {
                    var tempOrders = new List<Order>();
                    tempOrders = mainInterface.Client.Market.RegionOrders((int)regionId, page: page, type_id: (int)typeId).Result.Data;
                    if (tempOrders is null || tempOrders.Count == 0)
                    {
                        break;
                    }
                    allOrders.AddRange(tempOrders);
                    page++;
                }
                catch (ArgumentException)
                {
                    break;
                }
            }

            // Split them to buy and sell orders
            var buyOrders = new List<Order>();
            var sellOrders = new List<Order>();
            foreach (var order in allOrders)
            {
                if (order.IsBuyOrder)
                {
                    buyOrders.Add(order);
                }
                else
                {
                    sellOrders.Add(order);
                }
            }

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

            // Sort as desired
            SortOrders(sellOrders, sortByType, sortOrder);
            SortOrders(buyOrders, sortByType, sortOrder);

            if (stationSpecified)
            {
                sellOrders = sellOrders.FindAll((x) => x.LocationId == locationId);
                buyOrders = buyOrders.FindAll((x) => x.LocationId == locationId);
            }

            var fields = new List<string> { "issued", "price", "type_id", "location_id", "region_id", "volume_remain", "volume_total" };
            var fieldDescriptions = new List<string> { "Date Issued", "Price", "Item Name", "Station", "Region", "Vol. Remain", "Total Volume" };

            // Print to output
            output.WriteLine("Sell Orders");
            output.WriteLine();
            if(sellOrders.Count > 0)
            {
                mainInterface.printer.PrintTableHeader(fieldDescriptions, 20);
                mainInterface.printer.PrintJsonList<Order>(sellOrders, 20, 20, fields);
            }
            else
            {
                output.WriteLine("\tNo orders to print");
            }
            
            output.WriteLine();
            output.WriteLine("Buy Orders");
            output.WriteLine();
            if(buyOrders.Count > 0)
            {
                mainInterface.printer.PrintTableHeader(fieldDescriptions, 20);
                mainInterface.printer.PrintJsonList<Order>(sellOrders, 20, 20, fields);
            }

            else
            {
                output.WriteLine("\tNo orders to print");
            }
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
            var response = mainInterface.Client.Market.CharacterOrderHistory().Result;
            var orders = response.Data;
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
