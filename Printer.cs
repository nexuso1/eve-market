
using System.Collections.Generic;
using System.Text;
using System.IO;
using Newtonsoft.Json.Linq;
using ESI.NET.Enumerations;
namespace eve_market
{
    /// <summary>
    /// Class that handles printing of various data.
    /// </summary>
    public class Printer
    {
        /// <summary>
        /// Text output stream
        /// </summary>
        public TextWriter output;
        /// <summary>
        /// Reference to a MainIntreface instance.
        /// </summary>
        public MainEsiInterface mainEsiInterface;

        /// <summary>
        /// Basic constructor. Only assigns the instances from arguments to fields.
        /// </summary>
        /// <param name="interface">Reference to an (already created) MainInterface instance</param>
        /// <param name="textWriter">Text output stream</param>
        public Printer( MainEsiInterface esiInterface,TextWriter writer)
        {
            output = writer;
            mainEsiInterface = esiInterface;
        }

        /// <summary>
        /// Prints a line of a given length to the output stream.
        /// </summary>
        /// <param name="length">Length of the line</param>
        public void PrintLine(int length)
        {
            for (int i = 0; i < length; i++)
            {
                output.Write('-');
            }
            output.WriteLine();
        }

        /// <summary>
        /// Prints a line that adapts to the number of fields, 
        /// and the width corresponding to one field.
        /// </summary>
        /// <param name="fields">List of fields</param>
        /// <param name="width">Width for one field</param>
        public void PrintLine(List<string> fields, int width)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < (width + 1) * fields.Count; i++)
            {
                sb.Append('-');
            }

            output.WriteLine(sb.ToString());
        }

        /// <summary>
        /// Prints the header of a table, which contains the given fields, 
        /// with a cell width of "width".
        /// </summary>
        /// <param name="fields">List of field names</param>
        /// <param name="width">Width of a cell in the table</param>
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

        /// <summary>
        /// Prints the contracts from arguments. Warning, contracts have a lot 
        /// of variations and this function is quite extensive in its printing.
        /// Use listPublic if you're trying to print public contracts.
        /// </summary>
        /// <param name="contracts">List of contracts to print</param>
        public void PrintContracts(List<ESI.NET.Models.Contracts.Contract> contracts, bool listPublic = false)
        {
            // Prepare dict of id fields to resolve
            var idFields = new List<string> { "acceptor_id", "assignee_id", "issuer_corporation_id", "issuer_id", "end_location_id", "start_location_id" };
            var ids = new Dictionary<string, HashSet<long>>(); // Holds ids to resolved, grouped by field
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
                mainEsiInterface.universeInterface.ContractIdToName(new List<long>(temp), pair.Key);
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

            // Print the contracts
            foreach (var contract in contracts)
            {
                // Print important info at the begininng
                buffer.AppendLine(contract.Title);
                buffer.AppendLine(contractTypeNames[contract.Type]);
                buffer.AppendLine($"Availability: {contract.Availability}");
                buffer.AppendLine($"Status: {contract.Status}");
                buffer.AppendLine($"Expiration Date: {contract.DateExpired}");
                buffer.AppendLine($"Issuance Date: {contract.DateIssued}");

                // If it's completed, print the completion date and acceptor
                if(contract.Status != "outstanding" || contract.Status != "in_progress")
                {
                    buffer.AppendLine($"Date Completed: {contract.DateCompleted}");
                    buffer.AppendLine($"Acceptor: {mainEsiInterface.universeInterface.IdToName(contract.AcceptorId)}");
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
                    var items = mainEsiInterface.contractInterface.getContractItemString(contract.ContractId, listPublic);
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
                            buffer.AppendLine("\t\tNo items");
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
                    var bids = mainEsiInterface.contractInterface.GetBidStrings(contract.ContractId);
                    if (bids.Count == 0) buffer.AppendLine("\t\tNo bids to show");
                    else
                    {
                        foreach (var bid in bids)
                        {
                            buffer.AppendLine($"\t\t{bid}");
                        }
                    }
                }
                output.WriteLine(buffer.ToString());
                buffer = buffer.Clear();
            }
        }

        /// <summary>
        /// Prints a list of json-convertible objects as a table, with a given
        /// cell width and number of rows. Only prints the fields named in the 
        /// "fields". If a field is an ID field, also resolves it's name before
        /// printing.
        /// </summary>
        /// <typeparam name="T">JObject-convertible type</typeparam>
        /// <param name="objList">List of items to print</param>
        /// <param name="width">Table cell width</param>
        /// <param name="rows">Number of rows. If it's less than the length of "objList", prints only the first
        /// "rows" items.</param>
        /// <param name="fields">Keys of the fields to print.</param>
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
