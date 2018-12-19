using Newtonsoft.Json;

namespace SimpleRadio.Items
{
    public class Song
    {
        /// <summary>
        /// File to load up.
        /// </summary>
        [JsonProperty("file")]
        public string File { get; set; }
    }
}
