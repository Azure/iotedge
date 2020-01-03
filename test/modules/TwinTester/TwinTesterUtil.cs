// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Shared;

    static class TwinTesterUtil
    {
        public static TwinCollection GetResetedReportedPropertiesTwin(Twin originalTwin)
        {
            TwinCollection eraseReportedProperties = new TwinCollection();
            foreach (dynamic twinUpdate in originalTwin.Properties.Reported)
            {
                KeyValuePair<string, object> pair = (KeyValuePair<string, object>)twinUpdate;
                eraseReportedProperties[pair.Key] = null; // erase twin property by assigning null
            }

            return eraseReportedProperties;
        }
    }
}
