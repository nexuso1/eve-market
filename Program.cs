using System;
using System.Threading.Tasks;
using System.IO;

namespace eve_market
{
    /// <summary>
    /// Entry point of the program.
    /// </summary>
    class Program
    {
        /// <summary>
        /// Simple entry point function to the program. Turns the command line input into tokens
        /// and calls a function based on the value of the first token.
        /// </summary>
        /// <param name="streamReader">Input stream</param>
        /// <param name="writer">Output stream</param>
        /// <param name="apiInterface">MainEsiInterface instance</param>
        public static async Task ParseInput(TextReader streamReader, TextWriter writer, MainEsiInterface apiInterface)
        {
            string line = null;
            while ((line = streamReader.ReadLine()) != null)
            {
                var tokens = line.Split();
                var command = tokens[0];
                switch (command)
                {
                    case "authorize":
                        await apiInterface.HandleAuthorize(tokens);
                        break;
                    case "logout":
                        await apiInterface.HandleLogout(tokens);
                        break;

                    case "set":
                        await apiInterface.marketInterface.HandleDefaults(tokens);
                        break;

                    case "wallet":
                        await apiInterface.marketInterface.HandleWallet(tokens);
                        break;
                    case "my_orders":
                        await apiInterface.marketInterface.HandleMyOrders(tokens);
                        break;

                    case "my_order_history":
                        await apiInterface.marketInterface.HandleOrderHistory(tokens);
                        break;

                    case "info":
                        await apiInterface.universeInterface.HandleInfo(tokens);
                        break;

                    case "orders":
                        await apiInterface.marketInterface.HandleOrders(tokens);
                        break;

                    case "assets":
                        await apiInterface.marketInterface.HandleAssets(tokens);
                        break;

                    case "transactions":
                        await apiInterface.marketInterface.HandleTransactions(tokens);
                        break;
                    case "history":
                        await apiInterface.marketInterface.HandleHistory(tokens);
                        break;

                    case "contracts":
                        await apiInterface.contractInterface.HandleContracts(tokens);
                        break;
                    case "my_contracts":
                        await apiInterface.contractInterface.HandleMyContracts(tokens);
                        break;

                    case "exit":
                        return;

                    case "help":
                        apiInterface.HandleHelp(tokens);
                        break;

                    default:
                        writer.WriteLine("Unknown Command. For a list of available commands, type \"help\".");
                        break;
                }
            }
        }

        static async Task Main(string[] args)
        {
            Console.WriteLine("------------ ESI Market Interface ------------");
            Console.WriteLine("For a list of available commands type \"help\".");
            MainEsiInterface apiInterface = new MainEsiInterface(Console.Out);
            await ParseInput(Console.In, Console.Out, apiInterface);
        }
    }
}
