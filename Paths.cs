﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lamp
{
    public static class Paths
    {
        public static class GitHub
        {
            public const string LatestRelease = @"https://api.github.com/repos/GenieClient/Genie4/releases/latest";
        }

        public static class FileNames
        {
            //these are the file names that will be published to GitHub
            public const string RuntimeDependent = "Genie4-x64-Runtime-Dependent.zip";
            public const string x86 = "Genie4-x86.zip";
            public const string x64 = "Genie4-x64.zip";
            public const string Plugins = "Plugins.zip";
            public const string Config = "Base.Config.Files.zip";
        }
    }
}
