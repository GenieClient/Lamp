using System;
using System.IO;

namespace Lamp
{
    public static class Paths
    {
        public static class GitHub
        {
            public const string LatestRelease = @"https://api.github.com/repos/GenieClient/Genie4/releases/latest";
            public const string TestRelease = @"https://api.github.com/repos/GenieClient/Genie4/releases/tags/Test_Build";
            public const string MapRepositoryZip = @"https://github.com/GenieClient/Maps/archive/refs/heads/main.zip";
            public const string PluginRepositoryZip = @"https://github.com/GenieClient/Plugins/archive/refs/heads/main.zip";
        }

        public static class FileNames
        {
            //these are the file names that will be published to GitHub
            public const string RuntimeDependent = "Genie4-x64-Runtime-Dependent.zip";
            public const string x86 = "Genie4-x86.zip";
            public const string x64 = "Genie4-x64.zip";
            public const string Client = "Genie4.zip";
            public const string Plugins = "Plugins.zip";
            public const string Config = "Base.Config.Files.zip";
        }

        public static class Genie
        {
            public static readonly string Local = AppDomain.CurrentDomain.BaseDirectory;
            public static readonly string Config = Path.Combine(Local, "Config");
            public static readonly string Settings = Path.Combine(Config, "settings.cfg");
        }
    }
}
