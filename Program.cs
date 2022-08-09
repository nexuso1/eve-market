using System;
using System.IO;
using ESI.NET;
using ESI.NET.Enumerations;
using Microsoft.Extensions.Options;
using System.Text;
using System.Collections.Generic;
using ESI.NET.Models.SSO;

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
                        apiInterface.HandleSetDefault(tokens);
                        break;

                    case "wallet":
                        apiInterface.marketInterface.HandleWallet(tokens);
                        break;
                    case "orders":
                        apiInterface.marketInterface.HandleOrders(tokens);
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
            IOptions<EsiConfig> config = Options.Create(new EsiConfig()
            {
                EsiUrl = "https://esi.evetech.net/",
                DataSource = DataSource.Tranquility,
                ClientId = "0ae5b0cb1d754a2f87bf2d4fc8e23f50",
                SecretKey = "ibhqopm0Kr4Bediz7PvppNuu3tiGbjnTQsHVHf3r", // Definitely should be somewhere secure, however this is just a project demo
                CallbackUrl = $"http://localhost:8080/",
                UserAgent = "Market-Interface"
            });

            EsiClient client = new EsiClient(config);
            MainEsiInterface apiInterface = new MainEsiInterface(client, Console.Out);
            InputParser parser = new InputParser(apiInterface);

            parser.ParseInput(Console.In, Console.Out);
        }
    }
}
