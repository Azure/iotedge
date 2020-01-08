// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;

    static class TwinTesterUtil
    {
        public static Task ResetTwinReportedPropertiesAsync(ModuleClient moduleClient, Twin originalTwin)
        {
            TwinCollection cleanedReportedProperties = new TwinCollection();
            foreach (dynamic twinUpdate in originalTwin.Properties.Reported)
            {
                KeyValuePair<string, object> pair = (KeyValuePair<string, object>)twinUpdate;
                cleanedReportedProperties[pair.Key] = null; // erase twin property by assigning null
            }

            return moduleClient.UpdateReportedPropertiesAsync(cleanedReportedProperties);
        }
    }
}
