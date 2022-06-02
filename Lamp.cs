using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using System.IO;

namespace Lamp
{
    internal class Lamp
    {
        public static string GenieLegacyName = "Genie Client 3";
        public static string GenieProductName = "Genie Client 4";

        bool operating = true;
        bool auto = false;
        bool force = false;
        bool loadTest = false;
        bool local = false;
        bool updateClient = false;
        bool updateMaps = false;
        bool updateConfig = false;
        bool updatePlugins = false;
        string mapdir = string.Empty;
        string plugindir = string.Empty;
        Release latest = new Release();
        Release current = new Release();
        Release test = new Release();


        public Lamp(string[] args)
        {
            ProcessArgs(args);
            if (string.IsNullOrWhiteSpace(mapdir)) mapdir = $@"{FileHandler.GetDataDirectory(local)}\Maps";
            if (string.IsNullOrWhiteSpace(plugindir)) plugindir = $@"{FileHandler.GetDataDirectory(local)}\Plugins";
        }

        private void ProcessArgs(string[] args)
        {
            foreach (string arg in args)
            {

                switch (arg.Split('|')[0].ToLower())
                {
                    case "--automated":
                    case "--auto":
                    case "--a":
                        auto = true;
                        updateClient = true;
                        break;
                    case "--force":
                    case "--f":
                        force = true;
                        updateClient = true;
                        break;
                    case "--t":
                    case "--test":
                        updateClient = true;
                        loadTest = true;
                        break;
                    case "--l":
                    case "--local":
                        local = true;
                        break;
                    case "--m":
                    case "--map":
                    case "--maps":
                        string[] mapArgs = arg.Split('|');
                        if (mapArgs.Length > 1) mapdir = mapArgs[1];
                        updateMaps = true;
                        break;
                    case "--c":
                    case "--config":
                        updateConfig = true;
                        break;
                    case "--p":
                    case "--plugin":
                    case "--plugins":
                        string[] pluginArgs = arg.Split('|');
                        if (pluginArgs.Length > 1) plugindir = pluginArgs[1];
                        updatePlugins = true;
                        break;

                    default:
                        break;
                }
            }
        }

        public bool Execute()
        {
            if (auto)
            {
                if (updatePlugins || updateClient || updateConfig)
                {
                    current = FileHandler.GetCurrentVersion().Result;
                    latest = FileHandler.GetRelease(Paths.GitHub.LatestRelease).Result;
                    test = FileHandler.GetRelease(Paths.GitHub.TestRelease).Result;
                    latest.LoadAssets();
                    if (updateClient) ProcessClientUpdates();
                    if (updatePlugins) ProcessPluginUpdates();
                    if (updateConfig) ProcessConfigUpdates();
                }
                if (updateMaps) ProcessMapUpdates();
            }
            else
            {
                RunInteractive();
            }
            return true;
        }

