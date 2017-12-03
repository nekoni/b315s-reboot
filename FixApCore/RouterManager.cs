namespace FixApCore
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Xml.Linq;

    using Jurassic;
    using Jurassic.Library;

    public class RouterManager
    {
        private readonly string homePageUrl;

        private readonly string publicRsaKeyUrl;

        private readonly string loginPageUrl;

        private readonly string networkChangeUrl;

        private readonly string connectionStatusUrl;

        private readonly string rebootUrl;

        private readonly HttpClient client;

        private readonly HttpClientHandler httpClientHandler;

        private readonly string baseUrl;

        private readonly string user;

        private readonly string password;

        private string encpubkeyN;

        private string encpubkeyE;

        private string firstCsrf;

        private bool hasPublicKeys = false;

        private Encoding encoding = ASCIIEncoding.ASCII;

        private ScriptEngine engine;

        public RouterManager(string baseUrl, string user, string password)
        {
            this.baseUrl = baseUrl;
            this.homePageUrl = baseUrl + "/html/home.html";
            this.publicRsaKeyUrl = baseUrl + "/api/webserver/publickey";
            this.loginPageUrl = baseUrl + "/api/user/login";
            this.networkChangeUrl = baseUrl + "/api/net/net-mode";
            this.connectionStatusUrl = baseUrl + "/api/monitoring/status";
            this.rebootUrl = baseUrl + "/api/device/control";

            this.user = user;
            this.password = password;

            this.httpClientHandler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                UseCookies = true,
                CookieContainer = new CookieContainer()
            };

            this.client = new HttpClient(httpClientHandler);

            this.InitJsEngine();
        }

        private string EncodeData(string data)
        {
            if (!this.hasPublicKeys)
            {
                throw new NotSupportedException("You need to load keys first");
            }

            var rsa = new RsaEncryptor(this.encpubkeyN, this.encpubkeyE);
            var encString = Convert.ToBase64String(this.encoding.GetBytes(data));
            var num = (double)encString.Length / 245;
            var resTotal = string.Empty;
            for (int i = 0; i < num; i++)
            {
                var index = i * 245;
                var length = 245;
                if (index + 245 > encString.Length)
                {
                    length = encString.Length - index;
                }

                var encData = encString.Substring(index, length);
                var res = rsa.EncryptData(encData);
                resTotal += res;
            }

            return resTotal;
        }

        public async Task<string> GetConnectionTypeAsync()
        {
            await LoadCookiesIfNeededAsync();

            var status = await this.client.GetStringAsync(connectionStatusUrl);
            var match = Regex.Match(status, "<CurrentNetworkType>(?<mode>\\d+)</CurrentNetworkType>");
            if (match.Success)
            {
                return ConnectionStatusType.Parse(match.Groups["mode"].Value);
            }

            if (status.Contains("timed out"))
            {
                Console.WriteLine("Response from server timed out.");
            }
            else if (status.Contains("125002"))
            {
                Console.WriteLine("Probably router rebooted or Session Lost. Trying to load home page to get cookies again... ");

                this.hasPublicKeys = false;

                await this.client.GetStringAsync(this.homePageUrl);
            }
            else
            {
                Console.WriteLine("Couldn't get proper connection type (<CurrentNetworkType> tag). Response was: {0}", status);
            }

            return string.Empty;
        }

        public async Task<bool> RebootAsync()
        {
            await LoadHomePageWithCsrfAsync();

            var data = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><request><Control>1</Control></request>";
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri(this.rebootUrl),
                Method = HttpMethod.Post,
                Content = new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded")
            };

            request.Headers.Add("__RequestVerificationToken", firstCsrf);

            var response = await this.client.SendAsync(request);
            var result = await response.Content.ReadAsStringAsync();

            if (result.Contains("OK"))
            {
                Console.WriteLine("Rebooting...");
                return true;
            }

            Console.WriteLine("Reboot Failed!");
            Console.WriteLine("Response was: {0}", result);

            return false;
        }

        public async Task<bool> LoginAsync()
        {
            await GetPublicKeysAsync();

            return await this.DoLoginAsync();
        }

        public async Task<bool> SwitchConnectionTypeAsync(string connectionType)
        {
            await LoadHomePageWithCsrfAsync();

            var verboseType = ConnectionSwitchType.Parse(connectionType);
            Console.WriteLine("Switching to {0}", verboseType);

            var data =
                string.Format("<?xml version=\"1.0\" encoding=\"UTF-8\"?><request><NetworkMode>{0}</NetworkMode><NetworkBand>3FFFFFFF</NetworkBand><LTEBand>7FFFFFFFFFFFFFFF</LTEBand></request>", connectionType);

            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri(this.networkChangeUrl),
                Method = HttpMethod.Post,
                Content = new StringContent(data)
            };

            request.Headers.Add("__RequestVerificationToken", firstCsrf);

            var response = await this.client.SendAsync(request);
            var result = await response.Content.ReadAsStringAsync();

            if (result.Contains("OK"))
            {
                Console.WriteLine("Switching to {0}... OK!", verboseType);
                return true;
            }

            Console.WriteLine("Switching to {0}... Failed!", verboseType);
            Console.WriteLine("Response was: {0}", result);

            return false;
        }

        private void InitJsEngine()
        {
            engine = new ScriptEngine();
            engine.Global.SetPropertyValue("location", new Location(engine, homePageUrl), true);
            engine.SetGlobalValue("window", engine.Global);
            engine.SetGlobalValue("console", new FirebugConsole(engine));
            engine.EnableDebugging = true;
            engine.ForceStrictMode = false;
        }

        private async Task LoadCookiesIfNeededAsync(bool force = false)
        {
            var cookie = this.httpClientHandler.CookieContainer.GetCookies(new Uri(this.baseUrl))?["SessionID"];
            if (cookie == null || force)
            {
                await this.client.GetStringAsync(this.homePageUrl);
                await GetPublicKeysAsync();
            }
        }

        private async Task<bool> DoLoginAsync()
        {
            Console.WriteLine("Login");

            await LoadHomePageWithCsrfAsync();

            engine.Evaluate(string.Format("var name = '{0}';", this.user));
            engine.Evaluate(string.Format("var password = '{0}';", this.password));
            engine.Evaluate(string.Format("var g_password_type = '4';"));

            var jsResult = engine.Evaluate(
                "psd = base64encode(SHA256(name + base64encode(SHA256(password)) + g_requestVerificationToken[0]));");

            var sb = new StringBuilder();
            sb.AppendLine("var request = {");
            sb.AppendLine("Username: name,");
            sb.AppendLine("Password: psd,");
            sb.AppendLine("password_type: g_password_type");
            sb.AppendLine("};");
            sb.AppendLine("var xmlDate = object2xml('request', request);");

            engine.Evaluate(sb.ToString());

            var rsaData = engine.Evaluate("xmlDate");

            var data = EncodeData(rsaData.ToString());

            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri(this.loginPageUrl),
                Method = HttpMethod.Post,
                Content = new StringContent(data)
            };

            request.Headers.Add("__RequestVerificationToken", firstCsrf);
            request.Headers.Add("encrypt_transmit", "encrypt_transmit");

            var response = await this.client.SendAsync(request);
            var result = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode &&
                result.Contains("OK"))
            {
                Console.WriteLine("Login... Done!");
                return true;
            }

            var errorMessage = ProcessErrorMessages(result);
            Console.WriteLine("Logging in failed: {0}", errorMessage);

            return false;
        }

        private static string ProcessErrorMessages(string result)
        {
            if (result.Contains("108006"))
            {
                return "Either username or password was incorrect.";
            }

            if (result.Contains("100008"))
            {
                return "Some unknown error 100008 happened. Not sure why this happens yet. The application will probably try to login and fail all the time, until restarted.";
            }

            return result;
        }

        private async Task GetPublicKeysAsync()
        {
            if (this.hasPublicKeys)
            {
                Console.WriteLine("We already have public keys, not loading them.");
                return;
            }

            Console.WriteLine("Getting PublicKeys");

            var rsaPage = await this.client.GetStringAsync(publicRsaKeyUrl);
            var rsaXmlObject = XDocument.Parse(rsaPage);
            var rsaXmlResponse = rsaXmlObject.Element("response");
            this.encpubkeyE = rsaXmlResponse.Element("encpubkeye").Value;
            this.encpubkeyN = rsaXmlResponse.Element("encpubkeyn").Value;

            Console.WriteLine(
                "Public Key E: {0}, N: {1}",
                this.encpubkeyE,
                this.encpubkeyN.Remove(4) + "..." + this.encpubkeyN.Substring(this.encpubkeyN.Length - 4));
            var directory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var scriptSource = new FileScriptSource(Path.Combine(directory,"main.js"));
            var jsResult = engine.Evaluate(scriptSource);

            this.engine.Evaluate(
                string.Format(
                    "g_encPublickey.e = '{0}'; g_encPublickey.n = '{1}';",
                    this.encpubkeyE,
                    this.encpubkeyN));

            await this.LoadHomePageWithCsrfAsync();

            this.hasPublicKeys = true;

            Console.WriteLine("Getting PublicKeys... Done!");
        }

        private async Task LoadHomePageWithCsrfAsync()
        {
            var result = await this.client.GetStringAsync(homePageUrl);
            var matches = Regex.Matches(
                result,
                "name=\"csrf_token\" content=\"(?<data>[^\"]*)\"",
                RegexOptions.Singleline);
            this.firstCsrf = string.Empty;
            var secondCsrf = string.Empty;
            if (matches.Count == 2)
            {
                this.firstCsrf = matches[0].Groups["data"].Value;
                secondCsrf = matches[1].Groups["data"].Value;

                engine.Evaluate(
                    string.Format("g_requestVerificationToken = ['{0}', '{1}']",
                    firstCsrf,
                    secondCsrf));
            }
        }

        private class ConnectionStatusType
        {
            public static string LTE = "19";

            public static string ThreeG = "9";

            public static string NoService = "0";

            public static string Parse(string connectionTypeNumber)
            {
                if (ConnectionStatusType.LTE == connectionTypeNumber)
                {
                    return "LTE";
                }

                if (ConnectionStatusType.ThreeG == connectionTypeNumber)
                {
                    return "3G";
                }

                if (ConnectionStatusType.NoService == connectionTypeNumber)
                {
                    return "No Service";
                }

                return string.Format("Unknown (#{0})", connectionTypeNumber);
            }
        }

        private class ConnectionSwitchType
        {
            public static string LTE = "03";

            public static string Auto = "00";

            public static string Parse(string numeric)
            {
                if (numeric == LTE)
                {
                    return "LTE";
                }

                if (numeric == Auto)
                {
                    return "Auto";
                }

                return "Unknown";
            }
        }
    }
}
