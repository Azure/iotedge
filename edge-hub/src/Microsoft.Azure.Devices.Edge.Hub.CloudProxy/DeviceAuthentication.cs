// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Util;

    public class DeviceAuthentication : DeviceAuthenticationWithTokenRefresh
    {
        readonly ITokenProvider tokenProvider;

        public DeviceAuthentication(ITokenProvider tokenProvider, string deviceId)
            : base(deviceId)
        {
            this.tokenProvider = Preconditions.CheckNotNull(tokenProvider);
        }

        protected override Task<string> SafeCreateNewToken(string iotHub, int suggestedTimeToLive)
        {
            try
            {
                return this.tokenProvider.GetTokenAsync(Option.Some(TimeSpan.FromSeconds(suggestedTimeToLive)));
            }
            catch (TokenProviderException ex)
            {
                // DeviceAuthentication plugs into the device SDK, and we don't
                // want to leak our internal exceptions into the device SDK
                throw new IotHubCommunicationException(ex.Message, ex);
            }
        }
    }
}
