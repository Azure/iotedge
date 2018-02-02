// Copyright (c) Microsoft. All rights reserved.

// TODO: Remove resharper suppressions in this file once implementation is complete.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System.Text.RegularExpressions;
    using System.Web;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    // TODO: Edge Hub already has an identity type. This class is more of a helper for parsing identity
    //       strings than a real "identity". Consider re-factoring this so we have less things called "identity".

    public class SaslIdentity
    {
        static readonly Regex IotHubSasIdentityRegex = new Regex(
            @"^(?<keyName>[a-zA-Z0-9_-]+)@sas\.root\.(?<IotHubName>[a-zA-Z0-9_-]+)$",
            RegexOptions.Compiled | RegexOptions.ECMAScript | RegexOptions.IgnoreCase
        );
        static readonly Regex DeviceSasIdentityRegex = new Regex(
            @"^(?<twinId>(?<deviceId>[A-Za-z0-9-:.+%_#*?!(),=;$']+)(?<modules_infix>/modules/)?(?<moduleId>[A-Za-z0-9-:.+%_#*?!(),=;$']+)?)@sas.(?<IotHubName>[a-zA-Z0-9_-]+)$",
            RegexOptions.Compiled | RegexOptions.ECMAScript | RegexOptions.IgnoreCase
        );
        static readonly Regex DeviceUserPasswordIdentityRegex = new Regex(
            @"^(?<twinId>(?<deviceId>[A-Za-z0-9-:.+%_#*?!(),=;$']+)(?<modules_infix>/modules/)?(?<moduleId>[A-Za-z0-9-:.+%_#*?!(),=;$']+)?)@(?<IotHubName>[a-zA-Z0-9_-]+)$",
            RegexOptions.Compiled | RegexOptions.ECMAScript | RegexOptions.IgnoreCase
        );

        public SaslIdentity(
            SaslIdentityType identityType,
            string identity,
            string keyName,
            Option<string> deviceId,
            Option<string> moduleId,
            string iotHubName
        )
        {
            this.IdentityType = Preconditions.CheckIsDefined(identityType);
            this.Identity = Preconditions.CheckNonWhiteSpace(identity, nameof(identity));
            this.KeyName = Preconditions.CheckNonWhiteSpace(keyName, nameof(keyName));
            this.IotHubName = Preconditions.CheckNonWhiteSpace(iotHubName, nameof(iotHubName));
            this.DeviceId = deviceId;
            this.ModuleId = moduleId;
        }

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        // ReSharper disable once MemberCanBePrivate.Global
        public SaslIdentityType IdentityType { get; }

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        // ReSharper disable once MemberCanBePrivate.Global
        public string Identity { get; }

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        // ReSharper disable once MemberCanBePrivate.Global
        public string KeyName { get; }

        public Option<string> DeviceId { get; }

        public Option<string> ModuleId { get; }

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        // ReSharper disable once MemberCanBePrivate.Global
        public string IotHubName { get; }

        public static SaslIdentity Parse(string identity)
        {
            Preconditions.CheckNonWhiteSpace(identity, nameof(identity));

            SaslIdentityType identityType;
            string keyName;
            Option<string> deviceId;
            string iotHubName;
            Option<string> moduleId;

            Match deviceSasMatch = DeviceSasIdentityRegex.Match(identity);
            if (deviceSasMatch.Success)
            {
                identityType = SaslIdentityType.SharedAccessSignature;
                keyName = HttpUtility.UrlDecode(deviceSasMatch.Groups["twinId"].Value);
                deviceId = Option.Maybe(HttpUtility.UrlDecode(deviceSasMatch.Groups["deviceId"].Value));
                moduleId = Option.Maybe(HttpUtility.UrlDecode(deviceSasMatch.Groups["moduleId"].Value));
                iotHubName = deviceSasMatch.Groups["IotHubName"].Value;
            }
            else
            {
                Match iotHubSasMatch = IotHubSasIdentityRegex.Match(identity);
                if (iotHubSasMatch.Success)
                {
                    identityType = SaslIdentityType.SharedAccessSignature;
                    keyName = iotHubSasMatch.Groups["keyName"].Value;
                    deviceId = Option.None<string>();
                    moduleId = Option.None<string>();
                    iotHubName = iotHubSasMatch.Groups["IotHubName"].Value;
                }
                else
                {
                    Match deviceUserPasswordMatch = DeviceUserPasswordIdentityRegex.Match(identity);
                    if (deviceUserPasswordMatch.Success)
                    {
                        identityType = SaslIdentityType.UsernameAndPassword;
                        keyName = HttpUtility.UrlDecode(deviceUserPasswordMatch.Groups["twinId"].Value);
                        deviceId = Option.Maybe(HttpUtility.UrlDecode(deviceUserPasswordMatch.Groups["deviceId"].Value));
                        moduleId = Option.Maybe(HttpUtility.UrlDecode(deviceUserPasswordMatch.Groups["moduleId"].Value));
                        iotHubName = deviceUserPasswordMatch.Groups["IotHubName"].Value;
                    }
                    else
                    {
                        throw new EdgeHubConnectionException("Should be <deviceId>@<IotHubName> for device scope or <keyName>@root.<IotHubName> for device hub scope");
                    }
                }

            }

            return new SaslIdentity(identityType, identity, keyName, deviceId, moduleId, iotHubName);
        }
    }
}
