// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System.Diagnostics;
    using System.Reflection;

    public static class TracingInformation
    {
        public const string EdgeHubSourceName = "EdgeHub";

        public static ActivitySource EdgeHubActivitySource = new ActivitySource(EdgeHubSourceName, Assembly.GetExecutingAssembly().ImageRuntimeVersion);
    }
}
