using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ClaudeUnity
{
    /// <summary>
    /// Lightweight JSON parser for dynamic JSON structures.
    /// Returns JsonObject which wraps Dictionary for easy access.
    /// </summary>
    public class JsonObject
    {
        private readonly Dictionary<string, object> _data;

        public JsonObject(Dictionary<string, object> data)
        {
            _data = data ?? new Dictionary<string, object>();
        }

        public string GetString(string key)
        {
            if (_data.TryGetValue(key, out var val) && val is string s) return s;
            return null;
        }

        public int GetInt(string key, int def = 0)
        {
            if (_data.TryGetValue(key, out var val))
            {
                if (val is double d) return (int)d;
                if (val is long l) return (int)l;
                if (val is int i) return i;
            }
            return def;
        }

        public double GetDouble(string key, double def = 0)
        {
            if (_data.TryGetValue(key, out var val))
            {
                if (val is double d) return d;
                if (val is long l) return l;
                if (val is int i) return i;
            }
            return def;
        }

        public bool GetBool(string key, bool def = false)
        {
            if (_data.TryGetValue(key, out var val) && val is bool b) return b;
            return def;
        }

        public JsonObject GetObject(string key)
        {
            if (_data.TryGetValue(key, out var val) && val is Dictionary<string, object> d)
                return new JsonObject(d);
            return null;
        }

        public List<object> GetArray(string key)
        {
            if (_data.TryGetValue(key, out var val) && val is List<object> list)
                return list;
            return null;
        }

        public object GetRaw(string key)
        {
            _data.TryGetValue(key, out var val);
            return val;
        }

        public bool Has(string key) => _data.ContainsKey(key);

        public Dictionary<string, object> ToDictionary() => _data;

        public IEnumerable<string> Keys => _data.Keys;
    }

    public static class SimpleJsonParser
    {
        public static JsonObject Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return new JsonObject(null);
            int index = 0;
            var result = ParseValue(json, ref index);
            if (result is Dictionary<string, object> dict)
                return new JsonObject(dict);
            return new JsonObject(null);
        }

        public static object ParseValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length) return null;

            char c = json[index];
            if (c == '{') return ParseObject(json, ref index);
            if (c == '[') return ParseArray(json, ref index);
            if (c == '"') return ParseString(json, ref index);
            if (c == 't' || c == 'f') return ParseBool(json, ref index);
            if (c == 'n') return ParseNull(json, ref index);
            return ParseNumber(json, ref index);
        }
        private static Dictionary<string, object> ParseObject(string json, ref int index)
        {
            var dict = new Dictionary<string, object>();
            index++; // skip {
            SkipWhitespace(json, ref index);

            while (index < json.Length && json[index] != '}')
            {
                SkipWhitespace(json, ref index);
                if (json[index] == '}') break;
                if (json[index] == ',') { index++; continue; }

                var key = ParseString(json, ref index);
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ':') index++;
                SkipWhitespace(json, ref index);
                var value = ParseValue(json, ref index);
                dict[key] = value;
                SkipWhitespace(json, ref index);
            }

            if (index < json.Length) index++; // skip }
            return dict;
        }

        private static List<object> ParseArray(string json, ref int index)
        {
            var list = new List<object>();
            index++; // skip [
            SkipWhitespace(json, ref index);

            while (index < json.Length && json[index] != ']')
            {
                if (json[index] == ',') { index++; SkipWhitespace(json, ref index); continue; }
                list.Add(ParseValue(json, ref index));
                SkipWhitespace(json, ref index);
            }

            if (index < json.Length) index++; // skip ]
            return list;
        }

        private static string ParseString(string json, ref int index)
        {
            index++; // skip opening "
            var sb = new StringBuilder();
            while (index < json.Length)
            {
                char c = json[index];
                if (c == '\\' && index + 1 < json.Length)
                {
                    index++;
                    char next = json[index];
                    switch (next)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (index + 4 < json.Length)
                            {
                                var hex = json.Substring(index + 1, 4);
                                sb.Append((char)Convert.ToInt32(hex, 16));
                                index += 4;
                            }
                            break;
                        default: sb.Append(next); break;
                    }
                }
                else if (c == '"')
                {
                    index++; // skip closing "
                    return sb.ToString();
                }
                else
                {
                    sb.Append(c);
                }
                index++;
            }
            return sb.ToString();
        }

        private static object ParseNumber(string json, ref int index)
        {
            int start = index;
            if (json[index] == '-') index++;
            while (index < json.Length && (char.IsDigit(json[index]) || json[index] == '.' || json[index] == 'e' || json[index] == 'E' || json[index] == '+' || json[index] == '-'))
                index++;
            var numStr = json.Substring(start, index - start);
            if (numStr.Contains(".") || numStr.Contains("e") || numStr.Contains("E"))
                return double.Parse(numStr, CultureInfo.InvariantCulture);
            if (long.TryParse(numStr, out var l)) return l;
            return double.Parse(numStr, CultureInfo.InvariantCulture);
        }

        private static bool ParseBool(string json, ref int index)
        {
            if (json.Substring(index, 4) == "true") { index += 4; return true; }
            if (json.Substring(index, 5) == "false") { index += 5; return false; }
            throw new FormatException($"Invalid bool at {index}");
        }

        private static object ParseNull(string json, ref int index)
        {
            if (json.Substring(index, 4) == "null") { index += 4; return null; }
            throw new FormatException($"Invalid null at {index}");
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
        }

        /// <summary>
        /// Extracts raw JSON substring for a given key from a JSON object string.
        /// Useful for preserving tool_use input as raw JSON.
        /// </summary>
        public static string ExtractRawValue(string json, string key)
        {
            var searchKey = "\"" + key + "\"";
            int keyIndex = json.IndexOf(searchKey);
            if (keyIndex < 0) return null;

            int colonIndex = json.IndexOf(':', keyIndex + searchKey.Length);
            if (colonIndex < 0) return null;

            int valueStart = colonIndex + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart])) valueStart++;

            int depth = 0;
            int i = valueStart;
            char startChar = json[i];
            bool inString = false;

            if (startChar == '{' || startChar == '[')
            {
                char open = startChar;
                char close = startChar == '{' ? '}' : ']';
                depth = 1;
                i++;
                while (i < json.Length && depth > 0)
                {
                    char c = json[i];
                    if (c == '\\' && inString) { i += 2; continue; }
                    if (c == '"') inString = !inString;
                    if (!inString)
                    {
                        if (c == open) depth++;
                        else if (c == close) depth--;
                    }
                    i++;
                }
                return json.Substring(valueStart, i - valueStart);
            }

            // Primitive value
            while (i < json.Length && json[i] != ',' && json[i] != '}' && json[i] != ']')
                i++;
            return json.Substring(valueStart, i - valueStart).Trim();
        }
    }
}
