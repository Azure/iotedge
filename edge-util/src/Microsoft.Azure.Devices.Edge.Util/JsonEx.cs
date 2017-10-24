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

        public static JToken Merge(JToken baselineToken, JToken patchToken, bool treatNullAsDelete)
        {
            // Reached the leaf JValue
            if (patchToken.Type != JTokenType.Object || baselineToken.Type != JTokenType.Object)
            {
                return patchToken;
            }

            JObject patch = (JObject)patchToken;
            JObject baseline = (JObject)baselineToken;
            JObject result = new JObject(baseline);

            foreach (JProperty patchProp in patch.Properties())
            {
                if (IsValidToken(patchProp.Value))
                {
                    var baselineProp = baseline.Property(patchProp.Name);
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

        public static JObject Diff(JToken fromToken, JToken toToken)
        {
            JObject patch = new JObject();

            // both 'from' and 'to' must be objects
            if (fromToken.Type != JTokenType.Object || toToken.Type != JTokenType.Object)
                return patch;

            JObject from = (JObject)fromToken;
            JObject to = (JObject)toToken;

            foreach (JProperty fromProp in from.Properties())
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
                else
                {
                    throw new InvalidOperationException($"Property {fromProp.Name} has a value of unsupported type. Valid types are integer, float, string, bool, null and nested object");
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
    }
}
