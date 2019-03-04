// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    public class DeviceIdentityProvider : IDeviceIdentityProvider
    {
        const string ApiVersionKey = "api-version";
        const string DeviceClientTypeKey = "DeviceClientType";
        readonly IAuthenticator authenticator;
        readonly IClientCredentialsFactory clientCredentialsFactory;
        readonly bool clientCertAuthAllowed;
        Option<X509Certificate2> remoteCertificate;
        IList<X509Certificate2> remoteCertificateChain;

        public DeviceIdentityProvider(IAuthenticator authenticator, IClientCredentialsFactory clientCredentialsFactory, bool clientCertAuthAllowed)
        {
            this.authenticator = authenticator;
            this.clientCredentialsFactory = clientCredentialsFactory;
            this.clientCertAuthAllowed = clientCertAuthAllowed;
            this.remoteCertificate = Option.None<X509Certificate2>();
            this.remoteCertificateChain = new List<X509Certificate2>();
        }

        public async Task<IDeviceIdentity> GetAsync(string clientId, string username, string password, EndPoint clientAddress)
        {
            try
            {
                Preconditions.CheckNonWhiteSpace(username, nameof(username));
                Preconditions.CheckNonWhiteSpace(clientId, nameof(clientId));

                (string deviceId, string moduleId, string deviceClientType) = ParseUserName(username);
                IClientCredentials deviceCredentials = null;

                if (!string.IsNullOrEmpty(password))
                {
                    deviceCredentials = this.clientCredentialsFactory.GetWithSasToken(deviceId, moduleId, deviceClientType, password, false);
                }
                else if (this.remoteCertificate.HasValue)
                {
                    if (!this.clientCertAuthAllowed)
                    {
                        Events.CertAuthNotEnabled(deviceId, moduleId);
                        return UnauthenticatedDeviceIdentity.Instance;
                    }

                    this.remoteCertificate.ForEach(
                        cert =>
                        {
                            deviceCredentials = this.clientCredentialsFactory.GetWithX509Cert(
                                deviceId,
                                moduleId,
                                deviceClientType,
                                cert,
                                this.remoteCertificateChain);
                        });
                }
                else
                {
                    Events.AuthNotFound(deviceId, moduleId);
                    return UnauthenticatedDeviceIdentity.Instance;
                }

                if (deviceCredentials == null
                    || !clientId.Equals(deviceCredentials.Identity.Id, StringComparison.Ordinal)
                    || !await this.authenticator.AuthenticateAsync(deviceCredentials))
                {
                    Events.Error(clientId, username);
                    return UnauthenticatedDeviceIdentity.Instance;
                }

                Events.Success(clientId, username);
                return new ProtocolGatewayIdentity(deviceCredentials);
            }
            catch (Exception ex)
            {
                Events.ErrorCreatingIdentity(ex);
                throw;
            }
        }

        public void RegisterConnectionCertificate(X509Certificate2 certificate, IList<X509Certificate2> chain)
        {
            this.remoteCertificate = Option.Some(Preconditions.CheckNotNull(certificate, nameof(certificate)));
            this.remoteCertificateChain = Preconditions.CheckNotNull(chain, nameof(chain));
        }

        internal static (string deviceId, string moduleId, string deviceClientType) ParseUserName(string username)
        {
            // Username is of one of the 2 forms:
            //   username   = edgeHubHostName "/" deviceId [ "/" moduleId ] "/?" properties
            // Note, the ? should be the first character of the last segment (as it is a valid character for a deviceId/moduleId)
            //    OR
            //   username   = edgeHubHostName "/" deviceId [ "/" moduleId ] "/" properties
            //   properties = property *("&" property)
            //   property   = name "=" value
            // We recognize two property names:
            //   "api-version" [mandatory]
            //   "DeviceClientType" [optional]
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

            return (deviceId, moduleId, deviceClientType);
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
                .ToDictionary( // convert to Dictionary<string, string>
                    s => s[0],
                    s => Uri.UnescapeDataString(s.ElementAtOrEmpty(1)));
            return queryParameters;
        }

        static class Events
        {
            const int IdStart = MqttEventIds.SasTokenDeviceIdentityProvider;
            static readonly ILogger Log = Logger.Factory.CreateLogger<DeviceIdentityProvider>();

            enum EventIds
            {
                CreateSuccess = IdStart,
                CreateFailure,
                CertAuthNotEnabled,
                AuthNotFound,
                ErrorCreatingIdentity
            }

            public static void Success(string clientId, string username)
                => Log.LogInformation((int)EventIds.CreateSuccess, Invariant($"Successfully generated identity for clientId {clientId} and username {username}"));

            public static void Error(string clientId, string username)
                => Log.LogError((int)EventIds.CreateFailure, Invariant($"Unable to generate identity for clientId {clientId} and username {username}"));

            public static void CertAuthNotEnabled(string deviceId, string moduleId)
                => Log.LogInformation((int)EventIds.CertAuthNotEnabled, Invariant($"Cannot create identity for {GetId(deviceId, moduleId)} because certificate authentication is not enabled"));

            public static void AuthNotFound(string deviceId, string moduleId)
                => Log.LogInformation((int)EventIds.AuthNotFound, Invariant($"Cannot create identity for {GetId(deviceId, moduleId)} because neither token nor certificate was presented"));

            public static void ErrorCreatingIdentity(Exception ex)
                => Log.LogError((int)EventIds.ErrorCreatingIdentity, ex, "Error creating client identity");

            static string GetId(string deviceId, string moduleId) =>
                string.IsNullOrWhiteSpace(moduleId) ? deviceId : $"{deviceId}/{moduleId}";
        }
    }
}
