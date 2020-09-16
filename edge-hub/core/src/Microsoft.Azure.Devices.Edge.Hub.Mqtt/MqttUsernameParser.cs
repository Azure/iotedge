// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class MqttUsernameParser : IUsernameParser
    {
        const string ApiVersionKey = "api-version";
        const string DeviceClientTypeKey = "DeviceClientType";
        const string ModelIdKey = "model-id";

        public ClientInfo Parse(string username)
        {
            // Username is of one of the 2 forms:
            //   username   = edgeHubHostName "/" deviceId [ "/" moduleId ] "/?" properties
            // Note, the ? should be the first character of the last segment (as it is a valid character for a deviceId/moduleId)
            //    OR
            //   username   = edgeHubHostName "/" deviceId [ "/" moduleId ] "/" properties
            //   properties = property *("&" property)
            //   property   = name "=" value
            // We recognize three property names:
            //   "api-version" [mandatory]
            //   "DeviceClientType" [optional]
            //   "model-id" [optional]
            // We ignore any properties we don't recognize.
            // Note - this logic does not check the query parameters for special characters, and '?' is treated as a valid value
            // and not used as a separator, unless it is the first character of the last segment
            // (since the property bag is not url encoded). So the following are valid username inputs -
            // "iotHub1/device1/module1/foo?bar=b1&api-version=2010-01-01&DeviceClientType=customDeviceClient1"
            // "iotHub1/device1?&api-version=2010-01-01&DeviceClientType=customDeviceClient1"
            // "iotHub1/device1/module1?&api-version=2010-01-01&DeviceClientType=customDeviceClient1"
            string deviceId;
            string moduleId = string.Empty;
            IDictionary<string, string> queryParameters;

            string[] usernameSegments = Preconditions.CheckNonWhiteSpace(username, nameof(username)).Split('/');
            if (usernameSegments[usernameSegments.Length - 1].StartsWith("?", StringComparison.OrdinalIgnoreCase))
            {
                // edgeHubHostName/device1/?apiVersion=10-2-3&DeviceClientType=foo
                if (usernameSegments.Length == 3)
                {
                    deviceId = usernameSegments[1].Trim();
                    queryParameters = ParseDeviceClientType(usernameSegments[2].Substring(1).Trim());
                }
                else if (usernameSegments.Length == 4)
                {
                    // edgeHubHostName/device1/module1/?apiVersion=10-2-3&DeviceClientType=foo
                    deviceId = usernameSegments[1].Trim();
                    moduleId = usernameSegments[2].Trim();
                    queryParameters = ParseDeviceClientType(usernameSegments[3].Substring(1).Trim());
                }
                else
                {
                    throw new EdgeHubConnectionException($"Username {username} does not contain valid values");
                }
            }
            else
            {
                // edgeHubHostName/device1/apiVersion=10-2-3&DeviceClientType=foo
                if (usernameSegments.Length == 3 && usernameSegments[2].Contains("api-version="))
                {
                    deviceId = usernameSegments[1].Trim();
                    queryParameters = ParseDeviceClientType(usernameSegments[2].Trim());
                }
                else if (usernameSegments.Length == 4 && usernameSegments[3].Contains("api-version="))
                {
                    // edgeHubHostName/device1/module1/apiVersion=10-2-3&DeviceClientType=foo
                    deviceId = usernameSegments[1].Trim();
                    moduleId = usernameSegments[2].Trim();
                    queryParameters = ParseDeviceClientType(usernameSegments[3].Trim());
                }
                else if (usernameSegments.Length == 6 && username.EndsWith("/api-version=2017-06-30/DeviceClientType=Microsoft.Azure.Devices.Client/1.5.1-preview-003", StringComparison.OrdinalIgnoreCase))
                {
                    // The Azure ML container is using an older client that returns a device client with the following format -
                    // username = edgeHubHostName/deviceId/moduleId/api-version=2017-06-30/DeviceClientType=Microsoft.Azure.Devices.Client/1.5.1-preview-003
                    // Notice how the DeviceClientType parameter is separated by a '/' instead of a '&', giving a usernameSegments.Length of 6 instead of the expected 4
                    // To allow those clients to work, check for that specific api-version, and version.
                    deviceId = usernameSegments[1].Trim();
                    moduleId = usernameSegments[2].Trim();
                    queryParameters = new Dictionary<string, string>
                    {
                        [ApiVersionKey] = "2017-06-30",
                        [DeviceClientTypeKey] = "Microsoft.Azure.Devices.Client/1.5.1-preview-003"
                    };
                }
                else
                {
                    throw new EdgeHubConnectionException($"Username {username} does not contain valid values");
                }
            }

            // Check if the api-version parameter exists, but don't check its value.
            if (!queryParameters.TryGetValue(ApiVersionKey, out string apiVersionKey) || string.IsNullOrWhiteSpace(apiVersionKey))
            {
                throw new EdgeHubConnectionException($"Username {username} does not contain a valid Api-version property");
            }

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                throw new EdgeHubConnectionException($"Username {username} does not contain a valid device ID");
            }

            if (!queryParameters.TryGetValue(DeviceClientTypeKey, out string deviceClientType))
            {
                deviceClientType = string.Empty;
            }

            Option<string> modelIdOption = Option.None<string>();
            if (queryParameters.TryGetValue(ModelIdKey, out string modelId) && !string.IsNullOrWhiteSpace(modelId))
            {
                modelIdOption = Option.Some(modelId);
            }

            return new ClientInfo(deviceId, moduleId, deviceClientType, modelIdOption);
        }

        static IDictionary<string, string> ParseDeviceClientType(string queryParameterString)
        {
            // example input: "api-version=version&DeviceClientType=url-escaped-string&other-prop=value&some-other-prop"
            var kvsep = new[] { '=' };

            Dictionary<string, string> queryParameters = queryParameterString
                .Split('&') // split input string into params
                .Select(s => s.Split(kvsep, 2)) // split each param into a key/value pair
                .GroupBy(s => s[0]) // group duplicates (by key) together...
                .Select(s => s.First()) // ...and keep only the first one
                .ToDictionary(// convert to Dictionary<string, string>
                    s => s[0],
                    s => Uri.UnescapeDataString(s.ElementAtOrEmpty(1)));
            return queryParameters;
        }
    }
}
