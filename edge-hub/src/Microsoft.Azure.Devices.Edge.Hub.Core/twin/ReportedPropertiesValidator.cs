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
        const int TwinPropertyMaxDepth = 10; // taken from IoTHub
        const int TwinPropertyValueMaxLength = 4096; // bytes. taken from IoTHub
        const int TwinPropertyNameMaxLength = 1024; // taken from IoTHub
        const long TwinPropertyMaxSafeValue = 4503599627370495; // (2^52) - 1. taken from IoTHub
        const long TwinPropertyMinSafeValue = -4503599627370496; // -2^52. taken from IoTHub
        const int TwinPropertyDocMaxLength = 32 * 1024; // 32KB. taken from IoTHub

        public void Validate(TwinCollection reportedProperties)
        {
            Preconditions.CheckNotNull(reportedProperties, nameof(reportedProperties));

            JToken reportedPropertiesJToken = JToken.Parse(reportedProperties.ToJson());
            ValidateTwinCollectionSize(reportedProperties);
            // root level has no property name.
            ValidateToken(string.Empty, reportedPropertiesJToken, 0, false);
        }

        static void ValidateToken(string name, JToken item, int currentDepth, bool inArray)
        {
            ValidatePropertyNameAndLength(name);

            if (item is JObject @object)
            {
                ValidateTwinDepth(currentDepth);

                // do validation recursively
                foreach (JProperty kvp in @object.Properties())
                {
                    ValidateToken(kvp.Name, kvp.Value, currentDepth + 1, inArray);
                }
            }

            if (item is JValue value)
            {
                ValidateValueType(name, value);
                ValidateValue(name, value, inArray);
            }

            if (item is JArray array)
            {
                ValidateTwinDepth(currentDepth);

                // do array validation
                ValidateArrayContent(array, currentDepth + 1);
            }
        }

        static void ValidatePropertyNameAndLength(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (Encoding.UTF8.GetByteCount(name) > TwinPropertyNameMaxLength)
            {
                string truncated = name.Substring(0, 10);
                throw new InvalidOperationException($"Length of property name {truncated}.. exceeds maximum length of {TwinPropertyNameMaxLength}");
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

        static void ValidateArrayContent(JArray array, int currentDepth)
        {
            foreach (var item in array)
            {
                if (item.Type is JTokenType.Null)
                {
                    throw new InvalidOperationException("Arrays cannot contain 'null' as value");
                }

                if (item is JArray inner)
                {
                    if (currentDepth > TwinPropertyMaxDepth)
                    {
                        throw new InvalidOperationException($"Nested depth of twin property exceeds {TwinPropertyMaxDepth}");
                    }

                    // do array validation
                    ValidateArrayContent(inner, currentDepth + 1);
                }
                else
                {
                    // items in the array don't have property name.
                    ValidateToken(string.Empty, item, currentDepth, true);
                }
            }
        }

        static void ValidateValue(string name, JValue value, bool inArray)
        {
            if (inArray && value.Type is JTokenType.Null)
            {
                throw new InvalidOperationException($"Property {name} of an object in an array cannot be 'null'");
            }

            if (value.Type is JTokenType.Integer)
            {
                ValidateIntegerValue(name, (long)value);
            }
            else
            {
                string s = value.ToString();
                ValidatePropertyValueLength(name, s);
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

        static void ValidateTwinDepth(int currentDepth)
        {
            if (currentDepth > TwinPropertyMaxDepth)
            {
                throw new InvalidOperationException($"Nested depth of twin property exceeds {TwinPropertyMaxDepth}");
            }
        }
    }
}
