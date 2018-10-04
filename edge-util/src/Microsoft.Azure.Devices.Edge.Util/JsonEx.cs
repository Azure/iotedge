// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public static class JsonEx
    {
        public static T Get<T>(this JObject obj, string key)
        {
            if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken token))
            {
                throw new JsonSerializationException($"Could not find {key} in JObject.");
            }
            return token.Value<T>();
        }

        public static string Merge(object baseline, object patch, bool treatNullAsDelete)
        {
            JToken baselineToken = JToken.FromObject(baseline);
            JToken patchToken = JToken.FromObject(patch);
            JToken mergedToken = Merge(baselineToken, patchToken, treatNullAsDelete);
            return mergedToken.ToString();
        }

        public static JToken Merge(JToken baselineToken, JToken patchToken, bool treatNullAsDelete)
        {
            // Reached the leaf JValue
            if (patchToken.Type != JTokenType.Object || baselineToken.Type != JTokenType.Object)
            {
                return patchToken;
            }

            var patch = (JObject)patchToken;
            var baseline = (JObject)baselineToken;
            var result = new JObject(baseline);

            foreach (JProperty patchProp in patch.Properties())
            {
                if (IsValidToken(patchProp.Value))
                {
                    JProperty baselineProp = baseline.Property(patchProp.Name);
                    if (baselineProp != null && patchProp.Value.Type != JTokenType.Null)
                    {
                        JToken nestedResult = Merge(baselineProp.Value, patchProp.Value, treatNullAsDelete);
                        result[patchProp.Name] = nestedResult;
                    }
                    else // decide whether to remove or add the patch key
                    {
                        if (treatNullAsDelete && patchProp.Value.Type == JTokenType.Null)
                        {
                            result.Remove(patchProp.Name);
                        }
                        else
                        {
                            result[patchProp.Name] = patchProp.Value;
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Property {patchProp.Name} has a value of unsupported type. Valid types are integer, float, string, bool, null and nested object");
                }
            }
            return result;
        }

        static readonly JTokenType[] ValidDiffTypes =
        {
            JTokenType.Boolean,
            JTokenType.Float,
            JTokenType.Integer,
            JTokenType.Null,
            JTokenType.Object,
            JTokenType.String,
            JTokenType.Date
        };

        public static bool IsValidToken(JToken token) => ValidDiffTypes.Any(t => t == token.Type);

        public static string Diff(object from, object to)
        {
            JToken fromToken = JToken.FromObject(Preconditions.CheckNotNull(from, nameof(from)));
            JToken toToken = JToken.FromObject(Preconditions.CheckNotNull(to, nameof(to)));
            JObject diff = Diff(fromToken, toToken);
            return diff.ToString();
        }

        public static JObject Diff(JToken fromToken, JToken toToken)
        {
            var patch = new JObject();

            // both 'from' and 'to' must be objects
            if (fromToken.Type != JTokenType.Object || toToken.Type != JTokenType.Object)
                return patch;

            var from = (JObject)fromToken;
            var to = (JObject)toToken;

            foreach (JProperty fromProp in from.Properties())
            {
                if (IsValidToken(fromProp.Value))
                {
                    JProperty toProp = to.Property(fromProp.Name);
                    if (toProp != null)
                    {
                        // if this property exists in 'to' and is an object then do a
                        // recursive deep diff
                        if (fromProp.Value.Type == JTokenType.Object && toProp.Value.Type == JTokenType.Object)
                        {
                            JObject obj = Diff(fromProp.Value, toProp.Value);

                            // if something was added in 'obj' then there's a diff to be
                            // patched in this sub-object
                            if (obj.HasValues)
                            {
                                patch.Add(fromProp.Name, obj);
                            }
                        }

                        // if this property exists in 'to' but has a different value
                        // then add the prop from 'to' to 'patch'
                        else if (fromProp.Value.Type != toProp.Value.Type || fromProp.Value.Equals(toProp.Value) == false)
                        {
                            patch.Add(fromProp.Name, toProp.Value);
                        }
                    }

                    // if a property exists in 'from' but not in 'to' then that
                    // is to be deleted
                    else
                    {
                        patch.Add(fromProp.Name, JValue.CreateNull());
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Property {fromProp.Name} has a value of unsupported type. Valid types are integer, float, string, bool, null and nested object");
                }
            }

            foreach (JProperty toProp in to.Properties())
            {
                if (IsValidToken(toProp.Value))
                {
                    JProperty fromProp = from.Property(toProp.Name);

                    // if this property exists in 'to' but not in 'from' then
                    // add it to 'patch'
                    if (fromProp == null)
                    {
                        patch.Add(toProp.Name, toProp.Value);
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Property {toProp.Name} has a value of unsupported type. Valid types are integer, float, string, bool, null and nested object");
                }
            }

            return patch;
        }

        static readonly string[] MetadataPropertyNames = { "$metadata", "$version" };

        public static JToken StripMetadata(JToken token)
        {
            // get rid of metadata from the token in case it has any because we don't want
            // it affecting the json diff below
            foreach (JToken t in MetadataPropertyNames.Select(name => token[name]).Where(t => t != null))
            {
                t.Parent.Remove();
            }

            return token;
        }

        public static IEnumerable<JToken> ChunkedValue(this JObject self, string name) => new ChunkedProperty(self, name);

        public static IEnumerable<JToken> ChunkedValue(this JObject self, string name, bool ignoreCase) => new ChunkedProperty(self, name, ignoreCase);

        class ChunkedProperty : IEnumerable<JToken>
        {
            readonly JObject obj;
            readonly string name;
            readonly Regex regex;
            readonly IComparer<string> comparer;

            public ChunkedProperty(JObject obj, string name)
                : this(obj, name, false)
            {
            }

            public ChunkedProperty(JObject obj, string name, bool ignoreCase)
            {
                this.obj = Preconditions.CheckNotNull(obj, nameof(obj));
                this.name = Preconditions.CheckNotNull(name, nameof(name));
                this.comparer = ignoreCase
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal;
                var pattern = ignoreCase
                    ? string.Format("(?i:{0})(?<num>[0-9]*)", this.name.ToLower())
                    : string.Format("{0}(?<num>[0-9]*)", this.name);
                this.regex = new Regex(pattern);
            }

            public IEnumerator<JToken> GetEnumerator()
            {
                var tokens = this.obj
                    .Where<KeyValuePair<string, JToken>>(kv => this.regex.IsMatch(kv.Key))
                    .OrderBy(kv => kv.Key, this.comparer)
                    .Enumerate();

                foreach (var (num, kv) in tokens)
                {
                    var strNum = this.regex.Match(kv.Key).Groups["num"].Value;
                    Validate(strNum, num);
                    yield return kv.Value;
                }
            }

            static void Validate(string strNum, uint expectedNum)
            {
                // The zero-th item should have an empty num
                if (expectedNum == 0)
                {
                    if (strNum != string.Empty)
                    {
                        throw new JsonSerializationException(string.Format("Expected empty field number but found \"{0}\"", strNum));
                    }
                }
                else
                {
                    if (!int.TryParse(strNum, out int tokenNum))
                    {
                        throw new JsonSerializationException(string.Format("Attempted to parse integer from {0}", strNum));
                    }

                    if (expectedNum != tokenNum)
                    {
                        throw new JsonSerializationException(string.Format("Error while parsing chunked field, expected {0} found {1}", expectedNum, tokenNum));
                    }
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
