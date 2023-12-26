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
        public static readonly string LampFilepath = Path.Combine(LocalDirectory, "Lamp.exe");


        public static void DownloadZip(string downloadURL, string destinationPath)
        {
            Client.DefaultRequestHeaders.Accept.Clear();
            Client.DefaultRequestHeaders.Add("User-Agent", "Genie Client Updater");
            var response = Client.GetAsync(new Uri(downloadURL)).Result;
            string directory = Path.GetDirectoryName(destinationPath);
            if(!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            using (var zipFile = new FileStream(destinationPath, FileMode.Create))
            {
                response.Content.CopyToAsync(zipFile);
            }
        }

        public static MemoryStream DownloadToMemoryStream(string downloadURL)
        {
            Client.DefaultRequestHeaders.Accept.Clear();
            Client.DefaultRequestHeaders.Add("User-Agent", "Genie Client Updater");
            var response = Client.GetAsync(new Uri(downloadURL)).Result;
            MemoryStream memoryStream = new MemoryStream();
            response.Content.CopyToAsync(memoryStream);
            return memoryStream;
        }

        public static async Task<bool> AcquirePackageInMemory(string packageURL, string packageDestination)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.Write("The Updater is not currently supported for this system.");
                return false;
            }
            try
            {
                MemoryStream zipFile = DownloadToMemoryStream(packageURL);
                ZipArchive archive = new ZipArchive(zipFile);
                int stripFromBeginning = 0;
                if (archive.Entries.Count > 0 && archive.Entries[0].FullName.EndsWith("-main/"))
                {
                    //if this is from a github repo it will be in a folder in the zip's root that ends -main/
                    //and we want to not extract that and extract from it so we need to strip it off the path
                    stripFromBeginning = archive.Entries[0].FullName.Length;
                }
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string packageFile = Path.Combine(packageDestination, entry.FullName.Remove(0, stripFromBeginning).Replace("/", "\\"));
                    if (packageFile.ToUpper() == LampFilepath.ToUpper()) continue; //don't try to update Lamp, we're mid-execution.
                    if (entry.FullName.Length - stripFromBeginning > 0)
                    {
                        if (File.Exists(packageFile))
                        {
                            FileInfo existingFile = new FileInfo(packageFile);
                            if (existingFile.LastWriteTime != entry.LastWriteTime)
                            {
                                File.Delete(packageFile);
                            }
                        }
                        if (packageFile.EndsWith("\\")) 
                        {
                            if(!Directory.Exists(packageFile)) Directory.CreateDirectory(packageFile);
                        }
                        else
                        {
                            if (!File.Exists(packageFile)) entry.ExtractToFile(packageFile);
                        }
                    }
                }
                return true;
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        
        public static bool ArchiveExtensions(string directory, string archiveName, string extension)
        {
            try
            {
                string archiveFile = Path.Combine(directory, archiveName) + ".zip";
                int archiveNumber = 0;
                while(File.Exists(archiveFile))
                {
                    string numberedArchiveFilename = Path.Combine(directory, archiveName) + $"({archiveNumber}).zip";
                    if (!File.Exists(numberedArchiveFilename)) archiveFile = numberedArchiveFilename;
                    archiveNumber++;
                }
                using (FileStream saveStream = new FileStream(archiveFile, FileMode.Create))
                {
                    using (ZipArchive archive = new ZipArchive(saveStream, ZipArchiveMode.Create, true))
                    {
                        foreach (string file in Directory.GetFiles(directory, $"*.{extension}"))
                        {
                            archive.CreateEntryFromFile(file, Path.GetFileName(file));
                        }
                    }
                }
                return File.Exists(archiveFile);
            }
            catch
            {
                throw;
            }
        }

        public static int ChangeAllExtensionsInDirectory(string directory, string oldExtension, string newExtension)
        {
            //returns the number of files changed
            int changed = 0;
            foreach (string file in Directory.GetFiles(directory, $"*.{oldExtension}"))
            {
                changed += ChangeExtension(new FileInfo(file), newExtension) ? 1 : 0;
            }
            return changed;
        }

        public static bool ChangeExtension(FileInfo file, string newExtension)
        {
            try
            {
                do { Thread.Sleep(10); } while (FileIsLocked(file));
                string destinationFile = Path.Combine(file.DirectoryName, Path.GetFileNameWithoutExtension(file.FullName)) + $".{newExtension}";
                bool overwrite = false;
                if (File.Exists(destinationFile))
                {
                    Console.WriteLine($"The file {Path.GetFileName(destinationFile)} already exists. Do you wish to overwrite it? This may result in a loss of data. It is advised that you do not. You should compare the individual files to determine which to keep.");
                    Console.WriteLine($"Enter Y to overwrite.");
                    Console.WriteLine(Fluff.Prompt);
                    string? response = Console.ReadLine();
                    overwrite = response.ToLower().StartsWith("y");
                    if (!overwrite) return false;
                }
                File.Move(file.FullName, destinationFile, overwrite);
                return File.Exists(destinationFile);
            }
            catch
            {
                throw;
            }
        }
        public static async Task<bool> LoadReleaseAssets(Release release)
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
                return true;
            }
            return false;
        }

        public static async Task<Release> GetRelease(string githubPath)
        {
            Client.DefaultRequestHeaders.Accept.Clear();
            Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            Client.DefaultRequestHeaders.Add("User-Agent", "Genie Client Updater");
            try
            {
                var streamTask = Client.GetStreamAsync(githubPath);

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

        public static async Task<bool> LaunchGenie()
        {
            if (File.Exists(@$"{LocalDirectory}\genie.exe"))
            {
                Console.WriteLine("[Launching Genie]");
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    FileInfo file = new FileInfo(@$"{LocalDirectory}\genie.exe");
                    do { Thread.Sleep(10); } while (FileIsLocked(file));
                    Process genie = Process.Start($"{LocalDirectory}\\genie.exe");
                    return genie != null;
                }
            }
            return false;
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
        
        public static void Move(string source, string destinationPath)
        {
            string filename = Path.GetFileName(source);
            try
            {
                File.Move(source, $@"{destinationPath}\{filename}", true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
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
