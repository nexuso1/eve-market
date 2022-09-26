using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using ESI.NET.Enumerations;

namespace eve_market
{
    /// <summary>
    /// Class that handles the interaction with the Universe part of ESI,
    /// and provides various helpful functions to other parts of the program.
    /// </summary>
    public class UniverseInterface
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
        /// Used for caching requests for name resolution.
        /// </summary>
        private Dictionary<string, long> nameToIdCache = new Dictionary<string, long>();
        /// <summary>
        /// Used for caching requests for ID resolution.
        /// </summary>
        private Dictionary<long, string> idToNameCache = new Dictionary<long, string>();

        /// <summary>
        /// Basic constructor. Only assigns the instances from arguments to fields.
        /// </summary>
        /// <param name="interface">Reference to an (already created) MainEsiInterface instance</param>
        /// <param name="textWriter">Text output stream</param>
        public UniverseInterface(MainEsiInterface @interface, TextWriter textWriter)
        {
            output = textWriter;
            mainInterface = @interface;
        }

        /// <summary>
        /// Finds the closest matching item and prints its description
        /// </summary>
        /// <param name="itemName">Command line tokens</param>
        public async Task PrintItemInfo(string itemName)
        {
            var itemId = await NameToId(itemName, SearchCategory.InventoryType);

            if (itemId == -1)
            {
                output.WriteLine("No matching item found.");
                return;
            }

            var descResponse = await mainInterface.Client.Universe.Type((int)itemId);
            var itemDesc = descResponse.Data;
            var categoryResponse = await mainInterface.Client.Market.Group(itemDesc.MarketGroupId);
            var itemCategory = categoryResponse.Data;
            output.WriteLine(itemDesc.Name);
            mainInterface.printer.PrintLine(itemDesc.Name.Length);
            output.WriteLine($"Category description: {itemCategory.Description}");
            output.WriteLine($"Volume: {itemDesc.Volume} m^3");
            output.WriteLine($"Mass: {itemDesc.Mass} kg");
            output.WriteLine("Description:");
            output.WriteLine(itemDesc.Description);
        }

        /// <summary>
        /// HAndles the "info" command. 
        /// </summary>
        /// <param name="tokens"></param>
        public async Task HandleInfo(string[] tokens)
        {
            // Extract item name

            string itemName = "";
            var pattern = '"' + @"([\w ]+)" + '"'; // Matches things inbetween ""
            var matches = Regex.Matches(String.Join(' ', tokens), pattern);

            if (tokens.Length == 1)
            {
                output.WriteLine("No item name provided");
                return;
            }

            // Couldn't match anything
            if (matches.Count == 0)
            {
                output.WriteLine("Invalid name format or no name provided.");
                return;
            }

            itemName = matches[0].Groups[1].Value;
            await PrintItemInfo(itemName);
        }

        /// <summary>
        /// Find names of types/objects which containg a given string query and returns the list of matching ids
        /// </summary>
        /// <param name="query"></param>
        /// <param name="category">Enum describing the category of the object</param>
        /// <returns>A list of ids of the matches</returns>
        public async Task<long[]> SearchName(string query, SearchCategory category)
        {
            if (mainInterface.IsAuthorized)
            {
                var searchRes = await mainInterface.Client.Search.Query(SearchType.Character, query, category);
                if (searchRes.Data is null)
                {
                    throw new ArgumentException("No result found");
                }
                var data = searchRes.Data;

                switch (category)
                {
                    case SearchCategory.Station:
                        return data.Stations;
                    case SearchCategory.Structure:
                        return data.Structures;
                    case SearchCategory.Region:
                        return data.Regions;
                    case SearchCategory.SolarSystem:
                        return data.SolarSystems;
                    default:
                        throw new NotImplementedException("Category not implemented yet.");
                }
            }

            var idRes = await mainInterface.Client.Universe.IDs(new List<string> { query });
            if (idRes.Data is null)
            {
                throw new ArgumentException("No result found");
            }
            var idData = idRes.Data;
            switch (category)
            {
                case SearchCategory.Station:
                    return new long[1] { idData.Stations[0].Id };
                case SearchCategory.Structure:
                    return new long[1] { idData.Structures[0].Id };
                case SearchCategory.Region:
                    return new long[1] { idData.Regions[0].Id };
                case SearchCategory.SolarSystem:
                    return new long[1] { idData.Systems[0].Id };
                default:
                    throw new NotImplementedException("Category not implemented yet.");
            }
        }

