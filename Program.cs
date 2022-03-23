using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;
using System.IO.Compression;

namespace Lamp
{
    public class Program
    {
        private static readonly HttpClient client = new HttpClient();
        static async Task Main(string[] args)
        {
            bool operating = true;
            if(args.Length > 0)
            {
             
                switch (args[0].ToLower())
                {
                    case "--automated":
                    case "--auto":
                    case "--a":
                        AutomatedUpdate(false);
                        break;
                    case "--force":
                    case "--f":
                        AutomatedUpdate(true);
                        break;
                    case "--interactive":
                    case "--i":
                    default:
                        RunInteractive();
                        break;
                }
            }
            else
            {
                RunInteractive();
            }

            
            Environment.Exit(0);
        }

        private static void RunInteractive()
        {
            Console.WriteLine(Fluff.RoomName);
            Console.WriteLine(Fluff.RoomDescription);
            Console.WriteLine(Fluff.ObviousExits);
            Console.WriteLine(Fluff.Prompt);
            Console.Write(Fluff.Examine);
            Release current = GetCurrentVersion().Result;
            Console.Write(current.Version);
            Console.WriteLine(Fluff.Prompt);
            Console.Write(Fluff.Server);
            Release latest = GetLatestRelease().Result;
            if(latest.Version == "404")
            {
                Console.Write(latest.AssetsURL);
                Console.WriteLine(Fluff.Prompt);
            }
            else
            {
                Console.Write(latest.Version);
                Console.WriteLine(Fluff.Prompt);
                Console.WriteLine("Would you like to download the latest version?");
                Console.WriteLine("Yes to download, literally anything else to close out.");
                Console.WriteLine(Fluff.Prompt);
                string? response = Console.ReadLine();
                if (response.ToLower().StartsWith("y"))
                {
                    Console.WriteLine(Fluff.Prompt);
                    AutomatedUpdate(latest, current, true);
                }
                else
                {
                    if(current.Version != "0")
                    {
                        Console.WriteLine(Fluff.Exit);
                        Console.WriteLine("[Launching Genie]");
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            Process.Start($"{Environment.CurrentDirectory}\\genie.exe");
                        }
                    }
                }
            }
        }
        private static void  AutomatedUpdate(bool force)
        {
            Release current = GetCurrentVersion().Result;
            Release latest = GetLatestRelease().Result;
            AutomatedUpdate(latest, current, force);
        }
        private static void AutomatedUpdate(Release latest, Release current, bool force)
        {
            
            if(!force && current.Version == latest.Version)
            {
                Console.WriteLine("This instance of Genie is using the latest release.");
                Environment.Exit(0);
            }
            else
            {
                AcquirePackage(latest);
            }
        }

        private static void AcquirePackage(Release latest)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.Write("The Updater is not currently supported for this system.");
            }
            latest.LoadAssets();
            try
            {
                Asset zipFile = DownloadZip(latest);
                if (File.Exists(zipFile.LocalFilepath))
                {
                    Console.WriteLine("Extracting Files.");
                    ZipFile.ExtractToDirectory(zipFile.LocalFilepath, Environment.CurrentDirectory, true);
                    Console.WriteLine("Cleaning Up.");
                    File.Delete(zipFile.LocalFilepath);
                    if (File.Exists($"{Environment.CurrentDirectory}\\genie.exe"))
                    {
                        Console.WriteLine("Launching Genie");
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            Process.Start($"{Environment.CurrentDirectory}\\genie.exe");
                        }
                    }
                }
            }
            catch(FileNotFoundException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static Asset DownloadZip(Release latest)
        {
            Asset zipAsset = new Asset() { Name = "Invalid" };
            if(RuntimeInformation.FrameworkDescription.StartsWith(".NET 6") && latest.Assets.ContainsKey(Paths.FileNames.RuntimeDependent))
            {
                zipAsset = latest.Assets[Paths.FileNames.RuntimeDependent];
            }
            else
            {
                if(RuntimeInformation.OSArchitecture.ToString() == "X64" && latest.Assets.ContainsKey(Paths.FileNames.x64))
                {
                    zipAsset = latest.Assets[Paths.FileNames.x64];
                }
                else if (latest.Assets.ContainsKey(Paths.FileNames.x86))
                {
                    zipAsset = latest.Assets[Paths.FileNames.x86];
                }
                else
                {
                    throw new FileNotFoundException("No valid package was found for your system.");
                }
            }
            Console.WriteLine("Downloading latest client");
            try
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Add("User-Agent", "Genie Client Updater");
                var response = client.GetAsync(new Uri(zipAsset.DownloadURL)).Result;
                using (var zipFile = new FileStream(zipAsset.LocalFilepath, FileMode.Create))
                {
                    response.Content.CopyToAsync(zipFile);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            
            return zipAsset;
        }

        private static async Task<Release> GetLatestRelease()
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            client.DefaultRequestHeaders.Add("User-Agent", "Genie Client Updater");
            try
            {
                var streamTask = client.GetStreamAsync(Paths.GitHub.LatestRelease);

                Release latest = await JsonSerializer.DeserializeAsync<Release>(await streamTask);
                return latest;
            }
            catch (Exception ex)
            {
                return new Release() { Version = "404", AssetsURL = "Something's wrong. No server version could be found." };
            }
            
        }

        private static async Task<Release> GetCurrentVersion()
        {
            Release current = new Release();
            if(File.Exists($"{Environment.CurrentDirectory}\\genie.exe"))
            {
                current.Version = FileVersionInfo.GetVersionInfo($"{Environment.CurrentDirectory}\\genie.exe").FileVersion;
            }
            else
            {
                current.Version = "0";
            }
            return current;
            
        }

        public static void OpenBrowser(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }
    }
}