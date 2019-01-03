// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System.Text.RegularExpressions;
    using System.Web;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public static class SaslIdentity
    {
        static readonly Regex DeviceSasIdentityRegex = new Regex(
            @"^(?<twinId>(?<deviceId>[A-Za-z0-9-:.+%_#*?!(),=;$']+)(?<modules_infix>/modules/)?(?<moduleId>[A-Za-z0-9-:.+%_#*?!(),=;$']+)?)@sas.(?<IotHubName>[a-zA-Z0-9_-]+)$",
            RegexOptions.Compiled | RegexOptions.ECMAScript | RegexOptions.IgnoreCase);

        static readonly Regex DeviceUserPasswordIdentityRegex = new Regex(
            @"^(?<twinId>(?<deviceId>[A-Za-z0-9-:.+%_#*?!(),=;$']+)(?<modules_infix>/modules/)?(?<moduleId>[A-Za-z0-9-:.+%_#*?!(),=;$']+)?)@(?<IotHubName>[a-zA-Z0-9_-]+)$",
            RegexOptions.Compiled | RegexOptions.ECMAScript | RegexOptions.IgnoreCase);

        public static (string deviceId, string moduleId, string iotHubName) Parse(string identity)
        {
            Preconditions.CheckNonWhiteSpace(identity, nameof(identity));

            string deviceId;
            string moduleId;
            string iotHubName;

            Match deviceSasMatch = DeviceSasIdentityRegex.Match(identity);
            if (deviceSasMatch.Success)
            {
                deviceId = HttpUtility.UrlDecode(deviceSasMatch.Groups["deviceId"].Value);
                moduleId = HttpUtility.UrlDecode(deviceSasMatch.Groups["moduleId"].Value);
                iotHubName = deviceSasMatch.Groups["IotHubName"].Value;
            }
            else
            {
                Match deviceUserPasswordMatch = DeviceUserPasswordIdentityRegex.Match(identity);
                if (deviceUserPasswordMatch.Success)
                {
                    deviceId = HttpUtility.UrlDecode(deviceUserPasswordMatch.Groups["deviceId"].Value);
                    moduleId = HttpUtility.UrlDecode(deviceUserPasswordMatch.Groups["moduleId"].Value);
                    iotHubName = deviceUserPasswordMatch.Groups["IotHubName"].Value;
                }
                else
                {
                    throw new EdgeHubConnectionException("Should be <deviceId>@<IotHubName> for device scope or <keyName>@root.<IotHubName> for device hub scope");
                }

            }

            return (deviceId, moduleId, iotHubName);
        }
    }
}
