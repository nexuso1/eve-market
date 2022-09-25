using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using ESI.NET.Enumerations;
using ESI.NET.Models.Market;
using ESI.NET.Models.Assets;

namespace eve_market
{
    /// <summary>
    /// Class that handles the interaction with the Market part of ESI,
    /// as well ass the handling of assets.
    /// </summary>
    public class MarketInterface
    {
        /// <summary>
        /// Text output stream
        /// </summary>
        public TextWriter output;
        /// <summary>
        /// Reference to a MainEsiInterface instance
        /// </summary>
        public MainEsiInterface mainInterface;
        /// <summary>
        /// Id of the default region ("The Forge" currently)
        /// </summary>
        public long defaultRegionId = 10000002;
        /// <summary>
        /// Id of the default station ("Jita IV - Moon 4 - Caldari Navy Assembly Plant")
        /// </summary>
        public long defaultStationId = 60003760;
        /// <summary>
        /// Enum choosing the sort order of various items
        /// </summary>
        public enum SortOrder { Ascending, Descending };
        /// <summary>
        /// Enum for choosing the field by which orders should be sorted.
        /// </summary>
        public enum SortBy { Price, Date, Volume, Region, Station, CustomName, TypeName };
        /// <summary>
        /// Basic constructor. Only assigns the instances from arguments to fields.
        /// </summary>
        /// <param name="interface">Reference to an (already created) MainEsiInterface instance</param>
        /// <param name="textWriter">Text output stream</param>
        public MarketInterface(MainEsiInterface @interface, TextWriter textWriter)
        {
            output = textWriter;
            mainInterface = @interface;
        }

        /// <summary>
        /// Handles the "set" command.
        /// Sets default values for region or station used when displaying market listings.
        /// </summary>
        /// <param name="tokens">Tokens from the input, including the original command</param>
        public void HandleDefaults(string[] tokens)
        {
            if (tokens.Length < 3)
            {
                output.WriteLine("Invalid command format.");
                return;
            }

            var pattern = '"' + @"([\w ]+)" + '"';
            var matches = Regex.Matches(String.Join(' ', tokens), pattern);

            if (tokens.Length == 1)
            {
                output.WriteLine("No name provided");
                return;
            }

            if (matches.Count == 0)
            {
                output.WriteLine("Invalid name format.");
                return;
            }

            var name = matches[0].Groups[1].Value;

            switch (tokens[1])
            {
                case "region":
                    var regionId = mainInterface.universeInterface.NameToId(name, SearchCategory.Region);
                    if (regionId == -1)
                    {
                        output.WriteLine("Invalid region name provided.");
                        return;
                    }

                    defaultRegionId = regionId;
                    output.WriteLine($"Default region set to {mainInterface.universeInterface.IdToName(regionId)}.");
                    return;
                case "station":
                    var stationId = mainInterface.universeInterface.NameToId(name, SearchCategory.Station);
                    if (stationId == -1)
                    {
                        output.WriteLine("Invalid station name provided.");
                        return;
                    }

                    defaultStationId = stationId;
                    output.WriteLine($"Default station set to {mainInterface.universeInterface.IdToName(stationId)}.");
                    return;
                default:
                    output.WriteLine("Invalid name of a default setting");
                    return;
            }
        }
        
        /// <summary>
        /// Handles the "transactions" command.
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

            mainInterface.printer.PrintObjList(transactions, 20, transactions.Count, fields, fieldDesc);
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

            if (tokens.Length == 1)
            {
                output.WriteLine("No item name provided");
                return;
            }

            if (matches.Count == 0)
            {
                output.WriteLine("No item name provided");
                return;
            }

            if (matches.Count == 2)
            {
                specifiedRegion = true;
                if(matches.Count == 1)
                {
                    output.WriteLine("Invalid region name format. Please use double quotes around the region name.");
                    return;
                }
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
                return;
            }

            history.Sort((x, y) => x.Date.CompareTo(y.Date));
            history.Reverse();

            var fields = new List<string> { "date", "average", "highest", "lowest", "order_count", "volume" };
            var fieldDesc = new List<string> { "Date", "Average", "Highest", "Lowest", "Order Count", "Volume" };

            mainInterface.printer.PrintObjList(history, 20, 60, fields, fieldDesc);
        }

