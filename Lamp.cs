﻿using System;
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
        string mapdir = $@"{FileHandler.LocalDirectory}/Maps";
        string configdir = $@"{FileHandler.LocalDirectory}/Config";
        string plugindir = $@"{FileHandler.LocalDirectory}/Plugins";
        Release latest = new Release();
        Release current = new Release();
        Release test = new Release();


        public Lamp(string[] args)
        {
            ProcessArgs(args);
            if (string.IsNullOrWhiteSpace(mapdir)) mapdir = $@"{FileHandler.GetDataDirectory(local)}/Maps";
            if (string.IsNullOrWhiteSpace(configdir)) configdir = $@"{FileHandler.GetDataDirectory(local)}";
            if (string.IsNullOrWhiteSpace(plugindir)) plugindir = $@"{FileHandler.GetDataDirectory(local)}/Plugins";
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
                        break;
                    case "--force":
                    case "--f":
                        force = true;
                        break;
                    case "--t":
                    case "--test":
                        loadTest = true;
                        break;
                    case "--l":
                    case "--local":
                        local = true;
                        break;
                    case "--m":
                    case "--map":
                    case "--maps":
                        string[] mapArgs = arg.Split('"');
                        if (mapArgs.Length > 1) mapdir = mapArgs[1];
                        updateMaps = true;
                        break;
                    case "--c":
                    case "--config":
                        string[] configArgs = arg.Split('"');
                        if (configArgs.Length > 1) configdir = configArgs[1];
                        updateConfig = true;
                        break;
                    case "--p":
                    case "--plugin":
                    case "--plugins":
                        string[] pluginArgs = arg.Split('"');
                        if (pluginArgs.Length > 1) configdir = pluginArgs[1];
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
        }
        private void ProcessConfigUpdates() 
        {
            if (!latest.Assets.ContainsKey(Paths.FileNames.Config))
            {
                Console.WriteLine("No Config Assets were found in the latest release.");
            }
            try
            {
                Asset configAsset = latest.Assets[Paths.FileNames.Config];
                string destination = $@"{configdir}\{Paths.FileNames.Config}";
                FileHandler.AcquirePackage(configAsset.DownloadURL, destination);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
        private void ProcessClientUpdates() 
        {
            if (loadTest) test.LoadAssets();
            UpdateClient();
        }
        private void ProcessPluginUpdates() 
        {
            if (!latest.Assets.ContainsKey(Paths.FileNames.Plugins))
            {
                Console.WriteLine("No Plugin Assets were found in the latest release.");
            }
            try
            {
                Asset configAsset = latest.Assets[Paths.FileNames.Config];
                string destination = $@"{configdir}\{Paths.FileNames.Config}";
                FileHandler.AcquirePackage(configAsset.DownloadURL, destination);
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
                Console.WriteLine(Fluff.Prompt);
                Console.WriteLine("Would you like to download the latest version?");
                Console.WriteLine($"Yes or Latest to download Genie {latest.Version}.");
                Console.WriteLine("Test to download the latest Test Release.");
                Console.WriteLine(Fluff.Prompt);
                string? response = Console.ReadLine();
                if (response.ToLower().StartsWith("y") || response.ToLower().StartsWith("latest"))
                {
                    Console.WriteLine(Fluff.Prompt);
                    force = true;
                    UpdateClient();
                }
                if (response.ToLower().StartsWith("test"))
                {
                    Console.WriteLine(Fluff.Prompt);
                    test = FileHandler.GetRelease(Paths.GitHub.LatestRelease).Result;
                    loadTest = true;
                    test.LoadAssets();
                    UpdateClient();
                }
                else
                {
                    if (current.Version != "0")
                    {
                        Console.WriteLine(Fluff.Exit);
                        FileHandler.LaunchGenie();
                    }
                }
            }
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
                    if (RuntimeInformation.FrameworkDescription.StartsWith(".NET 6") && assets.ContainsKey(Paths.FileNames.RuntimeDependent))
                    {
                        zipAsset = assets[Paths.FileNames.RuntimeDependent];
                    }
                    else
                    {
                        if (RuntimeInformation.OSArchitecture.ToString() == "X64" && assets.ContainsKey(Paths.FileNames.x64))
                        {
                            zipAsset = assets[Paths.FileNames.x64];
                        }
                        else if (assets.ContainsKey(Paths.FileNames.x86))
                        {
                            zipAsset = assets[Paths.FileNames.x86];
                        }
                        else
                        {
                            throw new FileNotFoundException("No valid package was found for your system.");
                        }
                    }
                    Console.WriteLine("Downloading latest client");
                    FileHandler.AcquirePackage(zipAsset.DownloadURL, zipAsset.LocalFilepath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }
    }
}