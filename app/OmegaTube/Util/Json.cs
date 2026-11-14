using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OmegaTube.Util
{
    // Thin wrapper over .NET 8 System.Text.Json exposing a uniform untyped object graph:
    // objects as Dictionary<string, object>, arrays as List<object>, numbers as double, plus string/bool/null.
    public static class Json
    {
        private static readonly JsonSerializerOptions WriteOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static object Parse(string text)
        {
            using JsonDocument doc = JsonDocument.Parse(text);
            return Convert(doc.RootElement);
        }

        public static string Write(object value) => JsonSerializer.Serialize(value, WriteOptions);

        // Typed accessors that tolerate missing keys / wrong types.
        public static Dictionary<string, object> Obj(object o, string key) =>
            o is Dictionary<string, object> d && d.TryGetValue(key, out object v) ? v as Dictionary<string, object> : null;

        public static List<object> Arr(object o, string key) =>
            o is Dictionary<string, object> d && d.TryGetValue(key, out object v) ? v as List<object> : null;

        public static string Str(object o, string key, string fallback = "") =>
            o is Dictionary<string, object> d && d.TryGetValue(key, out object v) && v is string s ? s : fallback;

        public static double Num(object o, string key, double fallback = 0) =>
            o is Dictionary<string, object> d && d.TryGetValue(key, out object v) && v is double n ? n : fallback;

        public static bool Bool(object o, string key, bool fallback = false) =>
            o is Dictionary<string, object> d && d.TryGetValue(key, out object v) && v is bool b ? b : fallback;

        private static object Convert(JsonElement el)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Object:
                {
                    var obj = new Dictionary<string, object>();
                    foreach (JsonProperty p in el.EnumerateObject())
                    {
                        obj[p.Name] = Convert(p.Value);
                    }
                    return obj;
                }
                case JsonValueKind.Array:
                {
                    var list = new List<object>();
                    foreach (JsonElement item in el.EnumerateArray())
                    {
                        list.Add(Convert(item));
                    }
                    return list;
                }
                case JsonValueKind.String:
                    return el.GetString();
                case JsonValueKind.Number:
                    return el.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                default:
                    return null;
            }
        }
    }
}
