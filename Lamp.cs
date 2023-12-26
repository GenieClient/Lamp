using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Reflection;

namespace Lamp
{
    internal class Lamp
    {
        public static string GenieLegacyName = "Genie Client 3";
        public static string GenieProductName = "Genie Client 4";

        bool operating = true;
        bool auto = true;
        bool force = false;
        bool loadTest = false;
        bool local = false;
        bool updateClient = false;
        bool updateMaps = false;
        bool updateConfig = false;
        bool updatePlugins = true;
        bool updateScripts = false;
        bool updateArt = false;
        bool updateMapScripts = false;
        string scriptdir = string.Empty;
        string mapdir = string.Empty;
        string plugindir = string.Empty;
        string artdir = string.Empty;
        string sounddir = string.Empty;
        string maprepo = Paths.GitHub.MapRepositoryZip;
        string pluginrepo = Paths.GitHub.PluginRepositoryZip;
        string artrepo = string.Empty;
        string scriptrepo = string.Empty;
        string scriptExtension = "cmd";
        Release latest = new Release();
        Release current = new Release();
        Release test = new Release();


        public Lamp(string[] args)
        {
            LoadConfig();
            ProcessArgs(args);
            if (string.IsNullOrWhiteSpace(mapdir)) mapdir = $@"{FileHandler.GetDataDirectory(local)}\Maps";
            if (string.IsNullOrWhiteSpace(plugindir)) plugindir = $@"{FileHandler.GetDataDirectory(local)}\Plugins";
            if (string.IsNullOrWhiteSpace(scriptdir)) scriptdir = $@"{FileHandler.GetDataDirectory(local)}\Scripts";
        }

