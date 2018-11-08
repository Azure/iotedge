// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;

    public class OnBehalfOfDeviceAuthentication : DeviceAuthenticationWithTokenRefresh
    {
        readonly ITokenProvider tokenProvider;

        public OnBehalfOfDeviceAuthentication(ITokenProvider tokenProvider, string deviceId)
            : base(deviceId)
        {
            this.tokenProvider = Preconditions.CheckNotNull(tokenProvider);
        }

        protected override Task<string> SafeCreateNewToken(string iotHub, int suggestedTimeToLive) =>
            this.tokenProvider.GetTokenAsync(Option.Some(TimeSpan.FromSeconds(suggestedTimeToLive)));
    }
}
