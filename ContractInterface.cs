using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ESI.NET.Enumerations;

namespace eve_market
{
    /// <summary>
    /// Main interaction point for the Contract part of ESI.
    /// </summary>
    public class ContractInterface
    {
        /// <summary>
        /// Text output stream
        /// </summary>
        public TextWriter output;
        /// <summary>
        /// Reference to a MainInterface instance
        /// </summary>
        public MainEsiInterface mainInterface;

        /// <summary>
        /// Basic constructor. Only assigns the instances from arguments to fields.
        /// </summary>
        /// <param name="interface">Reference to an (already created) MainEsiInterface instance</param>
        /// <param name="textWriter">Text output stream</param>
        public ContractInterface(MainEsiInterface @interface, TextWriter textWriter)
        {
            output = textWriter;
            mainInterface = @interface;
        }

        /// <summary>
        /// Handles the "contracts" command. Displays
        /// a given page of contracts in the input 
        /// region. If no region is given, the default 
        /// region is used.
        /// </summary>
        /// <param name="tokens">Command line tokens</param>
        public void HandleContracts(string[] tokens)
        {
            long regionId = mainInterface.marketInterface.defaultRegionId;
            if (tokens.Length == 1)
            {
                output.WriteLine("Page not specified.");
                return;
            }
            if (tokens.Length > 2)
            {
                var pattern = '"' + @"([\w ]+)" + '"';
                var matches = Regex.Matches(String.Join(' ', tokens), pattern);
                var regionName = matches[0].Groups[1].Value;
                regionId = mainInterface.universeInterface.NameToId(regionName, SearchCategory.Region);
                if (regionId == -1)
                {
                    output.WriteLine("Invalid region name specified.");
                    return;
                }
            }


            int page = 1;
            if (!Int32.TryParse(tokens[1], out page))
            {
                output.WriteLine("Invalid page number given.");
                return;
            }

            var response = mainInterface.Client.Contracts.Contracts((int)regionId, page).Result;
            var contracts = response.Data;

            if (contracts.Count == 0)
            {
                output.WriteLine("No contracts to display");
                return;
            }

            mainInterface.printer.PrintContracts(contracts, true);
        }

        /// <summary>
        /// Handles the "my_contracts" command. Displays all contracts of the authorized character.
        /// </summary>
        /// <param name="tokens">Command line tokens (included for uniformity of API, not used)</param>
        public void HandleMyContracts(string[] tokens)
        {
            if (!mainInterface.CheckAuthorization()) return;

            int page = 1;
            var contracts = new List<ESI.NET.Models.Contracts.Contract>();
            while (true)
            {
                try
                {
                    var response = mainInterface.Client.Contracts.CharacterContracts(page).Result;
                    var data = response.Data;
                    if (data is null || data.Count == 0)
                    {
                        break;
                    }

                    contracts.AddRange(data);
                    page++;
                }

                catch (ArgumentException)
                {
                    break;
                }
            }

            if (contracts.Count == 0)
            {
                output.WriteLine("No contracts to display.");
                return;
            }
            mainInterface.printer.PrintContracts(contracts);
        }

        /// <summary>
        /// Helper function. For a given contract, constructs a string of
        /// the items in the given contract, with a specific formatting.
        /// If listPublic is false, it is assumed that the contract is part
        /// of the authorized characters contracts, otherwise it's public.
        /// </summary>
        /// <param name="contractId">ID of the contract</param>
        /// <param name="isPublic">Whether the contract is a public or character one</param>
        /// <returns>List of item info strings</returns>
        public List<List<string>> getContractItemString(long contractId, bool isPublic = false)
        {
            var contractItems = new List<ESI.NET.Models.Contracts.ContractItem>();
            if (isPublic) contractItems = mainInterface.Client.Contracts.ContractItems((int)contractId).Result.Data;
            else contractItems = mainInterface.Client.Contracts.CharacterContractItems((int)contractId).Result.Data;

            // No items in this contract
            if (contractItems is null || contractItems.Count == 0) return new List<List<string>> { new List<string>(), new List<string>() };
            var included = new List<string>();
            var asking = new List<string>();

            var idsToResolve = new HashSet<long>();

            // Gather all ids
            foreach (var item in contractItems)
            {
                idsToResolve.Add(item.TypeId);
            }

            var idBuffer = new long[idsToResolve.Count];
            idsToResolve.CopyTo(idBuffer);

            // Resolve them all at once
            mainInterface.universeInterface.ContractIdToName(new List<long>(idBuffer), "type_id");

            // Create item strings
            var buffer = new StringBuilder();
            foreach (var contractItem in contractItems)
            {
                buffer.Append(mainInterface.universeInterface.IdToName(contractItem.TypeId));
                buffer.Append(' ');
                if (contractItem.IsBlueprintCopy)
                {
                    buffer.Append($"BPC, [ME: {contractItem.MaterialEfficiency}, TE: {contractItem.TimeEfficiency}, Runs: {contractItem.Runs}] ");
                }
                buffer.Append($"x {contractItem.Quantity}");

                if (contractItem.IsIncluded)
                {
                    included.Add(buffer.ToString());
                }

                else
                {
                    asking.Add(buffer.ToString());
                }

                buffer = buffer.Clear();
            }

            return new List<List<string>> { included, asking };

        }

        /// <summary>
        /// Helper function. Constructs a string containing 
        /// all the bids from of the given auction contract.
        /// Returns an empty lists if no bids are found.
        /// </summary>
        /// <param name="contractId">Auction contract ID</param>
        /// <returns>List of bid strings</returns>
        public List<string> GetBidStrings(int contractId)
        {
            var bids = mainInterface.Client.Contracts.ContractBids(contractId).Result.Data;
            if (bids is null) return new List<string>();
            var res = new List<string>();

            foreach (var bid in bids)
            {
                res.Add($"{bid.Amount} ISK from {bid.BidderId}, date: {bid.DateBid}");
            }

            return res;
        }
    }
}