        /// <summary>
        /// Finds the region of a given object lower in the hierarchy
        /// </summary>
        /// <param name="id">ID of the object</param>
        /// <param name="type">Type of the object</param>
        /// <returns>ID of the region to which this object belongs</returns>
        public async Task<long> FindRegion(long id, SearchCategory type)
        {
            int systemId = 0;
            try
            {
                switch (type)
                {
                    case SearchCategory.Station:
                        var stationRes = await mainInterface.Client.Universe.Station((int)id);
                        var stationInfo = stationRes.Data;
                        systemId = stationInfo.SystemId;
                        break;

                    case SearchCategory.Structure:
                        if (!mainInterface.IsAuthorized)
                        {
                            throw new ArgumentException("No character authorized, can't look for structures");
                        }
                        var structRes = await mainInterface.Client.Universe.Structure(id);
                        var structInfo = structRes.Data;
                        systemId = structInfo.SolarSystemId;
                        break;

                    case SearchCategory.SolarSystem:
                        systemId = (int)id;
                        break;
                }
                // Find the constellation of the system
                var systemRes = await mainInterface.Client.Universe.System(systemId);
                var constellationId = systemRes.Data.ConstellationId;

                // Find the region of this constellation
                var constellationRes = await mainInterface.Client.Universe.Constellation(constellationId);
                var regionId = constellationRes.Data.RegionId;

                return regionId;
            }
            catch (NullReferenceException)
            {
                return -1;
            }
        }
        /// <summary>
        /// For a given name of object and it's type, finds its ID. 
        /// Doesn't require exact names, will find the closest match and returns its ID
        /// If no matches are found, returns -1
        /// </summary>
        /// <param name="name">Name of the object</param>
        /// <param name="category">Enum describing the category of the object</param>
        /// <returns>ID of the given query type/object</returns>
        public async Task<long> NameToId(string name, SearchCategory category)
        {
            if (nameToIdCache.ContainsKey(name))
            {
                return nameToIdCache[name];
            }

            if (name.Length == 0)
            {
                output.WriteLine("Please input an item name.");
            }

            // Search only for exact name matches for inventory types
            if (category == SearchCategory.InventoryType)
            {
                var response = await mainInterface.Client.Universe.IDs(new List<string> { name });
                var uniResult = response.Data;

                if (uniResult is null || uniResult.InventoryTypes.Count == 0)
                {
                    return -1;
                }

                long typeId = uniResult.InventoryTypes[0].Id;

                // Save the result to cache
                nameToIdCache[name] = typeId;
                nameToIdCache[uniResult.InventoryTypes[0].Name] = typeId;
                idToNameCache[typeId] = uniResult.InventoryTypes[0].Name;

                return typeId;
            }

            // Find the closest match containing "name" as a substring in the given category

            try
            {
                var matches = await SearchName(name, category);
                if (matches is null)
                {
                    return -1;
                }
                var closestMatch = matches[0];

                // Find the correct name for future reference
                var nameRes = await mainInterface.Client.Universe.Names(new List<long> { closestMatch });
                var matchName = nameRes.Data[0].Name;

                // Save the result to cache
                nameToIdCache[name] = closestMatch;
                idToNameCache[closestMatch] = matchName;

                return closestMatch;
            }
            catch (ArgumentException)
            {
                return -1;
            }
            
        }

