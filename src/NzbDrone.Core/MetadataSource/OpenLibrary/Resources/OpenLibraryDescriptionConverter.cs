using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NzbDrone.Core.MetadataSource.OpenLibrary.Resources
{
    // OL returns `description` (and `bio`) as one of:
    //   "plain string"
    //   {"type": "/type/text", "value": "string"}
    //   missing / null
    //
    // Without this converter, the bare-string form (~half of real responses)
    // fails deserialization. Apply via [JsonConverter(typeof(OpenLibraryDescriptionConverter))]
    // on string properties.
    public class OpenLibraryDescriptionConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(string);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonToken.String)
            {
                return (string)reader.Value;
            }

            if (reader.TokenType == JsonToken.StartObject)
            {
                var obj = JObject.Load(reader);
                return obj["value"]?.ToString();
            }

            // Unknown shape — skip the token so the rest of the document parses.
            JToken.Load(reader);
            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // We never serialize OL DTOs back out; no-op is safe.
            writer.WriteValue(value as string);
        }
    }
}