        private void ProcessMapUpdates() 
        {
            string destination = $@"{mapdir}\{Path.GetFileName(Paths.GitHub.MapRepositoryZip)}";
            FileHandler.AcquirePackage(Paths.GitHub.MapRepositoryZip, destination);
            foreach (string file in Directory.GetFiles($@"{mapdir}\Maps-main", "*.xml", SearchOption.AllDirectories))
            {
                FileHandler.Move(file, mapdir);
            }
            Directory.Delete($@"{mapdir}\Maps-main",  true);
        }
        private void ProcessConfigUpdates() 
        {
            if (string.IsNullOrWhiteSpace(latest.Version)) latest = FileHandler.GetRelease(Paths.GitHub.LatestRelease).Result;
            if (!latest.HasAssets) latest.LoadAssets();
            if (!latest.Assets.ContainsKey(Paths.FileNames.Config))
            {
                Console.WriteLine("No Config Assets were found in the latest release.");
            }
            try
            {
                Asset configAsset = latest.Assets[Paths.FileNames.Config];
                string destination = FileHandler.GetDataDirectory(local) + Paths.FileNames.Config;
                FileHandler.AcquirePackage(configAsset.DownloadURL, destination);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
        private void ProcessClientUpdates() 
        {
            if (loadTest)
            {
                if (string.IsNullOrWhiteSpace(test.Version)) test = FileHandler.GetRelease(Paths.GitHub.TestRelease).Result;
                if (!test.HasAssets) test.LoadAssets();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(latest.Version)) latest = FileHandler.GetRelease(Paths.GitHub.LatestRelease).Result;
                if (!latest.HasAssets) latest.LoadAssets();
            }
            UpdateClient();
        }
        private void ProcessPluginUpdates() 
        {
            if (string.IsNullOrWhiteSpace(latest.Version)) latest = FileHandler.GetRelease(Paths.GitHub.LatestRelease).Result;
            if (!latest.HasAssets) latest.LoadAssets();
            if (!latest.Assets.ContainsKey(Paths.FileNames.Plugins))
            {
                Console.WriteLine("No Plugin Assets were found in the latest release.");
            }
            try
            {
                Asset pluginsAsset = latest.Assets[Paths.FileNames.Plugins];
                string destination = $@"{plugindir}\{Paths.FileNames.Plugins}";
                FileHandler.AcquirePackage(pluginsAsset.DownloadURL, destination);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        public static void Rub()
        {
            FileHandler.LaunchGenie();
        }

        private void RunInteractive()
        {
            Console.WriteLine(Fluff.RoomName);
            Console.WriteLine(Fluff.RoomDescription);
            Console.WriteLine(Fluff.ObviousExits);
            Console.WriteLine(Fluff.Prompt);
            Console.Write(Fluff.Examine);
            Release current = FileHandler.GetCurrentVersion().Result;
            Console.Write(current.Version);
            Console.WriteLine(Fluff.Prompt);
            Console.Write(Fluff.Server);
            Release latest = FileHandler.GetRelease(Paths.GitHub.LatestRelease).Result;
            if (latest.Version == "404")
            {
                Console.Write(latest.AssetsURL);
                Console.WriteLine(Fluff.Prompt);
            }
            else
            {
                Console.Write(latest.Version);
                WriteInstructions();

                bool interacting = true;
                while (interacting)
                {
                    Console.WriteLine(Fluff.Prompt);
                    string? response = Console.ReadLine();
                    string arg = string.Empty;
                    if(response.Contains(" "))
                    {
                        arg = response.Substring(response.IndexOf(" ")).Trim();
                        response = response.Split(' ')[0];
                    }

                    switch (response.ToLower())
                    {
                        case "latest":
                            loadTest = false;
                            force = true;
                            UpdateClient();
                            Console.WriteLine("The feather thrums with power, emitting a warm glow.");
                            break;

                        case "local":
                            local = true;
                            mapdir = $@"{FileHandler.GetDataDirectory(local)}\Maps";
                            plugindir = $@"{FileHandler.GetDataDirectory(local)}\Plugins";
                            Console.WriteLine("\tYou take a moment to recall your current directories are...");
                            Console.WriteLine($"\tMAPS\t{mapdir}");
                            Console.WriteLine($"\tPLUGINS\t{plugindir}");
                            break;

                        case "appdata":
                            local = false;
                            mapdir = $@"{FileHandler.GetDataDirectory(local)}\Maps";
                            plugindir = $@"{FileHandler.GetDataDirectory(local)}\Plugins";
                            Console.WriteLine("\tYou take a moment to recall your current directories are...");
                            Console.WriteLine($"\tMAPS\t{mapdir}");
                            Console.WriteLine($"\tPLUGINS\t{plugindir}");
                            break;

                        case "test":
                            loadTest = true;
                            test = FileHandler.GetRelease(Paths.GitHub.TestRelease).Result;
                            test.LoadAssets();
                            UpdateClient();
                            Console.WriteLine("The feather thrums with latent power, its silvery geometric patterns glittering.");
                            break;

                        case "maps":
                            if (!string.IsNullOrWhiteSpace(arg)) mapdir = arg;
                            ProcessMapUpdates();
                            Console.WriteLine($"Maps have been updated in {mapdir}");
                            break;
                        case "config":
                            ProcessConfigUpdates();
                            Console.WriteLine("Sparks of violet energy shoot from the pedestal into the feather, imbuing it with strength.");
                            Console.WriteLine($"Base Config has been deployed to {FileHandler.GetDataDirectory(local)}.");
                            break;
                        case "plugins":
                            if (!string.IsNullOrWhiteSpace(arg)) plugindir = arg;
                            ProcessPluginUpdates();
                            Console.WriteLine("Beams of violet light flicker around the feather, imbuing it with strange power.");
                            Console.WriteLine($"Plugins have been update in {plugindir}.\r\nBe advised that the only plugins that are downloaded are ones with known fixes for\r\nknown compatibility issues with Genie 4.");
                            break;
                        case "jafar":
                            Lamp.Rub();
                            Console.WriteLine("I wish to be an all powerful genie!");
                            interacting = false;
                            break;
                        case "genie":
                            Lamp.Rub();
                            interacting = false;
                            break;
                        case "exit":
                            interacting = false;
                            break;
                        case "?":
                        case "help":
                            WriteInstructions();
                            break;
                        default:
                            Console.WriteLine("I did not understand that. Please enter the command again. You can also enter \"?\" or \"HELP\" to repeat the list of commands.");
                            break;

                    }
                }
                Console.WriteLine(Fluff.Exit);
            }
        }

        private void WriteInstructions()
        {
            Console.WriteLine(Fluff.Prompt);
            Console.WriteLine(Fluff.Instructions);
            Console.WriteLine($"LATEST\tto download Genie {latest.Version}.");
            Console.WriteLine("TEST\tto download the latest Test Release.");
            Console.WriteLine("LOCAL\tto set your data directory to the client directory.");
            Console.WriteLine("APPDATA\tto set your data directory to the app data folder.");
            Console.WriteLine("CONFIG\tto download a basic configuration.");
            Console.WriteLine("MAPS\tto download the latest Maps. \r\n\tIt occurs to you that you could also specify a directory by quoting it.");
            Console.WriteLine("\t\tex: MAPS \"C:\\Genie\\Maps");
            Console.WriteLine("PLUGINS\tto download the latest Plugins. \r\n\tIt occurs to you that you could also specify a directory by quoting it.");
            Console.WriteLine("\t\t-ex: PLUGINS \"C:\\Genie\\Plugins");
            Console.WriteLine("GENIE\tto exit the Lamp and launch Genie.");
            Console.WriteLine("EXIT\tto exit the Lamp wthout launching Genie.");
            Console.WriteLine("\r\nYou take a moment to recall your current directories are...");
            Console.WriteLine($"\tMAPS\t{mapdir}");
            Console.WriteLine($"\tPLUGINS\t{plugindir}");
        }
        private void UpdateClient()
        {

            if (!loadTest && !force && current.Version == latest.Version)
            {
                Console.WriteLine("This instance of Genie is using the latest release.");
            }
            else
            {
                try
                {
                    Dictionary<string, Asset> assets = loadTest ? test.Assets : latest.Assets;
                    Asset zipAsset = new Asset() { Name = "Invalid" };
                    if (assets.ContainsKey(Paths.FileNames.Client)) 
                    {
                        zipAsset = assets[Paths.FileNames.Client];
                    }
                    else if(assets.ContainsKey(Paths.FileNames.x64))
                    {
                        zipAsset = assets[Paths.FileNames.x64];
                    }
                    Console.WriteLine("Downloading latest client");
                    FileHandler.AcquirePackage(zipAsset.DownloadURL, zipAsset.LocalFilepath);
                    Lamp.Rub();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }
    }
}
