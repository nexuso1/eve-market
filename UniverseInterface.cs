using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ESI.NET.Enumerations;
using Newtonsoft.Json.Linq;

namespace eve_market
{
    public class UniverseInterface
    {
        public TextWriter output;
        public MainEsiInterface mainInterface;

        private Dictionary<string, long> nameToIdCache = new Dictionary<string, long>();
        private Dictionary<long, string> idToNameCache = new Dictionary<long, string>();

        public UniverseInterface(MainEsiInterface @interface, TextWriter textWriter)
        {
            output = textWriter;
            mainInterface = @interface;
        }

        /// <summary>
        /// Finds the closest matching item and prints its description
        /// </summary>
        /// <param name="itemName">Command line tokens</param>
        public void PrintItemInfo(string itemName)
        {
            var itemId = NameToId(itemName, SearchCategory.InventoryType);

            if (itemId == -1)
            {
                output.WriteLine("No matching item found.");
                return;
            }

            var itemDesc = mainInterface.Client.Universe.Type((int)itemId).Result.Data;
            var itemCategory = mainInterface.Client.Market.Group(itemDesc.MarketGroupId).Result.Data;
            output.WriteLine(itemDesc.Name);
            mainInterface.printer.PrintLine(itemDesc.Name.Length);
            output.WriteLine($"Category description: {itemCategory.Description}");
            output.WriteLine($"Volume: {itemDesc.Volume} m^3");
            output.WriteLine($"Mass: {itemDesc.Mass} kg");
            output.WriteLine("Description:");
            output.WriteLine(itemDesc.Description);
        }

        public void HandleInfo(string[] tokens)
        {
            var itemName = mainInterface.StringFromSlice(tokens, 1, tokens.Length - 1);
            PrintItemInfo(itemName);
        }

        /// <summary>
        /// Find names of types/objects which containg a given string query and returns the API response
        /// as a JOBject
        /// </summary>
        /// <param name="query"></param>
        /// <param name="category">Enum describing the category of the object</param>
        /// <returns>A JObject containing the matches</returns>
        public JObject SearchName(string query, SearchCategory category)
        {
            
            var queryResult = mainInterface.Client.Search.Query(SearchType.Public, query, category).Result;
            var data = queryResult.Data.ToString();

            return JObject.Parse(data);
        }

        /// <summary>
        /// For a given name of object and it's type, finds its ID. 
        /// Doesn't require exact names, will find the closest match and returns its ID
        /// </summary>
        /// <param name="name">Name of the object</param>
        /// <param name="category">Enum describing the category of the object</param>
        /// <returns>ID of the given query type/object</returns>
        public long NameToId(string name, SearchCategory category)
        {
            if (nameToIdCache.ContainsKey(name))
            {
                return nameToIdCache[name];
            }
            
            if (name.Length == 0)
            {
                output.WriteLine("Please input an item name.");
            }

            if (category == SearchCategory.InventoryType)
            {
                // Search only for exact name matches for inventory types
                var result = mainInterface.Client.Universe.IDs(new List<string> { name }).Result.Data;

                if(result is null || result.InventoryTypes.Count == 0)
                {
                    return -1;
                }

                long typeId = result.InventoryTypes[0].Id;

                // Save the result to cache
                nameToIdCache[name] = typeId;
                idToNameCache[typeId] = name;

                return typeId;
            }
            // Otherwise find the closest full name
            var matches = SearchName(name, category);
            var categoryKey = "";

            // Find the correct key for this category
            switch (category)
            {
                case SearchCategory.Station:
                    categoryKey = "station";
                    break;
                case SearchCategory.Structure:
                    categoryKey = "structure";
                    break;
                case SearchCategory.Region:
                    categoryKey = "region";
                    break;
                case SearchCategory.SolarSystem:
                    categoryKey = "solar_system";
                    break;
                default:
                    throw new NotImplementedException("Category not implemented yet.");
            }

            var closestMatch = matches[categoryKey][0].ToString();

            // Query the server
            var response = mainInterface.Client.Universe.IDs(new List<string> { name }).Result.Data;
            // Turn it into a JSON object to make working with it easier
            var resJson = JObject.Parse(response.ToString());
            long id = (long) resJson[categoryKey][0]["id"];

            // Save the result to cache
            nameToIdCache[name] = id;
            idToNameCache[id] = closestMatch;
           
            return id;
        }

        /// <summary>
        /// Finds the names corresponding to these IDs, caches them and returns them as a list. 
        /// Make sure all IDs are unique and of the same type. It is better to later query 
        /// the IdToName(long id) method for results, instead of using this list.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>Name corresponding to the given ID</returns>
        public List<string> IdToName(List<long> ids)
        {
            List<long> toResolve = new List<long>();
            List<long> longToResolve = new List<long>();
            // Check the cache
            foreach (var id in ids)
            {
                if (!idToNameCache.ContainsKey(id))
                {
                    if (id > Int32.MaxValue)
                    {
                        longToResolve.Add(id);
                    }
                    else
                    {
                        toResolve.Add(id);
                    }
                }
            }

            if(toResolve.Count > 0)
            {
                // Query the server
                var uniResult = mainInterface.Client.Universe.Names(toResolve).Result.Data;
                // Save the results to cache
                foreach (var resolved in uniResult)
                {
                    idToNameCache[resolved.Id] = resolved.Name;
                    nameToIdCache[resolved.Name] = resolved.Id;
                }
            }

            if (longToResolve.Count > 0)
            {
                var uniResult = mainInterface.Client.Universe.Names(longToResolve).Result.Data;
                var itemResult = mainInterface.Client.Assets.NamesForCharacter(longToResolve).Result.Data;
                var locResult = mainInterface.Client.Assets.LocationsForCharacter(longToResolve).Result.Data;

                if (uniResult is not null)
                {
                    foreach (var resolved in uniResult)
                    {
                        idToNameCache[resolved.Id] = resolved.Name;
                        nameToIdCache[resolved.Name] = resolved.Id;
                    }
                }

                else if (itemResult is not null)
                {
                    // Save the results to cache
                    foreach (var resolved in itemResult)
                    {
                        idToNameCache[resolved.ItemId] = resolved.Name;
                        nameToIdCache[resolved.Name] = resolved.ItemId;
                    }
                }
                else if (locResult is not null)
                {
                    // Save results to cache, if its (0,0,0), its in a station/hangar
                    foreach (var location in locResult)
                    {
                        if (location.X == 0 && location.Y == 0 && location.Z == 0) idToNameCache[location.ItemId] = "In hangar or station";
                        else idToNameCache[location.ItemId] = $"({location.X}, {location.Y}, {location.Z})";
                    }
                }

                else
                {
                    // The IDs are invalid
                    foreach (var id in longToResolve)
                    {
                        idToNameCache[id] = "Invalid loc. ID";
                    }
                }
            }

            var res = new List<string>();
            foreach (var id in ids)
            {
                res.Add(idToNameCache[id]);
            }

            return res;
        }

        public string IdToName(long id)
        {
            // Check cache
            if (idToNameCache.ContainsKey(id)) return idToNameCache[id];
            return IdToName(new List<long> { id })[0];
        }
    }
}
