using Newtonsoft.Json;

namespace TwitchAutoGameSwitcher.NxApi.Model
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class Game
    {
    }

    public class Presence
    {
        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("updatedAt")]
        public int UpdatedAt { get; set; }

        [JsonProperty("logoutAt")]
        public int LogoutAt { get; set; }

        [JsonProperty("game")]
        public Game Game { get; set; }
    }

    public class FriendJson
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("nsaId")]
        public string NsaId { get; set; }

        [JsonProperty("imageUri")]
        public string ImageUri { get; set; }

        [JsonProperty("image2Uri")]
        public string Image2Uri { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("isFriend")]
        public bool IsFriend { get; set; }

        [JsonProperty("isFavoriteFriend")]
        public bool IsFavoriteFriend { get; set; }

        [JsonProperty("isServiceUser")]
        public bool IsServiceUser { get; set; }

        [JsonProperty("isNew")]
        public bool IsNew { get; set; }

        [JsonProperty("isOnlineNotificationEnabled")]
        public bool IsOnlineNotificationEnabled { get; set; }

        [JsonProperty("friendCreatedAt")]
        public int FriendCreatedAt { get; set; }

        [JsonProperty("route")]
        public Route Route { get; set; }

        [JsonProperty("presence")]
        public Presence Presence { get; set; }
    }

    public class Route
    {
        [JsonProperty("appName")]
        public string AppName { get; set; }

        [JsonProperty("userName")]
        public string UserName { get; set; }

        [JsonProperty("shopUri")]
        public string ShopUri { get; set; }

        [JsonProperty("imageUri")]
        public string ImageUri { get; set; }

        [JsonProperty("channel")]
        public string Channel { get; set; }
    }
}
