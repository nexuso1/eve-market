using System;
using System.IO;

namespace eve_market
{
    class Program
    {
        /// <summary>
        /// Simple entry point function to the program. Turns the command line input into tokens
        /// and calls a function based on the value of the first token.
        /// </summary>
        /// <param name="streamReader">Input stream</param>
        /// <param name="writer">Output stream</param>
        /// <param name="apiInterface">MainEsiInterface instance</param>
        public static void ParseInput(TextReader streamReader, TextWriter writer, MainEsiInterface apiInterface)
        {
            string line = null;
            while ((line = streamReader.ReadLine()) != null)
            {
                var tokens = line.Split();
                var command = tokens[0];
                switch (command)
                {
                    case "authorize":
                        apiInterface.HandleAuthorize(tokens);
                        break;
                    case "logout":
                        apiInterface.HandleLogout(tokens);
                        break;

                    case "set":
                        apiInterface.marketInterface.HandleDefaults(tokens);
                        break;

                    case "wallet":
                        apiInterface.marketInterface.HandleWallet(tokens);
                        break;
                    case "my_orders":
                        apiInterface.marketInterface.HandleMyOrders(tokens);
                        break;

                    case "my_order_history":
                        apiInterface.marketInterface.HandleOrderHistory(tokens);
                        break;

                    case "info":
                        apiInterface.universeInterface.HandleInfo(tokens);
                        break;

                    case "orders":
                        apiInterface.marketInterface.HandleOrders(tokens);
                        break;

                    case "assets":
                        apiInterface.marketInterface.HandleAssets(tokens);
                        break;

                    case "transactions":
                        apiInterface.marketInterface.HandleTransactions(tokens);
                        break;
                    case "history":
                        apiInterface.marketInterface.HandleHistory(tokens);
                        break;

                    case "contracts":
                        apiInterface.contractInterface.HandleContracts(tokens);
                        break;
                    case "my_contracts":
                        apiInterface.contractInterface.HandleMyContracts(tokens);
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

        static void Main(string[] args)
        {
            Console.WriteLine("------------ ESI Market Interface ------------");
            Console.WriteLine("For a list of available commands type \"help\".");
            MainEsiInterface apiInterface = new MainEsiInterface(Console.Out);
            ParseInput(Console.In, Console.Out, apiInterface);
        }
    }
}
