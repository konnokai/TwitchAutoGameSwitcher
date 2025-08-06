using Newtonsoft.Json;

namespace TwitchAutoGameSwitcher.NxApi.Model
{
    public class TitleJson
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("image_url")]
        public string ImageUrl { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("since")]
        public DateTime Since { get; set; }
    }
}
