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
            try
            {
                finished = await new Lamp(args).Execute();
            }
            catch(Exception ex) 
            {
                Console.WriteLine(ex.ToString());
            }

            while(!finished)
            {
                await Task.Delay(1000);
            }
            Environment.Exit(0);
        }
    }
}