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

        public JObject SearchName(string query, SearchCategory category)
        {

            var queryResult = mainInterface.Client.Search.Query(SearchType.Public, query, category).Result;
            var data = queryResult.Data.ToString();

            return JObject.Parse(data);
        }

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

                nameToIdCache[name] = typeId;
                idToNameCache[typeId] = name;

                return typeId;
            }

            var matches = SearchName(name, category);
            var categoryKey = "";
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
            var response = mainInterface.Client.Universe.IDs(new List<string> { name }).Result.Data;
            var resJson = JObject.Parse(response.ToString());
            long id = (long) resJson[categoryKey][0]["id"];

            nameToIdCache[name] = id;
            idToNameCache[id] = closestMatch;
           
            return id;
        }


        public string IdToName(long id)
        {
            if (idToNameCache.ContainsKey(id))
            {
                return idToNameCache[id];
            }

            var result = mainInterface.Client.Universe.Names(new List<long> { id }).Result.Data;
            string name = result[0].Name;

            idToNameCache[id] = name;
            nameToIdCache[name] = id;

            return name;
        }
    };
}
