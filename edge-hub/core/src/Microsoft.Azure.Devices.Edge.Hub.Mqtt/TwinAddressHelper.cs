// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Primitives;

    /// <summary>
    ///     Set of routines for twin and direct request/response message address parsing / formatting.
    /// </summary>
    public static class TwinAddressHelper
    {
        const char SegmentSeparatorChar = '/';
        const string SegmentSeparator = "/";
        const char PropertiesSegmentPrefixChar = '?';
        const string PropertiesSegmentPrefix = "?";
        const char PropertySeparatorChar = '&';
        const string PropertySeparator = "&";
        const char PropertyValueSeparatorChar = '=';
        const string PropertyValueSeparator = "=";

        const string ServicePrefix = "$iothub" + SegmentSeparator;
        const string TwinChannelSegment = "twin" + SegmentSeparator;
        const string TwinPrefix = ServicePrefix + TwinChannelSegment;
        const string DirectMethodChannelSegment = "methods" + SegmentSeparator;
        const string DirectMethodResponseSegments = DirectMethodChannelSegment + ResponseSegment;
        const string DirectMethodPrefix = ServicePrefix + DirectMethodChannelSegment;
        const string ResponseSegment = "res" + SegmentSeparator;
        const string PatchMethod = "PATCH";
        const string GetMethod = "GET";
        const string PostMethod = "POST";

        static readonly StringSegment EmptyStringSegment = new StringSegment(string.Empty);

        public enum Operation
        {
            Unknown,
            InvalidTwinRequest,
            TwinGetState,
            TwinPatchReportedState,
            DirectMethodResponse
        }

        public static bool CheckTwinAddress(string topicName) => topicName.StartsWith(ServicePrefix, StringComparison.Ordinal);

        public static string FormatNotificationAddress(string version)
            => TwinPrefix + PatchMethod + SegmentSeparator + TwinNames.Properties + SegmentSeparator + TwinNames.Desired + SegmentSeparator
               + PropertiesSegmentPrefix + TwinNames.Version + PropertyValueSeparator + version;

        public static string FormatDeviceMethodRequestAddress(string correlationId, string methodName)
            => DirectMethodPrefix + PostMethod + SegmentSeparator + methodName + SegmentSeparator + PropertiesSegmentPrefix + TwinNames.RequestId + PropertyValueSeparator + correlationId;

        public static string FormatTwinResponseAddress(string statusCode, string correlationId)
            => TwinPrefix + ResponseSegment + statusCode + SegmentSeparator + PropertiesSegmentPrefix
               + TwinNames.RequestId + PropertyValueSeparator + correlationId;

        public static string FormatTwinResponseAddress(string statusCode, string correlationId, string version)
        {
            return FormatTwinResponseAddress(statusCode, correlationId) + PropertySeparator + TwinNames.Version + PropertyValueSeparator + version;
        }

        public static bool TryParseOperation(string address, Dictionary<StringSegment, StringSegment> properties, out Operation operation, out StringSegment resource)
        {
            Preconditions.CheckArgument(CheckTwinAddress(address));

            int offset = ServicePrefix.Length;

            if ((address.Length > TwinPrefix.Length)
                && (string.CompareOrdinal(address, offset, TwinChannelSegment, 0, TwinChannelSegment.Length) == 0))
            {
                offset += TwinChannelSegment.Length;

                const string PatchReportedSegments = PatchMethod + SegmentSeparator + TwinNames.Properties + SegmentSeparator + TwinNames.Reported + SegmentSeparator;
                const string GetSegment = GetMethod + SegmentSeparator;

                if (string.CompareOrdinal(address, offset, PatchReportedSegments, 0, PatchReportedSegments.Length) == 0)
                {
                    operation = Operation.TwinPatchReportedState;
                    offset += PatchReportedSegments.Length;
                }
                else if (string.CompareOrdinal(address, offset, GetSegment, 0, GetSegment.Length) == 0)
                {
                    operation = Operation.TwinGetState;
                    offset += GetSegment.Length;
                }
                else
                {
                    operation = Operation.InvalidTwinRequest;
                    resource = default(StringSegment);
                    return false;
                }
            }
            else if ((address.Length > DirectMethodPrefix.Length)
                     && (string.CompareOrdinal(address, offset, DirectMethodResponseSegments, 0, DirectMethodResponseSegments.Length) == 0))
            {
                operation = Operation.DirectMethodResponse;
                offset += DirectMethodResponseSegments.Length;
            }
            else
            {
                operation = Operation.Unknown;
                resource = default(StringSegment);
                return false;
            }

            if (offset == address.Length)
            {
                resource = EmptyStringSegment;
                return true;
            }

            if (address[offset] == PropertiesSegmentPrefixChar) // check if property bag follows parsed part immediately
            {
                resource = EmptyStringSegment;
                offset++;
            }
            else
            {
                // find the final segment to derive
                int lastSegmentSeparatorIndex = address.LastIndexOf(SegmentSeparatorChar, address.Length - 1, address.Length - offset); // todo: check for off by 1
                if ((lastSegmentSeparatorIndex == -1) // no more segments
                    || (lastSegmentSeparatorIndex == address.Length - 1) // no more non-empty segments
                    || (address[lastSegmentSeparatorIndex + 1] != PropertiesSegmentPrefixChar)) // last segment is not a property bag
                {
                    // declare the rest of the address as resource
                    resource = StringSegmentAtOffset(address, offset);
                    return true;
                }
                else
                {
                    resource = StringSegmentRange(address, offset, lastSegmentSeparatorIndex - 1);
                    offset = lastSegmentSeparatorIndex + 2;
                }
            }

            // property bag follows last separator
            if (!TryParseProperties(address, offset, properties))
            {
                return false;
            }

            return true;
        }

        public static void PassThroughUserProperties(Dictionary<StringSegment, StringSegment> sourcePropertyBag, Dictionary<string, string> targetProperties)
        {
            foreach (KeyValuePair<StringSegment, StringSegment> property in sourcePropertyBag)
            {
                if (property.Key.Value[0] == TwinNames.SystemParameterPrefixChar)
                {
                    continue;
                }

                targetProperties.Add(property.Key.ToString(), property.Value.ToString());
            }
        }

        public static long? DeriveRequestVersion(Dictionary<StringSegment, StringSegment> properties)
        {
            StringSegment versionString;
            long? version = null;
            if (properties.TryGetValue(new StringSegment(TwinNames.Version), out versionString))
            {
                long versionValue;
                if (long.TryParse(versionString.ToString(), out versionValue))
                {
                    version = versionValue;
                }
                else
                {
                    throw new InvalidOperationException("Cannot parse supplied version. Please make sure you are using version value as provided by the service.");
                }
            }

            return version;
        }

        public static string FormatCorrelationId(ulong correlationId) => correlationId.ToString("x", CultureInfo.InvariantCulture);

        public static bool TryParseCorrelationId(string correlationValue, out ulong correlationId) => ulong.TryParse(correlationValue, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out correlationId);

        public static bool IsRequest(Operation operation)
        {
            switch (operation)
            {
                case Operation.TwinGetState:
                case Operation.TwinPatchReportedState:
                case Operation.InvalidTwinRequest:
                    return true;
                case Operation.Unknown:
                case Operation.DirectMethodResponse:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
            }
        }

        // `end` is the index of the last character in the range, inclusive
        static StringSegment StringSegmentRange(string buffer, int start, int end) => new StringSegment(buffer, start, end + 1 - start);

        static StringSegment StringSegmentAtOffset(string buffer, int offset) => StringSegmentRange(buffer, offset, buffer.Length - 1);

        static bool TryParseProperties(string source, int offset, Dictionary<StringSegment, StringSegment> properties)
        {
            bool parsingValue = false;
            StringSegment currentKey = default(StringSegment);
            int currentStartIndex = offset;
            for (int index = offset; index < source.Length; index++)
            {
                char ch = source[index];
                if (parsingValue)
                {
                    if (ch == PropertySeparatorChar)
                    {
                        StringSegment value = currentStartIndex == index ? EmptyStringSegment : StringSegmentRange(source, currentStartIndex, index - 1);
                        properties.Add(currentKey, value);
                        parsingValue = false;
                        currentStartIndex = index + 1;
                    }
                }
                else
                {
                    bool lastChar = index == source.Length - 1;
                    if (ch == PropertyValueSeparatorChar)
                    {
                        currentKey = StringSegmentRange(source, currentStartIndex, index - 1);
                        parsingValue = true;
                        currentStartIndex = index + 1;
                    }
                    else if (lastChar)
                    {
                        // if lastChar, our key should include it
                        currentKey = StringSegmentRange(source, currentStartIndex, index);
                        parsingValue = true;
                        currentStartIndex = index + 1;
                    }
                    else if (ch == PropertySeparatorChar)
                    {
                        StringSegment key = currentStartIndex == index ? EmptyStringSegment : StringSegmentRange(source, currentStartIndex, index - 1);
                        properties.Add(key, EmptyStringSegment);
                        currentStartIndex = index + 1;
                    }
                }
            }

            if (parsingValue)
            {
                properties.Add(currentKey, StringSegmentAtOffset(source, currentStartIndex));
            }

            return true;
        }
    }
}
