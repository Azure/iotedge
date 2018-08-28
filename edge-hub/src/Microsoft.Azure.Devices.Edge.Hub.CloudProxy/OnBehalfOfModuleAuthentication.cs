// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;

    public class OnBehalfOfModuleAuthentication : ModuleAuthenticationWithTokenRefresh
    {
        readonly ITokenProvider tokenProvider;

        public OnBehalfOfModuleAuthentication(ITokenProvider tokenProvider, string deviceId, string moduleId)
            : base(deviceId, moduleId)
        {
            this.tokenProvider = Preconditions.CheckNotNull(tokenProvider);
        }

        protected override Task<string> SafeCreateNewToken(string iotHub, int suggestedTimeToLive) =>
            this.tokenProvider.GetTokenAsync(Option.Some(TimeSpan.FromSeconds(suggestedTimeToLive)));
    }
}
