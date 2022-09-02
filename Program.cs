using System;
using System.IO;

namespace eve_market
{
    public class InputParser
    {
        public MainEsiInterface apiInterface { get; set; }
        public InputParser(MainEsiInterface apiInterface)
        {
            this.apiInterface = apiInterface;
        }

        public void ParseInput(TextReader streamReader, TextWriter writer)
        {
            string line = null;
            while((line = streamReader.ReadLine()) != null){
                var tokens = line.Split();
                var command = tokens[0];
                switch (command)
                {
                    case "authorize":
                        apiInterface.HandleAuthorize(tokens);
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
                        writer.WriteLine("Printing help...");
                        break;

                    default:
                        writer.WriteLine("Unknown Command. For a list of available commands, type \"help\".");
                        break;
                }
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            System.Console.WriteLine("----- ESI Market Interface -----");
            MainEsiInterface apiInterface = new MainEsiInterface(Console.Out);
            InputParser parser = new InputParser(apiInterface);

            parser.ParseInput(Console.In, Console.Out);
        }
    }
}
