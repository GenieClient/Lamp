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
        bool updateScripts = false;
        string mapdir = string.Empty;
        string plugindir = string.Empty;
        string scriptdir = string.Empty;
        string scriptrepo = string.Empty;
        Release latest = new Release();
        Release current = new Release();
        Release test = new Release();


        public Lamp(string[] args)
        {
            ProcessArgs(args);
            if (string.IsNullOrWhiteSpace(mapdir)) mapdir = $@"{FileHandler.GetDataDirectory(local)}\Maps";
            if (string.IsNullOrWhiteSpace(plugindir)) plugindir = $@"{FileHandler.GetDataDirectory(local)}\Plugins";
            if (string.IsNullOrWhiteSpace(scriptdir)) scriptdir = $@"{FileHandler.GetDataDirectory(local)}\Scripts";
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

                    case "--background":
                    case "--bg":
                    case "--b":
                        auto = true;
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
                    case "--s":
                    case "--script":
                    case "--scripts":
                        string[] scriptArgs = arg.Split('|');
                        if (scriptArgs.Length > 2)
                        {
                            scriptdir = scriptArgs[1];
                            scriptrepo = scriptArgs[2];
                            updateScripts = true;
                        }
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
                if (updateScripts) ProcessScriptUpdates();
            }
            else
            {
                RunInteractive();
            }
            return true;
        }

        private void ProcessMapUpdates()
        {
            FileHandler.AcquirePackageInMemory(Paths.GitHub.MapRepositoryZip, mapdir);
        }

        private void ProcessScriptUpdates()
        {
            FileHandler.AcquirePackageInMemory(scriptrepo, scriptdir);
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
                FileHandler.AcquirePackageInMemory(configAsset.DownloadURL, destination);
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
                FileHandler.AcquirePackageInMemory(pluginsAsset.DownloadURL, plugindir);
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
                    if (response.Contains(" "))
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
                            Console.WriteLine("I wish to be an all powerful genie!");
                            Lamp.Rub();
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
                        case "transmogrify":
                            Transmogrify(arg);
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
            Console.WriteLine("TRANSMOGRIFY\tto convert all files of one type to another. Type TRANSMOGRIFY for syntax.");
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

        private void Transmogrify(string argString)
        {
            string[] args = argString.Split(" ");

            if (args.Length < 3)
            {
                Console.WriteLine("This method is used to convert files from one extension to another.");
                Console.WriteLine("It will create an archive in the target directory.");
                Console.WriteLine("Be advised that this can be dangerous as it will operate on any extension.");
                Console.WriteLine("PATH does not need to be enclosed in quotes.");
                Console.WriteLine("Syntax: TRANSMOGRIFY {PATH} {OLD EXTENSION} {NEW EXTENSION}");
                return;
            }
            string path = CombineTransmogrifyPath(args);
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"The indicated directory was not found: {path}");
                return;
            }
            string oldExtension = args[args.Length - 2];
            string newExtension = args[args.Length - 1];
            if (oldExtension.ToLower() == newExtension.ToLower())
            {
                Console.WriteLine($"The old extension, {oldExtension}, is the same as the new new extension, {newExtension}");
                Console.WriteLine($"If your intent is to change capitalization, this is not supported due to language constraints.");
                Console.WriteLine($"If you would like to change that, feel free to contribute a new renaming method to this Open Source project.");
                return;
            }
            if(!ConfirmTransmogrify(path, newExtension, oldExtension)) return;
            string step = "Initializing";
            try
            {
                step = "archiving the directory";
                if (!FileHandler.ArchiveExtensions(path, "Lamp Generated Archive", oldExtension))
                {
                    Console.WriteLine($"Lamp was unable to archive the directory {path}");
                    Console.WriteLine($"Process aborted.");
                    return;
                }
                step = "renaming files";
                int filesRenamed = FileHandler.ChangeAllExtensionsInDirectory(path, oldExtension, newExtension);
                Console.WriteLine($"Transmogrify completed. {filesRenamed} files were changed.");
                Console.WriteLine($"An archive of the previous files has been created in {path}");
                Console.WriteLine($"Please manually verify success before deleting the Lamp Generated Archive");
                return;
            } catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while {step}");
                Console.WriteLine(ex.Message);
            }
        }
        private bool ConfirmTransmogrify(string path, string newExtension, string oldExtension)
        {
            Console.WriteLine($"This command will change the extension of for all files of type {oldExtension} to {newExtension} in the directory {path}.");
            Console.WriteLine($"Used incorrectly this can and will destroy your files. Please be careful.");
            Console.WriteLine($"Lamp will create an archive, but it is strongly recommended you back up your files yourself, separately, as well.");
            Console.WriteLine($"Please confirm that you have entered the correct path, old extension, and new by reentering the full command.");
            try
            {
                Console.WriteLine(Fluff.Prompt);
                string? response = Console.ReadLine();
                string inputPath = CombineTransmogrifyPath(response.Substring(response.IndexOf(" ")).Trim().Split(' '));
                string[] args = response.Split(' ');
                string inputOldExtension = args[args.Length - 2];
                string inputNewExtension = args[args.Length - 1];
                if(args[0].ToLower() != "transmogrify" || inputPath.ToLower() != path.ToLower() || inputOldExtension.ToLower() != oldExtension.ToLower() || inputNewExtension.ToLower() != newExtension.ToLower())
                {
                    throw new Exception();
                }

                Console.WriteLine($"Checks out. Please double-down and confirm you understand by entering Y or Yes");
                response = null;
                Console.WriteLine(Fluff.Prompt);
                response = Console.ReadLine();
                if(response.ToLower().Trim() != "y" && response.ToLower().Trim() != "yes")
                {
                    throw new Exception();
                }
                return true;
            }
            catch
            {
                Console.WriteLine("Command could not be confirmed. Aborting.");
                return false;
            }
        }

        private string CombineTransmogrifyPath(string[] args)
        {
            string path = args[0];
            if(args.Length > 3) 
            {
                //more than the needed number of args were supplied. 
                //this probably means a path string which contains a space
                //so in such a case let's stitch all but the last 2 together
                //back into the path
                for(int i = 1; i<args.Length - 2; i++)
                {
                    path += $" {args[i]}";
                }
            }
            return path;
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
                    FileHandler.AcquirePackageInMemory(zipAsset.DownloadURL, zipAsset.LocalFilepath);
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
