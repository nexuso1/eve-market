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
        public enum IdType { System, Region, Station};
        public TextWriter output;
        public MainEsiInterface mainInterface;

        private Dictionary<string, long> nameToIdCache = new Dictionary<string, long>();
        private Dictionary<long, string> idToNameCache = new Dictionary<long, string>();

        public UniverseInterface(MainEsiInterface @interface, TextWriter textWriter)
        {
            output = textWriter;
            mainInterface = @interface;
        }

        public void HandleInfo(string[] tokens)
        {
            var itemName = mainInterface.StringFromSlice(tokens, 1, tokens.Length - 1);
            var itemId = NameToId(itemName, SearchCategory.InventoryType);
            var itemDesc = mainInterface.Client.Universe.Type((int)itemId).Result.Data;
            var itemCategory = mainInterface.Client.Market.Group(itemDesc.MarketGroupId).Result.Data;
            output.WriteLine(itemDesc.Name);
            output.Write($"Category: {itemCategory.Name}");
            output.WriteLine($"Volume: {itemDesc.Volume}");
            output.WriteLine($"Mass: {itemDesc.Mass}");
            output.WriteLine(itemDesc.Description);
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
            
            if (category == SearchCategory.InventoryType)
            {
                // Search only for exact name matches for inventory types
                var result = mainInterface.Client.Universe.IDs(new List<string> { name }).Result.Data;
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
        /// Finds the name corresponding to this ID, caches it and returns it
        /// </summary>
        /// <param name="id"></param>
        /// <returns>Name corresponding to the given ID</returns>
        public string IdToName(long id)
        {
            // Check the cache
            if (idToNameCache.ContainsKey(id))
            {
                return idToNameCache[id];
            }

            // Query the server
            var result = mainInterface.Client.Universe.Names(new List<long> { id }).Result.Data;
            string name = result[0].Name;

            // Save the result to cache
            idToNameCache[id] = name;
            nameToIdCache[name] = id;

            return name;
        }
    };
}
