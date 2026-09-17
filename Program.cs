using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace EarthdataAuthSetupDotNet
{
    public sealed class AuthPaths
    {
        public string BaseDir { get; set; }
        public string NetrcPath { get; set; }
        public string DodsrcPath { get; set; }
        public string UrsCookiesPath { get; set; }
        public string EdlTokenPath { get; set; }
    }

    public sealed class CredentialPair
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public sealed class Options
    {
        public string TargetDir { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool ForceRewriteNetrc { get; set; }
        public bool OpenLoginPage { get; set; }
        public string MockToken { get; set; }
        public bool ShowHelp { get; set; }

        public static Options Parse(string[] args)
        {
            var options = new Options();
            options.TargetDir = Directory.GetCurrentDirectory();
            options.Username = Environment.GetEnvironmentVariable("EARTHDATA_USERNAME");
            options.Password = Environment.GetEnvironmentVariable("EARTHDATA_PASSWORD");

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase))
                {
                    options.ShowHelp = true;
                }
                else if (string.Equals(arg, "--target-dir", StringComparison.OrdinalIgnoreCase))
                {
                    options.TargetDir = ReadValue(args, ref i, arg);
                }
                else if (string.Equals(arg, "--username", StringComparison.OrdinalIgnoreCase))
                {
                    options.Username = ReadValue(args, ref i, arg);
                }
                else if (string.Equals(arg, "--password", StringComparison.OrdinalIgnoreCase))
                {
                    options.Password = ReadValue(args, ref i, arg);
                }
                else if (string.Equals(arg, "--force-rewrite-netrc", StringComparison.OrdinalIgnoreCase))
                {
                    options.ForceRewriteNetrc = true;
                }
                else if (string.Equals(arg, "--open-login-page", StringComparison.OrdinalIgnoreCase))
                {
                    options.OpenLoginPage = true;
                }
                else if (string.Equals(arg, "--mock-token", StringComparison.OrdinalIgnoreCase))
                {
                    options.MockToken = ReadValue(args, ref i, arg);
                }
                else
                {
                    throw new EarthdataAuthException("Unknown argument: " + arg);
                }
            }

            return options;
        }

        private static string ReadValue(string[] args, ref int index, string argumentName)
        {
            if (index + 1 >= args.Length)
            {
                throw new EarthdataAuthException("Missing value for " + argumentName);
            }

            index++;
            return args[index];
        }
    }

    public sealed class EarthdataAuthException : Exception
    {
        public EarthdataAuthException(string message) : base(message)
        {
        }

        public EarthdataAuthException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public static class EarthdataAuthSetup
    {
        public const string EarthdataHost = "urs.earthdata.nasa.gov";
        public const string EarthdataLoginUrl = "https://urs.earthdata.nasa.gov/";
        public const string EarthdataTokenUrl = "https://urs.earthdata.nasa.gov/api/users/find_or_create_token";

        public static AuthPaths GetAuthPaths(string targetDir)
        {
            string baseDir = string.IsNullOrWhiteSpace(targetDir)
                ? Directory.GetCurrentDirectory()
                : Path.GetFullPath(targetDir);

            return new AuthPaths
            {
                BaseDir = baseDir,
                NetrcPath = Path.Combine(baseDir, ".netrc"),
                DodsrcPath = Path.Combine(baseDir, ".dodsrc"),
                UrsCookiesPath = Path.Combine(baseDir, ".urs_cookies"),
                EdlTokenPath = Path.Combine(baseDir, ".edl_token")
            };
        }

        public static void EnsureSupportFiles(AuthPaths paths)
        {
            Directory.CreateDirectory(paths.BaseDir);

            if (!File.Exists(paths.UrsCookiesPath))
            {
                File.WriteAllText(paths.UrsCookiesPath, string.Empty, Encoding.UTF8);
            }

            File.WriteAllText(
                paths.DodsrcPath,
                "HTTP.COOKIEJAR=" + paths.UrsCookiesPath + Environment.NewLine +
                "HTTP.NETRC=" + paths.NetrcPath + Environment.NewLine,
                Encoding.UTF8);
        }

        public static void WriteNetrcFile(AuthPaths paths, string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                throw new EarthdataAuthException("Earthdata username and password are required.");
            }

            string netrcContent = string.Format(
                "machine {0} login {1} password {2}{3}",
                EarthdataHost,
                username.Trim(),
                password.Trim(),
                Environment.NewLine);

            File.WriteAllText(paths.NetrcPath, netrcContent, Encoding.UTF8);
        }

        public static CredentialPair ReadNetrcCredentials(string netrcPath)
        {
            if (!File.Exists(netrcPath))
            {
                throw new EarthdataAuthException("No .netrc file exists at " + netrcPath);
            }

            string content = File.ReadAllText(netrcPath, Encoding.UTF8);
            string[] tokens = Regex.Split(content, "\\s+");
            int index = 0;

            while (index < tokens.Length)
            {
                string token = tokens[index];
                if (string.IsNullOrWhiteSpace(token))
                {
                    index++;
                    continue;
                }

                if (string.Equals(token, "machine", StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 1 >= tokens.Length)
                    {
                        break;
                    }

                    string host = tokens[index + 1];
                    index += 2;

                    string username = null;
                    string password = null;

                    while (index < tokens.Length && !string.Equals(tokens[index], "machine", StringComparison.OrdinalIgnoreCase))
                    {
                        string current = tokens[index];
                        if (string.Equals(current, "login", StringComparison.OrdinalIgnoreCase) && index + 1 < tokens.Length)
                        {
                            username = tokens[index + 1];
                            index += 2;
                        }
                        else if (string.Equals(current, "password", StringComparison.OrdinalIgnoreCase) && index + 1 < tokens.Length)
                        {
                            password = tokens[index + 1];
                            index += 2;
                        }
                        else
                        {
                            index++;
                        }
                    }

                    if (string.Equals(host, EarthdataHost, StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                        {
                            throw new EarthdataAuthException("Incomplete credentials found in " + netrcPath + ".");
                        }

                        return new CredentialPair
                        {
                            Username = username,
                            Password = password
                        };
                    }
                }
                else
                {
                    index++;
                }
            }

            throw new EarthdataAuthException(
                "No credentials for '" + EarthdataHost + "' were found in " + netrcPath + ".");
        }

        public static CredentialPair PromptForCredentials(string existingNetrcPath)
        {
            Console.WriteLine("Earthdata Login is required to access Giovanni and related APIs.");
            Console.WriteLine("If you need an account, create or manage it here: " + EarthdataLoginUrl);

            if (!string.IsNullOrWhiteSpace(existingNetrcPath) && File.Exists(existingNetrcPath))
            {
                Console.WriteLine("A local .netrc already exists at: " + existingNetrcPath);
            }

            Console.Write("Enter NASA Earthdata Login Username (or create an account at urs.earthdata.nasa.gov): ");
            string username = Console.ReadLine();
            string password = ReadPassword("Enter NASA Earthdata Login Password: ");

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                throw new EarthdataAuthException("Earthdata username and password are required.");
            }

            return new CredentialPair
            {
                Username = username.Trim(),
                Password = password.Trim()
            };
        }

        public static string ReadPassword(string prompt)
        {
            Console.Write(prompt);
            var builder = new StringBuilder();

            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (builder.Length > 0)
                    {
                        builder.Length -= 1;
                    }
                    continue;
                }

                if (!char.IsControl(key.KeyChar))
                {
                    builder.Append(key.KeyChar);
                }
            }

            return builder.ToString();
        }

        public static bool PromptYesNo(string message, bool defaultValue)
        {
            string suffix = defaultValue ? "[Y/n]" : "[y/N]";

            while (true)
            {
                Console.Write(message + " " + suffix + " ");
                string reply = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(reply))
                {
                    return defaultValue;
                }

                string normalized = reply.Trim().ToLowerInvariant();
                if (normalized == "y" || normalized == "yes")
                {
                    return true;
                }

                if (normalized == "n" || normalized == "no")
                {
                    return false;
                }

                Console.WriteLine("Please respond with 'y' or 'n'.");
            }
        }

        public static string GenerateEarthdataToken(string username, string password)
        {
            string rawCredentials = username + ":" + password;
            string encodedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawCredentials));

            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Post, EarthdataTokenUrl))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encodedCredentials);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

                HttpResponseMessage response = client.SendAsync(request).GetAwaiter().GetResult();
                string responseText = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                if (!response.IsSuccessStatusCode)
                {
                    throw new EarthdataAuthException(
                        "Token request failed with status " + ((int)response.StatusCode).ToString() + ": " + responseText);
                }

                string token = ExtractJsonStringValue(responseText, "access_token");
                if (string.IsNullOrWhiteSpace(token))
                {
                    throw new EarthdataAuthException(
                        "Earthdata token response did not contain 'access_token': " + responseText);
                }

                return token;
            }
        }

        public static string ExtractJsonStringValue(string json, string propertyName)
        {
            string pattern = "\"" + Regex.Escape(propertyName) + "\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\"";
            Match match = Regex.Match(json ?? string.Empty, pattern);
            if (!match.Success)
            {
                return null;
            }

            return Regex.Unescape(match.Groups["value"].Value);
        }

        public static void SaveToken(AuthPaths paths, string token)
        {
            File.WriteAllText(paths.EdlTokenPath, (token ?? string.Empty).Trim(), Encoding.UTF8);
        }

        public static AuthPaths SetupEarthdataAuth(Options options)
        {
            var paths = GetAuthPaths(options.TargetDir);
            EnsureSupportFiles(paths);

            if (options.OpenLoginPage)
            {
                TryOpenLoginPage();
            }

            if (!string.IsNullOrWhiteSpace(options.Username) && !string.IsNullOrWhiteSpace(options.Password))
            {
                WriteNetrcFile(paths, options.Username, options.Password);
            }
            else if (File.Exists(paths.NetrcPath) && !options.ForceRewriteNetrc)
            {
                if (PromptYesNo("Reuse the existing local .netrc file?", true))
                {
                    ReadNetrcCredentials(paths.NetrcPath);
                }
                else
                {
                    CredentialPair promptedCredentials = PromptForCredentials(paths.NetrcPath);
                    WriteNetrcFile(paths, promptedCredentials.Username, promptedCredentials.Password);
                }
            }
            else
            {
                CredentialPair promptedCredentials = PromptForCredentials(paths.NetrcPath);
                WriteNetrcFile(paths, promptedCredentials.Username, promptedCredentials.Password);
            }

            CredentialPair credentials = ReadNetrcCredentials(paths.NetrcPath);
            string token = !string.IsNullOrWhiteSpace(options.MockToken)
                ? options.MockToken
                : GenerateEarthdataToken(credentials.Username, credentials.Password);

            SaveToken(paths, token);
            return paths;
        }

        public static void TryOpenLoginPage()
        {
            try
            {
                var startInfo = new ProcessStartInfo();
                startInfo.FileName = EarthdataLoginUrl;
                startInfo.UseShellExecute = true;
                Process.Start(startInfo);
            }
            catch
            {
                Console.WriteLine("Open this page in your browser if needed: " + EarthdataLoginUrl);
            }
        }

        public static void PrintHelp()
        {
            Console.WriteLine("EarthdataAuthSetupDotNet");
            Console.WriteLine();
            Console.WriteLine("Creates .urs_cookies, .dodsrc, .netrc, and .edl_token for Earthdata API access.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --target-dir <path>        Directory where auth files should be created.");
            Console.WriteLine("  --username <value>         Earthdata username. Can also come from EARTHDATA_USERNAME.");
            Console.WriteLine("  --password <value>         Earthdata password. Can also come from EARTHDATA_PASSWORD.");
            Console.WriteLine("  --force-rewrite-netrc      Always prompt and overwrite the local .netrc file.");
            Console.WriteLine("  --open-login-page          Open the Earthdata login page in your default browser.");
            Console.WriteLine("  --mock-token <value>       Skip the live token request and write this token value instead.");
            Console.WriteLine("  -h, --help                 Show this help message.");
        }
    }

    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                Options options = Options.Parse(args);
                if (options.ShowHelp)
                {
                    EarthdataAuthSetup.PrintHelp();
                    return 0;
                }

                AuthPaths paths = EarthdataAuthSetup.SetupEarthdataAuth(options);
                Console.WriteLine("Earthdata authentication files are ready:");
                Console.WriteLine("  .urs_cookies : " + paths.UrsCookiesPath);
                Console.WriteLine("  .dodsrc      : " + paths.DodsrcPath);
                Console.WriteLine("  .netrc       : " + paths.NetrcPath);
                Console.WriteLine("  .edl_token   : " + paths.EdlTokenPath);
                Console.WriteLine("Earthdata token created successfully.");
                return 0;
            }
            catch (EarthdataAuthException ex)
            {
                Console.Error.WriteLine("Earthdata setup failed: " + ex.Message);
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Unexpected error: " + ex.Message);
                return 1;
            }
        }
    }
}

