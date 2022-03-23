using System;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace Lamp
{
    public class Release
    {
        [JsonPropertyName("tag_name")]
        public string Version { get; set; }
        [JsonPropertyName("html_url")]
        public string ReleaseURL { get; set; }
        [JsonPropertyName("assets_url")]
        public string AssetsURL { get; set; }
        public Dictionary<string, Asset> Assets { get; set; }
        public void LoadAssets()
        {
            if (!string.IsNullOrWhiteSpace(AssetsURL))
            {
                Assets = new Dictionary<string, Asset>();
                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
                client.DefaultRequestHeaders.Add("User-Agent", "Genie Client Updater");

                var streamTask = client.GetStreamAsync(AssetsURL).Result;
                List<Asset> latestAssets = JsonSerializer.Deserialize<List<Asset>>(streamTask);
                foreach(Asset asset in latestAssets)
                {
                    Assets.Add(asset.Name, asset);
                }
            }
        }

    }
    public class Asset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("browser_download_url")]
        public string DownloadURL { get; set; }
        public string LocalFilepath { 
            get
            {
                return Environment.CurrentDirectory + "\\" + Name;
            } 
        }
    }
}
