using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using ESI.NET;
using ESI.NET.Enumerations;
using Microsoft.Extensions.Options;
using System.Text;
using System.Collections.Generic;
using ESI.NET.Models.SSO;

namespace eve_market
{
    public class ApiInterface
    {
        public EsiClient esiClient { get; set; }
        public TextWriter output { get; set; }

        private SsoToken authToken { get; set; }
        private bool authorized = false;


        private readonly Random random = new Random();
        public ApiInterface(EsiClient client, TextWriter writer)
        {
            esiClient = client;
            output = writer;
        }

        public string GenerateStateString(int length = 10)
        {
            var temp_string = new StringBuilder(length);

            for (int i = 0; i < length; i++)
            {
                char ch = (char)random.Next('a', 'z');
                temp_string.Append(ch);
            }

            return temp_string.ToString();
        }

        public static int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        

        async public void HandleAuthorize(string[] tokens)
        {
            var challenge = esiClient.SSO.GenerateChallengeCode();
            var state = GenerateStateString();
            var scopes = new List<string>("publicData esi-wallet.read_character_wallet.v1 esi-assets.read_assets.v1 esi-markets.structure_markets.v1 esi-markets.read_character_orders.v1 esi-contracts.read_character_contracts.v1".Split(" "));
            var auth_url = esiClient.SSO.CreateAuthenticationUrl(scopes, state, challenge);

            Console.WriteLine("Please follow this link for authorization: " + auth_url);
            //System.Diagnostics.Process.Start("C:\\Windows\\Sysnative\\explorer.exe", auth_url); - potential automatic browser open
            HttpListener listener = new HttpListener();
            // Listen on this address
            string redirectUri = $"http://localhost:8080/";
            listener.Prefixes.Add(redirectUri);
            listener.Start();

            // Will hold the request response fromt the server
            var context = await listener.GetContextAsync();

            // Reterned url query string
            var queryString = context.Request.QueryString;
            var code = queryString["code"];
            var receivedChallenge = queryString["challenge"];

            // Tampered challenge
            if (challenge != receivedChallenge)
            {
                Console.WriteLine("Invalid challenge code received. Please check your network and try again.");
                return;
            }

            // Obtain the authorization token
            authToken = await esiClient.SSO.GetToken(GrantType.AuthorizationCode, code, challenge);
            if (authToken is not null)
            {
                authorized = true; // Go into authorized mode
            }

            // Prepare a response after authorization
            var response = context.Response;
            string responseString = "<html><head><meta http-equiv='refresh' content='10;url=https://eveonline.com'></head><body>Please return to the app.</body></html>";
            
            // Write the response to a buffer
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            var responseOutput = response.OutputStream;

            // Present it to the user
            await responseOutput.WriteAsync(buffer, 0, buffer.Length);
            responseOutput.Close();
            
            // Close the listener
            listener.Stop();

        }
    }

    public class InputParser
    {
        public ApiInterface apiInterface { get; set; }
        public InputParser(ApiInterface apiInterface)
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
            ApiInterface apiInterface = new ApiInterface(client, Console.Out);
            InputParser parser = new InputParser(apiInterface);

            parser.ParseInput(Console.In, Console.Out);
        }
    }
}