        Regex ConfigPattern = new Regex(@"\#config \{(\w*)\}(?: |)\{(.*)\}");
        private void LoadConfig()
        {
            if (File.Exists(Paths.Genie.Settings))
            {
                using (StreamReader reader = new StreamReader(Paths.Genie.Settings))
                {
                    while(!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        Match config = ConfigPattern.Match(line);
                        if(config.Success)
                        {
                            string value = config.Groups[2].Value;
                            switch (config.Groups[1].Value.ToLower())
                            {
                                case "scriptdir":
                                    scriptdir = value;
                                    break;

                                case "mapdir":
                                    mapdir = value;
                                    break;

                                case "plugindir":
                                    plugindir = value;
                                    break;

                                case "artdir":
                                    artdir = value;
                                    break;

                                case "sounddir":
                                    sounddir = value;
                                    break;

                                case "maprepo":
                                    maprepo = value;
                                    break;

                                case "pluginrepo":
                                    pluginrepo = value;
                                    break;

                                case "artrepo":
                                    artrepo = value;
                                    break;

                                case "scriptrepo":
                                    scriptrepo = value;
                                    break;

                                case "scriptextension":
                                    scriptExtension = value;
                                    break;

                                case "updatemapscripts":
                                    updateMapScripts = value.ToLower() == "true";
                                    break;

                                default:
                                    break;
                            }
                        }
                    }
                }
            }
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
                    case "--art":
                    case "--images":
                        string[] artArgs = arg.Split('|');
                        if (artArgs.Length > 2)
                        {
                            artdir = artArgs[1];
                            artrepo = artArgs[2];
                            updateArt = true;
                        }
                        break;
                    case "--ms":
                    case "--mapscripts":
                        updateMapScripts = true;
                        break;
                    default:
                        break;
                }
            }
        }

        public async Task<bool> Execute()
        {
            Console.Write("Checking Versions");
            current = await FileHandler.GetCurrentVersion();
            Console.Write(".");
            latest = await FileHandler.GetRelease(Paths.GitHub.LatestRelease);
            Console.Write(".");
            test = await FileHandler.GetRelease(Paths.GitHub.TestRelease);
            Console.Write(".");
            bool success = true;
            if (auto)
            {
                if (updatePlugins || updateClient || updateConfig)
                {
                    if (updatePlugins) await ProcessPluginUpdates();
                    if (updateConfig) await ProcessConfigUpdates();
                    if (updateClient)
                    {
                        success = await ProcessClientUpdates();
                        
                        if (success) success = await FileHandler.LaunchGenie();
                    }
                }
                if (updateMaps) await ProcessMapUpdates();
                if (updateScripts) await ProcessScriptUpdates();
                if (updateArt) await ProcessArtUpdates();
            }
            else
            {
                RunInteractive();
            }
            return success;
        }

        private async Task<bool> ProcessMapUpdates()
        {
            bool success = await FileHandler.AcquirePackageInMemory(Paths.GitHub.MapRepositoryZip, mapdir);
            if (success && updateMapScripts 
                && Directory.Exists(Path.Combine(mapdir, @"Copy These to Genie's Scripts Folder")))
            {
                success = await ProcessMapScriptUpdates();
            }
            return success;
        }

        private async Task<bool> ProcessMapScriptUpdates()
        {
            try
            {
                foreach (string file in Directory.GetFiles(Path.Combine(mapdir, @"Copy These to Genie's Scripts Folder")))
                {
                    string targetFile = Path.Combine(scriptdir, $"{Path.GetFileNameWithoutExtension(file)}.{scriptExtension}");
                    if (File.Exists(targetFile)) File.Delete(targetFile);
                    File.Move(file, targetFile);
                }
                return true;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message + Environment.NewLine + ex.StackTrace);
                return false;
            }
        }

        private async Task<bool> ProcessScriptUpdates()
        {
            return await FileHandler.AcquirePackageInMemory(scriptrepo, scriptdir);
        }

        private async Task<bool> ProcessArtUpdates()
        {
            return await FileHandler.AcquirePackageInMemory(artrepo, artdir);
        }
        private async Task<bool> ProcessPluginUpdates()
        {
            return await FileHandler.AcquirePackageInMemory(Paths.GitHub.PluginRepositoryZip, plugindir);
        }

        private async Task<bool> ProcessConfigUpdates()
        {
            if (string.IsNullOrWhiteSpace(latest.Version)) latest = FileHandler.GetRelease(Paths.GitHub.LatestRelease).Result;
            if (!latest.HasAssets) await latest.LoadAssets();
            if (!latest.Assets.ContainsKey(Paths.FileNames.Config))
            {
                Console.WriteLine("No Config Assets were found in the latest release.");
                return false;
            }
            else try
            {
                Asset configAsset = latest.Assets[Paths.FileNames.Config];
                string destination = FileHandler.GetDataDirectory(local) + Paths.FileNames.Config;
                return await FileHandler.AcquirePackageInMemory(configAsset.DownloadURL, destination);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return false;
            }
        }
        private async Task<bool> ProcessClientUpdates()
        {
            if (loadTest)
            {
                test = FileHandler.GetRelease(Paths.GitHub.TestRelease).Result;
                await test.LoadAssets();
            }
            else
            {
                latest = FileHandler.GetRelease(Paths.GitHub.LatestRelease).Result;
                await latest.LoadAssets();
            }
            return await UpdateClient();
        }

        public static async Task<bool> Rub()
        {
            return await FileHandler.LaunchGenie();
        }

        private async void RunInteractive()
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
                            if(await UpdateClient())
                            {
                                Console.WriteLine("The feather thrums with power, emitting a warm glow. The glow suddenly intensifies as a loud noise echoes from the walls around you and you find yourself drawn out of the Lamp...");
                                
                                interacting = !(await Rub());
                            }
                            break;

                        case "test":
                            loadTest = true;
                            if (await UpdateClient())
                            {
                                Console.WriteLine("The feather thrums with power, emitting a warm glow. The glow suddenly intensifies as a loud noise echoes from the walls around you and you find yourself drawn out of the Lamp...");
                                interacting = !(await Rub());
                            }
                            break;

                        case "maps":
                            if (!string.IsNullOrWhiteSpace(arg)) mapdir = arg;
                            Console.WriteLine("Sparks of ruby energy shoot from the pedestal into the feather, imbuing it with strength.");
                            if (await ProcessMapUpdates()) Console.WriteLine($"The feather seems to absorb the shimmering light, which resolves into a weblike pattern as it fades. Maps have been updated in {mapdir}");
                            else Console.WriteLine($"The ruby light explodes violently outward in a jarring wave of portent that leaves you wondering. Something seems to have gone wrong and Maps have not been updated.");
                            break;
                        case "config":
                            Console.WriteLine("Streams of verdant energy drift from the pedestal into the feather, reinforcing its base.");
                            if (await ProcessConfigUpdates()) Console.WriteLine($"Base Config has been deployed to {FileHandler.GetDataDirectory(local)}.");
                            else Console.WriteLine($"The verdent lines seem to wither and fade. Something seems to have gone wrong and Base Config has not been updated.");
                            break;
                        case "plugins":
                            if (!string.IsNullOrWhiteSpace(arg)) plugindir = arg;
                            Console.WriteLine("Beams of violet light flicker around the feather, imbuing it with strange power.");
                            if(await ProcessPluginUpdates()) Console.WriteLine($"Plugins have been updated in {plugindir}.\r\nBe advised that the only plugins that are downloaded are ones with known fixes for\r\nknown compatibility issues with Genie 4.");
                            else Console.WriteLine($"The violet light shimmers menacingly before abruptly vanishing. Something seems to have gone wrong and Plugins have not been updated.");
                            break;

                        case "art":
                            if (string.IsNullOrWhiteSpace(artrepo) || !artrepo.EndsWith(".zip")) Console.WriteLine("Motes of azure light flicker weakly around the feather. You realize that you have no hope of updating the Art files without an ARTREPO set.\r\n");
                            if (!string.IsNullOrWhiteSpace(arg)) artdir = arg;
                            else if (!string.IsNullOrWhiteSpace(artdir))
                            Console.WriteLine("Trails of azure light spiral around the feather, imbuing it with beautiful power.");
                            if (await ProcessArtUpdates()) Console.WriteLine($"Art has been updated in {artdir}.\r\n");
                            else Console.WriteLine("Waves of teleologic energy pulse from the feather, dispersing the azure light. Something seems to have gone wrong. Art has not been updated.");
                            break;
                        case "jafar":
                            Console.WriteLine("I wish to be an all powerful genie!");
                            interacting = !(await Lamp.Rub());
                            break;
                        case "genie":
                            interacting = !(await Lamp.Rub());
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

        private void WriteDirectories()
        {
            Console.WriteLine("You take a moment to recall your current directories are...");
            Console.WriteLine($"\tMAPS\t{mapdir}");
            Console.WriteLine($"\tPLUGINS\t{plugindir}");
            Console.WriteLine($"\tART\t{artdir}\r\n");
            Console.WriteLine("You take a moment to recall your current repositories are...");
            Console.WriteLine($"\tMAPS\t{maprepo}");
            Console.WriteLine($"\tPLUGINS\t{pluginrepo}");
            Console.WriteLine($"\tART*\t{plugindir}");
            Console.WriteLine($"\t\t*This feature is currently under construction.\r\n\t\tART should work but you will need to manually add the ARTREPO Config to your settings.cfg.\r\n\tAll repositories should point to a zip file.");
        }
        private void WriteInstructions()
        {
            Console.WriteLine(Fluff.Prompt);
            Console.WriteLine(Fluff.Instructions);
            Console.WriteLine($"LATEST\tto download Genie {latest.Version}.");
            Console.WriteLine("TEST\tto download the latest Test Release.");
            Console.WriteLine("CONFIG\tto download a basic configuration.");
            Console.WriteLine("TRANSMOGRIFY\tto convert all files of one type to another. Type TRANSMOGRIFY for syntax.");
            Console.WriteLine("MAPS\tto download the latest Maps. \r\n\tIt occurs to you that you could also specify a directory by quoting it.");
            Console.WriteLine("\t\tex: MAPS \"C:\\Genie\\Maps");
            Console.WriteLine("PLUGINS\tto download the latest Plugins. \r\n\tIt occurs to you that you could also specify a directory by quoting it.");
            Console.WriteLine("\t\t-ex: PLUGINS \"C:\\Genie\\Plugins");
            Console.WriteLine("GENIE\tto exit the Lamp and launch Genie.");
            Console.WriteLine("EXIT\tto exit the Lamp wthout launching Genie.\r\n\r\n");
            WriteDirectories();
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
        private async Task<bool> UpdateClient()
        {

            if (!loadTest && !force && current.Version == latest.Version)
            {
                Console.WriteLine("This instance of Genie is using the latest release.");
                return false;
            }
            else
            {
                try
                {
                    Release release = loadTest ? test : latest;
                    await release.LoadAssets();
                    Dictionary<string, Asset> assets = release.Assets;
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
                    
                    if(await FileHandler.AcquirePackageInMemory(zipAsset.DownloadURL, Path.GetDirectoryName(zipAsset.LocalFilepath)))
                    {
                        return await Lamp.Rub();
                    }
                    else
                    {
                        return false;
                    }
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return false;
                }
            }
        }
    }
}
