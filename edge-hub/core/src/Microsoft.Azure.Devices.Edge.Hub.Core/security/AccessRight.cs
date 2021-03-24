// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    [Flags]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum AccessRights
    {
        DeviceConnect = 8,
    }

    static class AccessRightsHelper
    {
        public static string[] AccessRightsToStringArray(AccessRights accessRights)
        {
            var values = new List<string>(2);
            foreach (AccessRights right in Enum.GetValues(typeof(AccessRights)))
            {
                if (accessRights.HasFlag(right))
                {
                    values.Add(right.ToString());
                }
            }

            return values.ToArray();
        }
    }
}
