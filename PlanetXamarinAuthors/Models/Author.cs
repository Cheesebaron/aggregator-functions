using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PlanetXamarinAuthors.Models
{
    public record Author(
        [JsonProperty("firstName", Required = Required.DisallowNull)]
        string FirstName,
        [JsonProperty("lastName", Required = Required.DisallowNull)]
        string LastName,
        [JsonProperty("stateOrRegion", Required = Required.DisallowNull)]
        string StateOrRegion,
        [EmailAddress]
        [JsonProperty("emailAddress", Required = Required.Always)]
        string EmailAddress,
        [JsonProperty("tagOrBio", Required = Required.DisallowNull)]
        string ShortBioOrTagLine,
        [Url]
        [JsonProperty("webSite", Required = Required.Always)]
        Uri WebSite,
        [JsonProperty("twitterHandle", Required = Required.DisallowNull)]
        string TwitterHandle,
        [JsonProperty("githubHandle", Required = Required.Always)]
        string GitHubHandle,
        [JsonProperty("gravatarHash", Required = Required.DisallowNull)]
        string GravatarHash,
        [JsonProperty("feedUris", Required = Required.Always)]
        IEnumerable<Uri> FeedUris,
        [JsonProperty("position", Required = Required.DisallowNull)]
        GeoPosition Position,

        // In ISO 639-1, lowercase, 2 letters
        // https://en.wikipedia.org/wiki/List_of_ISO_639-1_codes
        [JsonProperty("languageCode", Required = Required.Always)]
        string FeedLanguageCode
    );
}
