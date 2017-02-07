namespace DotNetty.Rpc.Protocol
{
    using System.Globalization;
    using System.IO;
    using System.Text;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class SerializationUtil
    {
        private static readonly JsonSerializerSettings DefaultJsonSerializerSetting = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto
        };

        public static string Serialize<T>(T obj)
        {
            JsonSerializer jsonSerializer = JsonSerializer.Create(DefaultJsonSerializerSetting);
            var stringWriter = new StringWriter(new StringBuilder(), CultureInfo.InvariantCulture);
            using (var jsonTextWriter = new JsonTextWriter(stringWriter))
            {
                jsonTextWriter.Formatting = jsonSerializer.Formatting;
                jsonSerializer.Serialize(jsonTextWriter, obj, typeof(T));
            }
            return stringWriter.ToString();
        }

        public static T Deserialize<T>(byte[] data)
        {
            string s = Encoding.UTF8.GetString(data);
            return JsonConvert.DeserializeObject<T>(s, DefaultJsonSerializerSetting);
        }

        public static JObject SafeDeserialize(byte[] data)
        {
            string s = Encoding.UTF8.GetString(data);
            return (JObject)JsonConvert.DeserializeObject(s);
        }
    }
}
