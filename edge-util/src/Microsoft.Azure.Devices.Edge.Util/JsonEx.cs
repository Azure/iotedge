// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System.Linq;

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

        public static string MergeJson(string baseline, string patch, bool treatNullAsDelete)
        {
            JToken baselineJToken = JToken.Parse(Preconditions.CheckNonWhiteSpace(baseline, nameof(baseline)));
            JToken patchJToken = JToken.Parse(Preconditions.CheckNonWhiteSpace(patch, nameof(patch)));
            JToken mergedJToken = MergeJson(baselineJToken, patchJToken, treatNullAsDelete);
            return mergedJToken.ToString();
        }

        static JToken MergeJson(JToken baseline, JToken patch, bool treatNullAsDelete)
        {
            // Reached the leaf JValue
            if ((patch is JValue) || (baseline.Type == JTokenType.Null) || (baseline is JValue))
            {
                return patch;
            }

            Dictionary<string, JToken> patchDictionary = patch.ToObject<Dictionary<string, JToken>>();
            Dictionary<string, JToken> baselineDictionary = baseline.ToObject<Dictionary<string, JToken>>();

            Dictionary<string, JToken> result = baselineDictionary;
            foreach (KeyValuePair<string, JToken> patchPair in patchDictionary)
            {
                bool baselineContainsKey = baselineDictionary.ContainsKey(patchPair.Key);
                if (baselineContainsKey && (patchPair.Value.Type != JTokenType.Null))
                {
                    JToken baselineValue = baselineDictionary[patchPair.Key];
                    JToken nestedResult = MergeJson(baselineValue, patchPair.Value, treatNullAsDelete);
                    result[patchPair.Key] = nestedResult;
                }
                else // decide whether to remove or add the patch key
                {
                    if (treatNullAsDelete && (patchPair.Value.Type == JTokenType.Null))
                    {
                        result.Remove(patchPair.Key);
                    }
                    else
                    {
                        result[patchPair.Key] = patchPair.Value;
                    }
                }
            }
            return JToken.FromObject(result);
        }

		static readonly JTokenType[] ValidDiffTypes =
        {
            JTokenType.Boolean,
            JTokenType.Float,
            JTokenType.Integer,
            JTokenType.Null,
            JTokenType.Object,
            JTokenType.String
        };

        public static bool IsValidToken(JToken token) => ValidDiffTypes.Any(t => t == token.Type);

        public static JObject Diff(JToken fromToken, JToken toToken)
        {
            JObject patch = new JObject();

            // both 'from' and 'to' must be objects
            if (fromToken.Type != JTokenType.Object || toToken.Type != JTokenType.Object)
                return patch;

            JObject from = (JObject)fromToken;
            JObject to = (JObject)toToken;

            foreach (var fromProp in from.Properties())
            {
                if (IsValidToken(fromProp.Value))
                {
                    var toProp = to.Property(fromProp.Name);
                    if (toProp != null)
                    {
                        // if this property exists in 'to' and is an object then do a
                        // recursive deep diff
                        if (fromProp.Value.Type == JTokenType.Object && toProp.Value.Type == JTokenType.Object)
                        {
                            var obj = Diff(fromProp.Value, toProp.Value);

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
            }

            foreach (var toProp in to.Properties())
            {
                if (IsValidToken(toProp.Value))
                {
                    var fromProp = from.Property(toProp.Name);

                    // if this property exists in 'to' but not in 'from' then
                    // add it to 'patch'
                    if (fromProp == null)
                    {
                        patch.Add(toProp.Name, toProp.Value);
                    }
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
    }
}
