using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using ESI.NET.Enumerations;
namespace eve_market
{
    public class Printer
    {
        public TextWriter output;
        public MainEsiInterface mainEsiInterface;
        public Printer( MainEsiInterface esiInterface,TextWriter writer)
        {
            output = writer;
            mainEsiInterface = esiInterface;
        }

        public void PrintLine(int length)
        {
            for (int i = 0; i < length; i++)
            {
                output.Write('-');
            }
            output.WriteLine();
        }

        public void PrintLine(List<string> fields, int width)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < (width + 1) * fields.Count; i++)
            {
                sb.Append('-');
            }

            output.WriteLine(sb.ToString());
        }

        public void PrintTableHeader(List<string> fields, int width)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                int length = fields[i].Length;
                output.Write(fields[i]);
                for (int j = 0; j < width - fields[i].Length; j++)
                {
                    output.Write(' ');
                }

                output.Write('|');
            }

            output.WriteLine();
        }

        private List<List<string>> getContractItemString(long contractId)
        {
            var contractItems = new List<ESI.NET.Models.Contracts.ContractItem>();
            var included = new List<string>();
            var asking = new List<string>();
            int page = 1;
            while (true)
            {
                try
                {
                    var response = mainEsiInterface.Client.Contracts.ContractItems((int)contractId, page).Result.Data;
                    if (response.Count == 0) break;
                    contractItems.AddRange(response);
                    page++;
                }
                catch (ArgumentException)
                {
                    break;
                }
            }

            var idsToResolve = new HashSet<long>();
            
            foreach(var item in contractItems)
            {
                idsToResolve.Add(item.TypeId);
            }

            var idBuffer = new long[idsToResolve.Count];
            idsToResolve.CopyTo(idBuffer);
            mainEsiInterface.universeInterface.IdToName(new List<long>(idBuffer));

            var buffer = new StringBuilder();
            foreach (var contractItem in contractItems)
            {
                buffer.Append(mainEsiInterface.universeInterface.IdToName(contractItem.TypeId));
                buffer.Append(' ');
                if(contractItem.IsBlueprintCopy)
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

        private List<string> GetBidStrings(int contractId)
        {
            var bids = mainEsiInterface.Client.Contracts.ContractBids(contractId).Result.Data;
            var res = new List<string>();

            foreach (var bid in bids)
            {
                res.Add($"{bid.Amount} ISK from {bid.BidderId}, date: {bid.DateBid}");
            }

            return res;
        }

        /// <summary>
        /// Prints the contracts from arguments
        /// </summary>
        /// <param name="contracts">List of contracts to print</param>
        public void PrintContracts(List<ESI.NET.Models.Contracts.Contract> contracts)
        {
            // Prepare dict of id fields to resolve
            var idFields = new List<string> { "acceptor_id", "assignee_id", "issuer_corporation_id", "issuer_id", "end_location_id", "start_location_id" };
            var ids = new Dictionary<string, HashSet<long>>();
            foreach (var field in idFields)
            {
                ids.Add(field, new HashSet<long>());
            }

            // Gather ids to resolve
            foreach (var item in contracts)
            {
                var json = JObject.FromObject(item);
                foreach (var field in ids.Keys)
                {
                    if (json.ContainsKey(field))
                    {
                        var val = json[field].ToObject<long>();
                        if (!ids[field].Contains(val)) ids[field].Add(val);
                    }
                }
            }

            // Resolve the ids
            foreach (var pair in ids)
            {
                if (pair.Value.Count == 0) continue;
                // First convert them to a list
                long[] temp = new long[pair.Value.Count];
                pair.Value.CopyTo(temp);

                // This will cache the results, and they can later be accessed by IdToName(long id)
                mainEsiInterface.universeInterface.IdToName(new List<long>(temp));
            }
            var buffer = new StringBuilder();
            var contractTypeNames = new Dictionary<ContractType, string>
            {
                { ContractType.Auction, "Auction" },
                { ContractType.Courier, "Courier" },
                { ContractType.ItemExchange, "Item Exchange" },
                { ContractType.Loan, "Loan" },
                { ContractType.Unknown, "Unkown" }
            };

            foreach (var contract in contracts)
            {
                // Print important info at the begininng
                buffer.AppendLine(contract.Title);
                buffer.AppendLine(contractTypeNames[contract.Type]);
                buffer.AppendLine($"Availability: {contract.Availability}");
                buffer.AppendLine($"Status: {contract.Status}");
                buffer.AppendLine($"Expiration Date: {contract.DateExpired}");
                buffer.AppendLine($"Issuance Date: {contract.DateIssued}");

                // If it's completed, print the completion date
                if(contract.Status != "outstanding" || contract.Status != "in_progress")
                {
                    buffer.AppendLine($"Date Completed: {contract.DateCompleted}");
                }

                // Print issuer info
                buffer.AppendLine($"Issuer: {mainEsiInterface.universeInterface.IdToName(contract.IssuerId)}");
                buffer.AppendLine($"Issuer Corp: {mainEsiInterface.universeInterface.IdToName(contract.IssuerCorporationId)}");

                // If it's a courier contract, print start and destination
                if (contract.Type == ContractType.Courier)
                {
                    buffer.AppendLine($"Start Location: {mainEsiInterface.universeInterface.IdToName(contract.StartLocationId)}");
                    buffer.AppendLine($"Destination: {mainEsiInterface.universeInterface.IdToName(contract.EndLocationId)}");
                }

                // If items are involved, print them
                if (contract.Type != ContractType.Loan)
                {
                    buffer.AppendLine("Items:");
                    var items = getContractItemString(contract.ContractId);
                    buffer.AppendLine("\tOffering:");

                    if (items[0].Count == 0)
                    {
                        buffer.Append("\t\tNo items");
                    }
                    else
                    {
                        foreach (var item in items[0])
                        {
                            buffer.Append("\t\t");
                            buffer.Append(item);
                            buffer.Append('\n');
                        }
                    }

                    // Add ask items if it's an exchange
                    if(contract.Type == ContractType.ItemExchange)
                    {
                        buffer.AppendLine("\tAsking");
                        if (items[1].Count == 0)
                        {
                            buffer.Append("\t\tNo items");
                        }
                        else
                        {
                            foreach (var item in items[1])
                            {
                                buffer.Append("\t\t");
                                buffer.Append(item);
                                buffer.Append('\n');
                            }
                        }
                        
                        buffer.AppendLine($"Total Volume: {contract.Volume} m^3");
                    }
                    
                }

                // If it's a courier contract, print collateral and reward
                if(contract.Type == ContractType.Courier)
                {
                    buffer.AppendLine($"Collateral: {contract.Collateral} ISK");
                    buffer.AppendLine($"Reward: {contract.Reward} ISK");
                }

                // Potentially list price
                if(contract.Type == ContractType.Auction || contract.Type == ContractType.ItemExchange)
                {
                    buffer.AppendLine($"Price: {contract.Price} ISK");
                }

                // List buyout in an auction
                if(contract.Type == ContractType.Auction)
                {
                    buffer.AppendLine($"Buyout: {contract.Buyout} ISK");
                }

                // List bids
                if(contract.Type == ContractType.Auction)
                {
                    buffer.AppendLine("Bids:");
                    var bids = GetBidStrings(contract.ContractId);
                    if (bids.Count == 0) buffer.AppendLine("\t\tNo bids to show");
                    else
                    {
                        foreach (var bid in bids)
                        {
                            buffer.AppendLine($"\t\t{bid}");
                        }
                    }
                }

            }
        }

        public void PrintJsonList<T>(List<T> objList, int width, int rows, List<string> fields)
        {
            int counter = 0;
            var buffer = new StringBuilder();
            var jsonList = new List<JObject>();
            var ids = new Dictionary<string, HashSet<long>>();

            // Prepare dict of id fields to resolve
            foreach (var field in fields)
            {
                if (mainEsiInterface.IsIdField(field))
                {
                    ids.Add(field, new HashSet<long>());
                }
            }

            // Gather ids to resolve
            foreach (var item in objList)
            {
                var json = JObject.FromObject(item);
                foreach(var field in ids.Keys)
                {
                    var val = json[field].ToObject<long>();
                    if (!ids[field].Contains(val)) ids[field].Add(val);
                }
                jsonList.Add(json);
            }

            // Resolve the ids
            foreach (var pair in ids)
            {
                // First convert them to a list
                long[] temp = new long[pair.Value.Count];
                pair.Value.CopyTo(temp);

                // This will cache the results, and they can later be accessed by IdToName(long id)
                mainEsiInterface.universeInterface.IdToName(new List<long>(temp));
            }

            // Print each json as a row
            foreach (var json in jsonList)
            {
                if (counter >= rows)
                {
                    break;
                }

                var fieldString = "";
                for (int j = 0; j < fields.Count; j++)
                {
                    var key = fields[j];
                    // Print the resolved name for this id
                    if (mainEsiInterface.IsIdField(key))
                    {
                        // Ids are now cached, so this is just dictionary lookup
                        fieldString = mainEsiInterface.universeInterface.IdToName(json[key].ToObject<long>());
                    }

                    else
                    {
                        fieldString = json[key].ToString();
                    }

                    // Shorten it, since it doesnt fit inside the cell
                    if (fieldString.Length > width)
                    {
                        for (int k = 0; k < width - 3; k++)
                        {
                            buffer.Append(fieldString[k]);
                        }

                        buffer.Append("...");
                    }

                    // Otherwise it might be too short
                    else
                    {
                        buffer.Append(fieldString);
                        for (int k = fieldString.Length; k < width; k++)
                        {
                            buffer.Append(' ');
                        }
                    }

                    // Add the column separator
                    buffer.Append('|');
                }
                output.WriteLine(buffer.ToString());
                PrintLine(fields, width);
                buffer = buffer.Clear();
            }
        }
    }
}
