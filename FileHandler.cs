using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace Lamp
{
    internal static class FileHandler
    {
        private static readonly HttpClient Client = new HttpClient();
        public static readonly string LocalDirectory = AppDomain.CurrentDomain.BaseDirectory;
        public static void DownloadZip(string downloadURL, string destinationPath)
        {
            Client.DefaultRequestHeaders.Accept.Clear();
            Client.DefaultRequestHeaders.Add("User-Agent", "Genie Client Updater");
            var response = Client.GetAsync(new Uri(downloadURL)).Result;
            using (var zipFile = new FileStream(destinationPath, FileMode.Create))
            {
                response.Content.CopyToAsync(zipFile);
            }
        }

        public static void AcquirePackage(string packageURL, string packageDestination)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.Write("The Updater is not currently supported for this system.");
            }
            try
            {
                DownloadZip(packageURL, packageDestination);
                if (File.Exists(packageDestination))
                {
                    string? destinationDirectory = Path.GetDirectoryName(packageDestination);
                    FileInfo file = new FileInfo(packageDestination);
                    do { Thread.Sleep(10); } while (FileIsLocked(file));
                    Console.WriteLine("Extracting Files.");
                    ZipFile.ExtractToDirectory(packageDestination, destinationDirectory, true);
                    do { Thread.Sleep(10); } while (FileIsLocked(file));
                    Console.WriteLine("Cleaning Up.");
                    File.Delete(packageDestination);
                }
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static void LoadReleaseAssets(Release release)
        {
            if (!string.IsNullOrWhiteSpace(release.AssetsURL))
            {
                release.Assets = new Dictionary<string, Asset>();
                Client.DefaultRequestHeaders.Accept.Clear();
                Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
                Client.DefaultRequestHeaders.Add("User-Agent", "Genie Client Updater");

                var streamTask = Client.GetStreamAsync(release.AssetsURL).Result;
                List<Asset> latestAssets = JsonSerializer.Deserialize<List<Asset>>(streamTask);
                foreach (Asset asset in latestAssets)
                {
                    release.Assets.Add(asset.Name, asset);
                }
            }
        }

        public static async Task<Release> GetRelease(string githubPath)
        {
            Client.DefaultRequestHeaders.Accept.Clear();
            Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            Client.DefaultRequestHeaders.Add("User-Agent", "Genie Client Updater");
            try
            {
                var streamTask = Client.GetStreamAsync(Paths.GitHub.LatestRelease);

                Release latest = await JsonSerializer.DeserializeAsync<Release>(await streamTask);
                return latest;
            }
            catch (Exception ex)
            {
                return new Release() { Version = "404", AssetsURL = "Something's wrong. No server version could be found." };
            }

        }

        public static async Task<Release> GetCurrentVersion()
        {
            Release current = new Release();
            if (File.Exists($"{LocalDirectory}\\genie.exe"))
            {
                current.Version = FileVersionInfo.GetVersionInfo($"{LocalDirectory}\\genie.exe").FileVersion;
            }
            else
            {
                current.Version = "0";
            }
            return current;

        }

        public static void LaunchGenie()
        {
            if (File.Exists(@$"{LocalDirectory}\genie.exe"))
            {
                Console.WriteLine("[Launching Genie]");
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    FileInfo file = new FileInfo(@$"{LocalDirectory}\genie.exe");
                    do { Thread.Sleep(10); } while (FileIsLocked(file));
                    Process.Start($"{LocalDirectory}\\genie.exe");
                }
            }
        }

        public static string GetDataDirectory(bool local)
        {
            if (local) return LocalDirectory;

            string dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            dir = System.IO.Path.Combine(dir, Lamp.GenieProductName);
            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            return dir;
        }
        

        public static bool FileIsLocked(FileInfo file)
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