        /// <summary>
        /// Function that resolves the ids in a given contract, categories
        /// are determined based on the name of the field to which these
        /// ids belong. Results are cached and are available later from
        /// the IdToName() function.
        /// </summary>
        /// <param name="ids">List of ids to resolve</param>
        /// <param name="fieldName">Name of the field to which these ids belong</param>
        public async Task ContractIdToName(List<long> ids, string fieldName)
        {
            switch (fieldName)
            {
                // Input ids are character ids
                case "acceptor_id":
                case "assignee_id":
                case "issuer_id":
                    await ResolveContractIds(ids, SearchCategory.Character);
                    break;

                // Input ids are structure/station ids
                case "start_location_id":
                case "end_location_id":
                    await ResolveContractIds(ids, SearchCategory.Structure);
                    break;

                // Input ids are inventory type ids
                case "type_id":
                    await ResolveContractIds(ids, SearchCategory.InventoryType);
                    break;
                
                // Input ids are corporation ids
                case "issuer_corporation_id":
                    await ResolveContractIds(ids, SearchCategory.Corporation);
                    break;
                default:
                    throw new ArgumentException("Field not implemented or invalid.");
            }
        }

        /// <summary>
        /// Function which does the actual resolving of the contract ids. 
        /// Results are cached and later available through IdToName() function.
        /// </summary>
        /// <param name="ids">List of ids to resolve</param>
        /// <param name="category">Enum denoting the category of the ids</param>
        private async Task ResolveContractIds(List<long> ids, SearchCategory category)
        {
            List<long> toResolve = new List<long>();
            List<long> longToResolve = new List<long>();

            // Check cache
            foreach (var id in ids)
            {
                // Not in cache or couldn't resolve previously
                if (!idToNameCache.ContainsKey(id) || idToNameCache[id] == "Unknown")
                {
                    if(id == 0)
                    {
                        idToNameCache[id] = "None";
                        continue;
                    }

                    if (id > Int32.MaxValue) longToResolve.Add(id);
                    else toResolve.Add(id);

                }
                
            }

            // Nothing to resolve
            if (toResolve.Count == 0 && longToResolve.Count == 0) return;

            // Different id categories require different ways of resolving them
            switch (category)
            {
                case SearchCategory.Corporation:
                    if(toResolve.Count > 0)
                    {
                        foreach (var id in toResolve)
                        {
                            var result = await mainInterface.Client.Corporation.Information((int)id);
                            idToNameCache[id] = result.Data.Name;
                        }
                    }
                    
                    break;

                case SearchCategory.Structure:
                case SearchCategory.Station:
                    // Resolve stations
                    if(toResolve.Count > 0)
                    {
                        var uniResponse = await mainInterface.Client.Universe.Names(toResolve);
                        var uniData = uniResponse.Data;
                        // Cache the station results
                        foreach (var result in uniData)
                        {
                            idToNameCache[result.Id] = result.Name;
                            nameToIdCache[result.Name] = result.Id;
                        }
                    }
                    
                    if(longToResolve.Count > 0)
                    {
                        if (mainInterface.IsAuthorized)
                        {
                            // Resolve structures (each has to be done one by one)
                            foreach (var id in longToResolve)
                            {
                                var structResponse = await mainInterface.Client.Universe.Structure(id);
                                var structResult = structResponse.Data;

                                // No access to the structure
                                if (structResult is null)
                                {
                                    idToNameCache[id] = "Unknown";
                                    continue;
                                }

                                // Cache the result
                                idToNameCache[id] = structResult.Name;
                                nameToIdCache[structResult.Name] = id;
                            }
                        }
                        else
                        {
                            foreach (var id in longToResolve)
                            {
                                idToNameCache[id] = "Unknown";
                            }
                        }
                    }

                    break;

                case SearchCategory.Character:
                    if(toResolve.Count > 0)
                    {
                        var charResponse = await mainInterface.Client.Universe.Names(toResolve);
                        var charResult = charResponse.Data;
                        // Cache the station results
                        foreach (var result in charResult)
                        {
                            idToNameCache[result.Id] = result.Name;
                            nameToIdCache[result.Name] = result.Id;
                        }
                    }
                    break;

                case SearchCategory.InventoryType:
                    if(toResolve.Count > 0)
                    {
                        var typeResponse = await mainInterface.Client.Universe.Names(toResolve);
                        var typeResult = typeResponse.Data;
                        // Cache the station results
                        foreach (var result in typeResult)
                        {
                            idToNameCache[result.Id] = result.Name;
                            nameToIdCache[result.Name] = result.Id;
                        }
                    }
                    break;

                default:
                    throw new ArgumentException("Category not implemented.");
            }
        }

