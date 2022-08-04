using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using ESI.NET.Models.SSO;
using ESI.NET;
using ESI.NET.Enumerations;

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
        public MainEsiInterface(EsiClient client, TextWriter writer)
        {
            Client = client;
            Output = writer;

            // Create interfaces
            marketInterface = new MarketInterface(this, Output);
            universeInterface = new UniverseInterface(this, Output);
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

        async public void HandleAuthorize(string[] tokens)
        {
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
            var authData = await Client.SSO.Verify(AuthToken);
            Client.SetCharacterData(authData);

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
