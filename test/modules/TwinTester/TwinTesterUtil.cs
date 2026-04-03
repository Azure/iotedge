// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;

    static class TwinTesterUtil
    {
        public static Task ResetTwinReportedPropertiesAsync(IotHubModuleClient moduleClient, TwinProperties originalTwin)
        {
            var cleanedReportedProperties = new PropertyCollection();
            foreach (KeyValuePair<string, object> pair in originalTwin.Reported)
            {
                cleanedReportedProperties[pair.Key] = null; // erase twin property by assigning null
            }

            return moduleClient.UpdateReportedPropertiesAsync(cleanedReportedProperties);
        }
    }
}
