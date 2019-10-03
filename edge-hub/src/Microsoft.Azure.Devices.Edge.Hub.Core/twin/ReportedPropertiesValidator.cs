// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Twin
{
    using System;
    using System.Text;
    using JetBrains.Annotations;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json.Linq;

    public class ReportedPropertiesValidator : IValidator<TwinCollection>
    {
        const int TwinPropertyMaxDepth = 5; // taken from IoTHub
        const int TwinPropertyValueMaxLength = 4096; // bytes. taken from IoTHub
        const long TwinPropertyMaxSafeValue = 4503599627370495; // (2^52) - 1. taken from IoTHub
        const long TwinPropertyMinSafeValue = -4503599627370496; // -2^52. taken from IoTHub
        const int TwinPropertyDocMaxLength = 8 * 1024; // 8K bytes. taken from IoTHub

        public void Validate(TwinCollection reportedProperties)
        {
            Preconditions.CheckNotNull(reportedProperties, nameof(reportedProperties));
            JToken reportedPropertiesJToken = JToken.Parse(reportedProperties.ToJson());
            ValidateTwinProperties(reportedPropertiesJToken, 1);
            ValidateTwinCollectionSize(reportedProperties);
        }

        static void ValidateTwinProperties(JToken properties, int currentDepth)
        {
            foreach (JProperty kvp in ((JObject)properties).Properties())
            {
                ValidatePropertyNameAndLength(kvp.Name);

                ValidateValueType(kvp.Name, kvp.Value);

                if (kvp.Value is JValue)
                {
                    if (kvp.Value.Type is JTokenType.Integer)
                    {
                        ValidateIntegerValue(kvp.Name, (long)kvp.Value);
                    }
                    else
                    {
                        string s = kvp.Value.ToString();
                        ValidatePropertyValueLength(kvp.Name, s);
                    }
                }

                if (kvp.Value != null && kvp.Value is JObject)
                {
                    if (currentDepth > TwinPropertyMaxDepth)
                    {
                        throw new InvalidOperationException($"Nested depth of twin property exceeds {TwinPropertyMaxDepth}");
                    }

                    // do validation recursively
                    ValidateTwinProperties(kvp.Value, currentDepth + 1);
                }
            }
        }

        static void ValidatePropertyNameAndLength(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (Encoding.UTF8.GetByteCount(name) > TwinPropertyValueMaxLength)
            {
                string truncated = name.Substring(0, 10);
                throw new InvalidOperationException($"Length of property name {truncated}.. exceeds maximum length of {TwinPropertyValueMaxLength}");
            }

            for (int index = 0; index < name.Length; index++)
            {
                char ch = name[index];
                // $ is reserved for service properties like $metadata, $version etc.
                // However, $ is already a reserved character in Mongo, so we need to substitute it with another character like #.
                // So we're also reserving # for service side usage.
                if (char.IsControl(ch) || ch == '.' || ch == '$' || ch == '#' || char.IsWhiteSpace(ch))
                {
                    throw new InvalidOperationException($"Property name {name} contains invalid character '{ch}'");
                }
            }
        }

        static void ValidatePropertyValueLength(string name, string value)
        {
            int valueByteCount = value != null ? Encoding.UTF8.GetByteCount(value) : 0;
            if (valueByteCount > TwinPropertyValueMaxLength)
            {
                throw new InvalidOperationException($"Value associated with property name {name} has length {valueByteCount} that exceeds maximum length of {TwinPropertyValueMaxLength}");
            }
        }

        [AssertionMethod]
        static void ValidateIntegerValue(string name, long value)
        {
            if (value > TwinPropertyMaxSafeValue || value < TwinPropertyMinSafeValue)
            {
                throw new InvalidOperationException($"Property {name} has an out of bound value. Valid values are between {TwinPropertyMinSafeValue} and {TwinPropertyMaxSafeValue}");
            }
        }

        static void ValidateValueType(string property, JToken value)
        {
            if (!JsonEx.IsValidToken(value))
            {
                throw new InvalidOperationException($"Property {property} has a value of unsupported type. Valid types are integer, float, string, bool, null and nested object");
            }
        }

        static void ValidateTwinCollectionSize(TwinCollection collection)
        {
            long size = Encoding.UTF8.GetByteCount(collection.ToJson());
            if (size > TwinPropertyDocMaxLength)
            {
                throw new InvalidOperationException($"Twin properties size {size} exceeds maximum {TwinPropertyDocMaxLength}");
            }
        }
    }
}
