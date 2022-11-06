using Newtonsoft.Json;

namespace PlanetXamarinAuthors.Models
{
    public record struct GeoPosition(
        [JsonProperty("lat", Required = Required.Always)]
        double Latitude,
        [JsonProperty("lon", Required = Required.Always)]
        double Longitude)
    {
        public static GeoPosition Empty = new(-1337, 42);
    }
}
