// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public static class JsonEx
    {
        static readonly JTokenType[] ValidDiffTypes =
        {
            JTokenType.Boolean,
            JTokenType.Float,
            JTokenType.Integer,
            JTokenType.Null,
            JTokenType.Object,
            JTokenType.String,
            JTokenType.Date,
            JTokenType.Array
        };

        static readonly string[] MetadataPropertyNames = { "$metadata", "$version" };

        public static T Get<T>(this JObject obj, string key)
        {
            if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken token))
            {
                throw new JsonSerializationException($"Could not find {key} in JObject.");
            }

            return token.Value<T>();
        }

        public static string Merge(object baseline, object patch, bool treatNullAsDelete, string chunkedProperty = "")
        {
            JToken baselineToken = JToken.FromObject(baseline);
            JToken patchToken = JToken.FromObject(patch);
            JToken mergedToken = Merge(baselineToken, patchToken, treatNullAsDelete, chunkedProperty);
            return mergedToken.ToString();
        }

        public static JToken Merge(JToken baselineToken, JToken patchToken, bool treatNullAsDelete, string chunkedProperty = "")
        {
            // Reached the leaf JValue
            if (patchToken.Type != JTokenType.Object || baselineToken.Type != JTokenType.Object)
            {
                return patchToken;
            }

            var patch = (JObject)patchToken;
            var baseline = (JObject)baselineToken;
            var result = new JObject(baseline);

            // Collect the chunked (for example createOptionsXX) keys that exist in the patch
            HashSet<string> patchChunkedNames = new HashSet<string>();

            if (!string.IsNullOrEmpty(chunkedProperty)) {
                patchChunkedNames = patch.Properties()
                    .Where(p => IsChunkedName(chunkedProperty, p.Name))
                    .Select(p => p.Name)
                    .ToHashSet(StringComparer.Ordinal);
            }

            foreach (JProperty patchProp in patch.Properties())
            {
                if (IsValidToken(patchProp.Value))
                {
                    JProperty baselineProp = baseline.Property(patchProp.Name);
                    if (baselineProp != null && patchProp.Value.Type != JTokenType.Null)
                    {
                        JToken nestedResult = Merge(baselineProp.Value, patchProp.Value, treatNullAsDelete, chunkedProperty);
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

            // Clean up result from non-existing chunked properties.
            if (!string.IsNullOrEmpty(chunkedProperty)) {
                var resultToRemove = result.Properties()
                    .Where(p =>
                        IsChunkedName(chunkedProperty, p.Name) &&
                        (patchChunkedNames.Count == 0 ||
                        !patchChunkedNames.Contains(p.Name)))
                    .Select(p => p.Name)
                    .ToList();

                foreach (var name in resultToRemove)
                {
                    result.Remove(name);
                }
            }
            return result;
        }

        private static bool IsChunkedName(string chunkedName, string propertyName)
        {
            if (!propertyName.StartsWith(chunkedName, StringComparison.Ordinal))
            {
                return false;
            }

            // Require exactly two digits after chunked property (for example "createOptionsXX")
            if (propertyName.Length != chunkedName.Length + 2)
            {
                return false;
            }

            string suffix = propertyName.Substring(chunkedName.Length); // e.g. "01", "15"

            return int.TryParse(suffix, out int n) && n >= 0 && n <= 99;
        }

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
            {
                return patch;
            }

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
                        else if (fromProp.Value.Type != toProp.Value.Type || !JToken.DeepEquals(fromProp.Value, toProp.Value))
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

        /// <summary>
        /// Returns an ordered iterator of JTokens for a chunked field.
        /// The field must use a sequence number suffix, excluding zero.
        /// For example, createOptions, createOptions01, createOptions02, etc.
        /// The iterator assumes that these fields are string sortable.
        /// </summary>
        /// <param name="self">The JObject</param>
        /// <param name="name">The base name of the field. For example, createOptions</param>
        /// <returns></returns>
        public static IEnumerable<JToken> ChunkedValue(this JObject self, string name) => new ChunkedProperty(self, name);

        /// <summary>
        /// Returns an ordered iterator of JTokens for a chunked field.
        /// The field must use a sequence number suffix, excluding zero.
        /// For example, createOptions, createOptions01, createOptions02, etc.
        /// The iterator assumes that these fields are string sortable.
        /// </summary>
        /// <param name="self">The JObject</param>
        /// <param name="name">The base name of the field. For example, createOptions</param>
        /// <param name="ignoreCase">If true, ignore case of the field name</param>
        /// <returns></returns>
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
                    this.Validate(strNum, num);
                    yield return kv.Value;
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

            void Validate(string strNum, uint expectedNum)
            {
                // The zero-th item should have an empty num
                if (expectedNum == 0)
                {
                    if (strNum != string.Empty)
                    {
                        throw new JsonSerializationException(string.Format("Error while parsing chunked field \"{0}\", expected empty field number but found \"{1}\"", this.name, strNum));
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
                        throw new JsonSerializationException(string.Format("Error while parsing chunked field \"{0}\", expected {0}{1:D2} found {0}{2}", this.name, expectedNum, strNum));
                    }
                }
            }
        }
    }
}
