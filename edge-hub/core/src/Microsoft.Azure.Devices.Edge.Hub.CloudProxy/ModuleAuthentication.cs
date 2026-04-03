// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleAuthentication : ClientAuthenticationWithTokenRefresh
    {
        readonly ITokenProvider tokenProvider;

        public ModuleAuthentication(ITokenProvider tokenProvider, string deviceId, string moduleId)
            : base(deviceId, moduleId)
        {
            this.tokenProvider = Preconditions.CheckNotNull(tokenProvider);
        }

        protected override async Task<string> SafeCreateNewTokenAsync(string iotHub, TimeSpan suggestedTimeToLive, CancellationToken cancellationToken)
        {
            try
            {
                return await this.tokenProvider.GetTokenAsync(Option.Some(suggestedTimeToLive));
            }
            catch (TokenProviderException ex)
            {
                // ModuleAuthentication plugs into the device SDK, and we don't
                // want to leak our internal exceptions into the device SDK
                throw new IotHubClientException(ex.Message, ex);
            }
        }
    }
}
