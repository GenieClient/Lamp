using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
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
            bool auto = false;
            bool forced = false;
            bool test = false;
            foreach(string arg in args)
            {
                       
                switch (args[0].ToLower())
                {
                    case "--automated":
                    case "--auto":
                    case "--a":
                        auto = true;
                        break;
                    case "--force":
                    case "--f":
                        forced = true;
                        break;
                    case "--t":
                    case "--test":
                        test = true;
                        break;
                    case "--interactive":
                    case "--i":
                    default:
                        RunInteractive();
                        break;
                }
            }
            if (auto)
            {
                if(test)
                {
                    AutomatedTest();
                }
                else
                {
                    AutomatedUpdate(forced);
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
            Release latest = GetRelease(Paths.GitHub.LatestRelease).Result;
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
                Console.WriteLine($"Yes or Latest to download Genie {latest.Version}.");
                Console.WriteLine("Test to download the latest Test Release.");
                Console.WriteLine(Fluff.Prompt);
                string? response = Console.ReadLine();
                if (response.ToLower().StartsWith("y") || response.ToLower().StartsWith("latest"))
                {
                    Console.WriteLine(Fluff.Prompt);
                    AutomatedUpdate(latest, current, true);
                }
                if (response.ToLower().StartsWith("test"))
                {
                    Console.WriteLine(Fluff.Prompt);
                    AutomatedTest();
                }
                else
                {
                    if (current.Version != "0")
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
            Release latest = GetRelease(Paths.GitHub.LatestRelease).Result;
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

        private static void AutomatedTest()
        {
            Release testRelease = GetRelease(Paths.GitHub.TestRelease).Result;
            AcquirePackage(testRelease);
        }

        private static void AcquirePackage(Release release)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.Write("The Updater is not currently supported for this system.");
            }
            try
            {
                release.LoadAssets();
                Asset zipFile = DownloadZip(release);
                if (File.Exists(zipFile.LocalFilepath))
                {
                    FileInfo file = new FileInfo(zipFile.LocalFilepath);
                    do { Thread.Sleep(10); } while (FileIsLocked(file));
                    Console.WriteLine("Extracting Files.");
                    ZipFile.ExtractToDirectory(zipFile.LocalFilepath, Environment.CurrentDirectory, true);
                    do { Thread.Sleep(10); } while (FileIsLocked(file));
                    Console.WriteLine("Cleaning Up.");
                    File.Delete(zipFile.LocalFilepath);
                    if (File.Exists(@$"{Environment.CurrentDirectory}\genie.exe"))
                    {
                        Console.WriteLine("Launching Genie");
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            file = new FileInfo(@$"{Environment.CurrentDirectory}\genie.exe");
                            do { Thread.Sleep(10); } while (FileIsLocked(file));
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

        private static async Task<Release> GetRelease(string githubPath)
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

        private static bool FileIsLocked(FileInfo file)
        {
            try
            {
                using (FileStream stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }

            //file is not locked
            return false;
        }
    }
}