        /// <summary>
        /// Finds the names corresponding to these IDs, caches them and returns them as a list.
        /// Make sure all IDs are unique and of the same type.It is better to later query 
        /// the IdToName(long id) method for the results, instead of using this list.
        /// </summary>
        /// <param name = "ids">List of ids to resolve</param >
        /// <returns> Name corresponding to the given ID</returns>
        public async Task<List<string>> IdToName(List<long> ids)
        {
            List<long> toResolve = new List<long>();
            List<long> longToResolve = new List<long>();
            // Check the cache
            foreach (var id in ids)
            {
                // Not in cache or couldn't resolve previously
                if (!idToNameCache.ContainsKey(id) || idToNameCache[id] == "Unknown")
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
                var uniResponse = await mainInterface.Client.Universe.Names(toResolve);
                // Save the results to cache
                foreach (var resolved in uniResponse.Data)
                {
                    idToNameCache[resolved.Id] = resolved.Name;
                    nameToIdCache[resolved.Name] = resolved.Id;
                }
            }

            if (longToResolve.Count > 0)
            {
                var uniResult = await mainInterface.Client.Universe.Names(longToResolve);

                if (uniResult.Data is not null)
                {
                    foreach (var resolved in uniResult.Data)
                    {
                        idToNameCache[resolved.Id] = resolved.Name;
                        nameToIdCache[resolved.Name] = resolved.Id;
                    }
                }

                // Check endpoints requiring authorization
                else if (mainInterface.IsAuthorized)
                {
                    // Check asset custom names
                    var itemResult = await mainInterface.Client.Assets.NamesForCharacter(longToResolve);
                    // Check asset locations
                    var locResult = await mainInterface.Client.Assets.LocationsForCharacter(longToResolve);

                    if (itemResult.Data is not null)
                    {
                        // Save the results to cache
                        foreach (var resolved in itemResult.Data)
                        {
                            idToNameCache[resolved.ItemId] = resolved.Name;
                            nameToIdCache[resolved.Name] = resolved.ItemId;
                        }
                    }
                    else if (locResult.Data is not null)
                    {
                        // Save results to cache, if its (0,0,0), its in a station/hangar
                        foreach (var location in locResult.Data)
                        {
                            if (location.X == 0 && location.Y == 0 && location.Z == 0) idToNameCache[location.ItemId] = "In hangar or station";
                            else idToNameCache[location.ItemId] = $"({location.X}, {location.Y}, {location.Z})";
                        }
                    }

                    else
                    {
                        // Check structures
                        foreach (var id in longToResolve)
                        {
                            var structResult = await mainInterface.Client.Universe.Structure(id);
                            if (structResult.Data is not null)
                            {
                                idToNameCache[id] = structResult.Data.Name;
                            }

                            else
                            {
                                idToNameCache[id] = "Unknown";
                            }
                        }
                    }
                }

                else
                {
                    foreach (var id in longToResolve)
                    {
                        idToNameCache[id] = "Unknown";
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

        /// <summary>
        /// Overload for singular ids. If id is in the 
        /// cache, returns it's cached value. Otherwise calls
        /// IdToName() with a List of length 1 containing this id.
        /// </summary>
        /// <param name="id">Id to resolve</param>
        /// <returns>Resolved name</returns>
        public async Task<string> IdToName(long id)
        {
            // Check cache
            if (idToNameCache.ContainsKey(id)) return idToNameCache[id];

            var nameList = await IdToName(new List<long> { id });
            return nameList[0];
        }
    }
}
