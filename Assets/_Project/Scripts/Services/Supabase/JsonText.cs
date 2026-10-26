using System.Globalization;
using System.Text;

namespace LostPetNetwork.Services.Supabase
{
    /// <summary>
    /// Builds small JSON request bodies by hand. Unity's JsonUtility cannot leave a field out, write null or
    /// embed an already-built JSON object, all of which the database requests need.
    /// </summary>
    public sealed class JsonBody
    {
        readonly StringBuilder _sb = new StringBuilder("{");
        bool _first = true;

        public JsonBody Str(string key, string value) => Raw(key, value == null ? "null" : Quote(value));

        public JsonBody Num(string key, double value) => Raw(key, value.ToString("R", CultureInfo.InvariantCulture));

        public JsonBody NumOrNull(string key, double? value) => value.HasValue ? Num(key, value.Value) : Raw(key, "null");

        public JsonBody Bool(string key, bool value) => Raw(key, value ? "true" : "false");

        /// <summary>Adds a value that is already valid JSON (an object, number, null...).</summary>
        public JsonBody Raw(string key, string json)
        {
            if (!_first) _sb.Append(',');
            _first = false;
            _sb.Append(Quote(key)).Append(':').Append(string.IsNullOrEmpty(json) ? "null" : json);
            return this;
        }

        public override string ToString() => _sb + "}";

        public static string Quote(string s)
        {
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
