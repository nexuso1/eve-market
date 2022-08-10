using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using ESI.NET.Models.SSO;
using ESI.NET;
using ESI.NET.Enumerations;
using Microsoft.Extensions.Options;

namespace eve_market
{
    public class MainEsiInterface
    {
        public EsiClient Client; // ESI API Client instance
        public TextWriter Output; // Stream to which output will be written

        private SsoToken authToken;
        private AuthorizedCharacterData authData;
        public bool IsAuthorized { get; private set; }
        public SsoToken AuthToken
        {
            private get { return authToken; }

            set
            {
                authToken = value;
                IsAuthorized = true;
                Output.WriteLine("Succesfully authorized.");
            }
        }

        public AuthorizedCharacterData AuthData
        {
            get { return authData; }
            set
            {
                authData = value;
                Output.WriteLine($"Welcome, {authData.CharacterName}.");
            }
        }

        private readonly Random random = new Random();

        public MarketInterface marketInterface;
        public UniverseInterface universeInterface;
        public Printer printer;
        public MainEsiInterface(TextWriter writer)
        {
            Client = createClient();
            Output = writer;

            // Create interfaces
            marketInterface = new MarketInterface(this, Output);
            universeInterface = new UniverseInterface(this, Output);
            printer = new Printer(this, Output);

            // Check if there is an existing authorization token
            if (File.Exists("refresh.token")) LoadAuthToken();

        }

        private EsiClient createClient()
        {
            IOptions<EsiConfig> config = Options.Create(new EsiConfig()
            {
                EsiUrl = "https://esi.evetech.net/",
                DataSource = DataSource.Tranquility,
                ClientId = "0ae5b0cb1d754a2f87bf2d4fc8e23f50",
                SecretKey = "ibhqopm0Kr4Bediz7PvppNuu3tiGbjnTQsHVHf3r", // Definitely should be somewhere secure, however this is just a project demo
                CallbackUrl = $"http://localhost:8080/",
                UserAgent = "Market-Interface"
            });

            return new EsiClient(config);
        }

        public bool IsIdField(string field)
        {
            var id_fields = new HashSet<string> { "location_id", "region_id", "type_id" };
            return id_fields.Contains(field);
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

        public string StringFromSlice(string[] tokens, int offset, int size, char sep = ' ')
        {
            return string.Join(' ', new ArraySegment<string>(tokens, offset, size).ToArray());
        }

        public void HandleSetDefault(string[] tokens)
        {
            marketInterface.HandleDefaults(tokens);
            return;
        }

        public bool CheckAuthorization()
        {
            if (!IsAuthorized)
            {
                Output.WriteLine("No character authorized. Please use command \"authorize\" to authorize one of your characters.");
                return false;
            }

            return true;
        }

        async private void LoadAuthToken(string path = "refresh.token")
        {
            using (var reader = new StreamReader(File.OpenRead(path)))
            {
                var refreshToken = reader.ReadLine();
                try
                {
                    var token = await Client.SSO.GetToken(GrantType.RefreshToken, refreshToken);
                    AuthToken = token;
                    AuthData = await Client.SSO.Verify(token);
                }

                catch (ArgumentException e)
                {
                    Output.WriteLine("Previous authorization is invalid. Please authorize a character again.");

                    // Reload the client (some weird OAuth bug, if it's not reloaded the requests contain multiple copies of same headers)
                    Client = createClient();
                }
            }
        }

        private void SaveRefreshToken(SsoToken token)
        {
            using (var writer = new StreamWriter("refresh.token"))
            {
                writer.Write(token.RefreshToken);
            }
        }

        async public void HandleAuthorize(string[] tokens)
        {
            if (IsAuthorized)
            {
                await Client.SSO.RevokeToken(AuthData.Token);
            }

            // OAuth code challeneg
            var challenge = Client.SSO.GenerateChallengeCode();
            // Random string for state parameter in ouath
            var state = GenerateStateString();
            // API scopes required for this app
            var scopes = new List<string>("publicData esi-wallet.read_character_wallet.v1 esi-assets.read_assets.v1 esi-markets.structure_markets.v1 esi-markets.read_character_orders.v1 esi-contracts.read_character_contracts.v1".Split(" "));
            var auth_url = Client.SSO.CreateAuthenticationUrl(scopes, state, challenge);

            Output.WriteLine("Please follow this link for authorization: " + auth_url);

            //TODO: potential automatic browser open
            //System.Diagnostics.Process.Start("C:\\Windows\\Sysnative\\explorer.exe", auth_url);
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
            var receivedState = queryString["state"];

            // Tampered challenge
            if (state != receivedState)
            {
                Output.WriteLine("Invalid state received. Please check your network and try again.");
                return;
            }

            // Obtain the authorization token
            AuthToken = await Client.SSO.GetToken(GrantType.AuthorizationCode, code, challenge);
            AuthData = await Client.SSO.Verify(AuthToken);
            Client.SetCharacterData(AuthData);
            SaveRefreshToken(AuthToken);

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
}
