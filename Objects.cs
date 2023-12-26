using System.Text.Json.Serialization;
using System.Collections.Generic;
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
        public bool HasAssets { get; set; }
        public async Task LoadAssets()
        {
            HasAssets = await FileHandler.LoadReleaseAssets(this);
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
                return FileHandler.LocalDirectory + "\\" + Name;
            } 
        }
    }
}
