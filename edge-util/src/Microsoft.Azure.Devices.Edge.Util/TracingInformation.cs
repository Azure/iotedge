// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;

    public static class TracingInformation
    {
        public const string EdgeHubSourceName = "EdgeHub";

        public const string EdgeAgentSourceName = "EdgeAgent";

        public static ActivitySource EdgeHubActivitySource = new ActivitySource(EdgeHubSourceName, Assembly.GetExecutingAssembly().ImageRuntimeVersion);

        public static ActivitySource EdgeAgentActivitySource = new ActivitySource(EdgeAgentSourceName, Assembly.GetExecutingAssembly().ImageRuntimeVersion);

        public static IEnumerable<string> ExtractTraceContextFromCarrier(IDictionary<string, string> carrier, string key)
        {
            if (carrier.TryGetValue(key, out var value))
            {
                return new[] { value };
            }

            return Enumerable.Empty<string>();
        }

        public static IEnumerable<string> ExtractTraceContextFromCarrier(IReadOnlyDictionary<string, string> carrier, string key)
        {
            if (carrier.TryGetValue(key, out var value))
            {
                return new[] { value };
            }

            return Enumerable.Empty<string>();
        }

        public static void InjectTraceContextIntoCarrier(IDictionary<string, string> carrier, string key, string value)
        {
            carrier[key] = value;
        }
    }
}
