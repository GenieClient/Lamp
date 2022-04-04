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
        static async Task Main(string[] args)
        {

            bool finished = false;
            finished = new Lamp(args).Execute();

            while(!finished)
            {
            
            }
            Lamp.Rub();
            Environment.Exit(0);
        }

        
        

        



        
    }
}