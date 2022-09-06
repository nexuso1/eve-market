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
    /// <summary>
    /// Main interface for communicating with the ESI API. Holds references
    /// to specialized interfaces, has some helper functions and takes care of
    /// authentication of the user.
    /// </summary>
    public class MainEsiInterface
    {
        /// <summary>
        /// ESI API Client instance
        /// </summary>
        public EsiClient Client;

        /// <summary>
        /// Stream to which output will be written
        /// </summary>
        public TextWriter Output;
        private SsoToken authToken;
        private AuthorizedCharacterData authData;

        /// <summary>
        /// Bool which is set to true if a character is authorized
        /// </summary>
        public bool IsAuthorized { get; private set; }
        /// <summary>
        /// Auth Token obtained from the server
        /// </summary>
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

        /// <summary>
        /// Information about the authorized character
        /// </summary>
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

        /// <summary>
        /// MarketInterface instance, used for accessing the 
        /// Market portion of ESI
        /// </summary>
        public MarketInterface marketInterface;

        /// <summary>
        /// UniverseInterface instance, used for accessing the 
        /// Universe portoin of ESI
        /// </summary>
        public UniverseInterface universeInterface;

        /// <summary>
        /// ContractInterface instance, used for accessing the 
        /// Universe portion of ESI
        /// </summary>
        public ContractInterface contractInterface;

        /// <summary>
        /// Printer instance, used for nice printing of various 
        /// data gathered from ESI
        /// </summary>
        public Printer printer;

        /// <summary>
        /// Basic contstructor, creates the instances of other interfaces and the 
        /// Printer, and also creates the EsiClient used for communication with ESI.
        /// If there is an auth token from a previous session, uses it again.
        /// </summary>
        /// <param name="writer">Output text stream</param>
        public MainEsiInterface(TextWriter writer)
        {
            Client = createClient();
            Output = writer;

            // Create interfaces
            marketInterface = new MarketInterface(this, Output);
            universeInterface = new UniverseInterface(this, Output);
            contractInterface = new ContractInterface(this, Output);
            printer = new Printer(this, Output);

            // Check if there is an existing authorization token
            if (File.Exists("refresh.token")) LoadAuthToken();

        }

        /// <summary>
        /// Helper function. Creates a new instance of EsiClient with the correct configuration
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Helper function which returns true if a given json field is an ID, and thus
        /// the corresponding name has to be resolved.
        /// </summary>
        /// <param name="field">Name of the field</param>
        /// <returns>True if  </returns>
        public bool IsIdField(string field)
        {
            var id_fields = new HashSet<string> { "location_id", "region_id", "type_id", "item_id",
            "acceptor_id", "assignee_id", "issuer_corporation_id", "issuer_id", "end_location_id", "start_location_id" };
            return id_fields.Contains(field);
        }

        /// <summary>
        /// Generates a state string of given length, used for OAuth.
        /// </summary>
        /// <param name="length">length of the state string</param>
        /// <returns>State string</returns>
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

        /// <summary>
        /// Handles the "help" command. Prints the
        /// text inside the help.txt file.
        /// </summary>
        /// <param name="tokens">Command line tokens. Only included for uniformity of API</param>
        public void HandleHelp(string[] tokens)
        {
            string helpText = File.ReadAllText("..\\..\\..\\help.txt");
            Output.Write(helpText);
        }

        /// <summary>
        /// Helper function which creates a string from a given slice
        /// of the "tokens" string array.
        /// </summary>
        /// <param name="tokens">Array of strings</param>
        /// <param name="offset">Beginning of the slice</param>
        /// <param name="size">Length of the slice</param>
        /// <param name="sep">Separator to be added between tokens</param>
        /// <returns>String of joined tokens starting at "offset" with a length of "size", separated by "sep"</returns>
        public string StringFromSlice(string[] tokens, int offset, int size, char sep = ' ')
        {
            return string.Join(sep, new ArraySegment<string>(tokens, offset, size).ToArray());
        }

        /// <summary>
        /// Checks whether a character is authorized. Returns the value
        /// of IsAuthorized an
        /// </summary>
        /// <returns></returns>
        public bool CheckAuthorization()
        {
            if (!IsAuthorized)
            {
                Output.WriteLine("No character authorized. Please use command \"authorize\" to authorize one of your characters.");
            }

            return IsAuthorized;
        }

        /// <summary>
        /// Loads the auth token from the save file if it exists, and
        /// authorizes the user. Has to recreate the Client for it to
        /// work properly.
        /// </summary>
        /// <param name="path">Path of the token save file</param>
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
                    Client.SetCharacterData(AuthData);
                }

                catch (ArgumentException)
                {
                    Output.WriteLine("Previous authorization is invalid. Please authorize a character again.");

                    // Reload the client (some weird OAuth bug, if it's not reloaded the requests contain multiple copies of same headers)
                    Client = createClient();
                }
            }
        }

        /// <summary>
        /// Saves the refresh token in a file.
        /// </summary>
        /// <param name="token">Save file path</param>
        private void SaveRefreshToken(SsoToken token)
        {
            using (var writer = new StreamWriter("refresh.token"))
            {
                writer.Write(token.RefreshToken);
            }
        }

        /// <summary>
        /// Logs the authorized character out (if there is any)
        /// </summary>
        /// <param name="tokens">Command line tokens. Only included for the uniformity of API</param>
        async public void HandleLogout(string[] tokens)
        {
            if (IsAuthorized)
            {
                await Client.SSO.RevokeToken(AuthData.Token);
                Client = createClient();
            }

            else
            {
                Output.WriteLine("No character authorized.");
            }
        }

        /// <summary>
        /// Handles the authorization of a user. Takes 
        /// care of the OAuth authorization process, and
        /// refers the end-user to the auth page.
        /// </summary>
        /// <param name="tokens">Command line tokens. Included for the uniformity of API</param>
        async public void HandleAuthorize(string[] tokens)
        {
            if (IsAuthorized)
            {
                await Client.SSO.RevokeToken(AuthData.Token);
                Client = createClient();
            }

            // OAuth code challeneg
            var challenge = Client.SSO.GenerateChallengeCode();
            // Random string for state parameter in ouath
            var state = GenerateStateString();
            // API scopes required for this app
            var scopes = new List<string>("publicData esi-wallet.read_character_wallet.v1 esi-search.search_structures.v1 esi-universe.read_structures.v1 esi-assets.read_assets.v1 esi-markets.structure_markets.v1 esi-markets.read_character_orders.v1 esi-contracts.read_character_contracts.v1".Split(" "));
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