        /// <summary>
        /// Handles the "assets" command.
        /// Displays all assets of the authorized character as a table.
        /// </summary>
        /// <param name="tokens">Command line tokens</param>
        /// <param name="sortByType">Which field to sort by</param>
        /// <param name="sortOrder">Ascending/Descending</param>
        public void HandleAssets(string[] tokens, SortBy sortByType = SortBy.Station, SortOrder sortOrder = SortOrder.Descending)
        {
            if (!mainInterface.CheckAuthorization())
            {
                output.WriteLine("No character authorized.");
                return;
            }

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
            mainInterface.printer.PrintObjList(assets, 20, assets.Count, fields, fieldDesc);
        }

        /// <summary>
        /// Helper function that resolves the location and type id and determines their type.
        /// </summary>
        /// <param name="locationName"></param>
        /// <param name="regionId"></param>
        /// <param name="locationId"></param>
        /// <param name="stationSpecified"></param>
        /// <returns></returns>
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
        /// Handles the "orders" command.
        /// Finds all orders of the given type in the given location. If not location is given, the default region will be used
        /// </summary>
        /// <param name="tokens"></param>
        /// <param name="sortByType"></param>
        /// <param name="sortOrder"></param>
        public void HandleOrders(string[] tokens, SortBy sortByType = SortBy.Price, SortOrder sortOrder = SortOrder.Descending)
        {
            // Extract location and item name
            bool specifiedLocation = false;
            string locationName = "";
            string itemName = "";
            var pattern = '"' + @"([\w ]+)" + '"'; // Matches things inbetween ""
            var matches = Regex.Matches(String.Join(' ', tokens), pattern);

            if (tokens.Length == 1)
            {
                output.WriteLine("No item name provided");
                return;
            }
            // location name was provided
            if (matches.Count == 2)
            {
                specifiedLocation = true;
                locationName = matches[1].Groups[1].Value;
            }

            // Couldn't match anything
            if(matches.Count == 0)
            {
                output.WriteLine("Invalid name format or no name provided.");
                return;
            }

            itemName = matches[0].Groups[1].Value;

            // Resolve item id
            var typeId = mainInterface.universeInterface.NameToId(itemName, SearchCategory.InventoryType);
            if (typeId == -1)
            {
                output.WriteLine("Invalid item name.");
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


            // Has to be done since the RegionId is 0 in the response, and this is the easiest way to ensure proper printing.
            foreach (var order in allOrders)
            {
                order.RegionId = (int)regionId;
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
            var sortSpecified = false;
            // Check for different sort order
            if (tokenSet.Contains("-a"))
            {
                sortOrder = SortOrder.Ascending;
                sortSpecified = true;
            }

            if (tokenSet.Contains("-de"))
            {
                sortSpecified = true;
                sortOrder = SortOrder.Descending;
            }

            // Check for field determination
            if (tokenSet.Contains("-d"))
            {
                sortByType = SortBy.Date;
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
            if (!sortSpecified)
            {
                SortOrders(sellOrders, sortByType, SortOrder.Ascending);
                SortOrders(buyOrders, sortByType, SortOrder.Descending);
            }
            else
            {
                SortOrders(buyOrders, sortByType, sortOrder);
                SortOrders(sellOrders, sortByType, sortOrder);
            }
            
            

            // Filter orders from other stations
            if (stationSpecified)
            {
                sellOrders = sellOrders.FindAll((x) => x.LocationId == locationId);
                buyOrders = buyOrders.FindAll((x) => x.LocationId == locationId);
            }

            var fields = new List<string> { "issued", "price", "type_id", "location_id", "region_id", "volume_remain", "volume_total" };
            var fieldDesc = new List<string> { "Date Issued", "Price", "Item Name", "Station", "Region", "Vol. Remain", "Total Volume" };

            // Print to output
            output.WriteLine("Sell Orders");
            output.WriteLine();
            if(sellOrders.Count > 0)
            {
                mainInterface.printer.PrintObjList<Order>(sellOrders, 20, 20, fields, fieldDesc);
            }
            else
            {
                output.WriteLine("\tNo orders to show.");
            }
            
            output.WriteLine();
            output.WriteLine("Buy Orders");
            output.WriteLine();
            if(buyOrders.Count > 0)
            {
                mainInterface.printer.PrintObjList<Order>(buyOrders, 20, 20, fields, fieldDesc);
            }

            else
            {
                output.WriteLine("\tNo orders to show.");
            }
        }

        /// <summary>
        /// Handles the "my_orders" command.
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
                output.WriteLine("\tNo orders to show."); 
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


            var sortSpecified = false;
            // Check for parameters
            if (tokens.Length > 1)
            {
                var tokenSet = new HashSet<string>(tokens);

                // Check for different sort order
                if (tokenSet.Contains("-a"))
                {
                    sortOrder = SortOrder.Ascending;
                    sortSpecified = true;
                }

                if (tokenSet.Contains("-de"))
                {
                    sortSpecified = true;
                    sortOrder = SortOrder.Descending;
                }

                // Check for field determination
                if (tokenSet.Contains("-d"))
                {
                    sortByType = SortBy.Date;
                }

                else if (tokenSet.Contains("-v"))
                {
                    sortByType = SortBy.Volume;
                }

                else if (tokenSet.Contains("-p"))
                {
                    sortByType = SortBy.Price;
                }
            }

            // Sort as desired
            if (!sortSpecified)
            {
                SortOrders(sellOrders, sortByType, SortOrder.Ascending);
                SortOrders(buyOrders, sortByType, SortOrder.Descending);
            }
            else
            {
                SortOrders(buyOrders, sortByType, sortOrder);
                SortOrders(sellOrders, sortByType, sortOrder);
            }

            SortOrders(sellOrders, sortByType, sortOrder);
            SortOrders(buyOrders, sortByType, sortOrder);

            var fields = new List<string> { "issued", "price", "type_id", "location_id", "region_id", "volume_remain", "volume_total" };
            var fieldDesc = new List<string> { "Date Issued", "Price", "Item Name", "Station", "Region", "Vol. Remain", "Total Volume" };
            // Print to output
            output.WriteLine("Sell Orders");
            output.WriteLine();
            if(sellOrders.Count > 0)
            {
                mainInterface.printer.PrintObjList<Order>(sellOrders, 20, 20, fields, fieldDesc);
            }
            else
            {
                output.WriteLine("\tNo orders to show.");
            }
            
            output.WriteLine();
            output.WriteLine("Buy Orders");
            output.WriteLine();

            if(buyOrders.Count > 0)
            {
                mainInterface.printer.PrintObjList<Order>(buyOrders, 20, 20, fields, fieldDesc);
            }
            else
            {
                output.WriteLine("\tNo orders to show.");
            }
            
        }

        /// <summary>
        /// Handles the "my_order_history" command. Gathers the order history of a given 
        /// character, potentially sorts it and prints it out.
        /// </summary>
        /// <param name="tokens"></param>
        /// <param name="sortByType"></param>
        /// <param name="sortOrder"></param>
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

                if (tokenSet.Contains("-de"))
                {
                    sortOrder = SortOrder.Descending;
                }

                // Check for field determination
                if (tokenSet.Contains("-d"))
                {
                    sortByType = SortBy.Date;
                }

                else if (tokenSet.Contains("-v"))
                {
                    sortByType = SortBy.Volume;
                }

                else if (tokenSet.Contains("-p"))
                {
                    sortByType = SortBy.Price;
                }
            }

            // Sort as desired
            SortOrders(orders, sortByType, sortOrder);

            var fields = new List<string> { "issued", "price", "type_id", "location_id", "region_id", "volume_remain", "volume_total" };
            var fieldDesc = new List<string> { "Date Issued", "Price", "Item Name", "Station", "Region", "Vol. Remain", "Total Volume" };

            // Print to output
            output.WriteLine("Order History");
            output.WriteLine();
            mainInterface.printer.PrintObjList<Order>(orders, 20, 20, fields, fieldDesc);
        }


        /// <summary>
        /// Gathers information about the authorized characters' wallet and prints it out to output
        /// </summary>
        /// <param name="tokens">Included for the uniformity of API, not used</param>
        public void HandleWallet(string[] tokens)
        {
            if (!mainInterface.CheckAuthorization()) return;

            var response = mainInterface.Client.Wallet.CharacterWallet().Result;
            var balance = response.Message;
            output.WriteLine($"Your current personal account balance is {balance} ISK.");
            return;
        }
    }
}
