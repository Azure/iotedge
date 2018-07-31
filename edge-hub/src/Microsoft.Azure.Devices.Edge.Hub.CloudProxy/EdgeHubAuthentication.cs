// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util.Edged;

    /// <summary>
    /// Authentication method that uses HSM to get a SAS token. 
    /// </summary>
    public class EdgeHubAuthentication : ModuleAuthenticationWithTokenRefresh
    {
        readonly ISignatureProvider signatureProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeHubAuthentication"/> class.
        /// </summary>
        public EdgeHubAuthentication(ISignatureProvider signatureProvider, string deviceId)
            : base(deviceId, $"$edgeHub")
        {
            this.signatureProvider = signatureProvider ?? throw new ArgumentNullException(nameof(signatureProvider));
        }

        protected override async Task<string> SafeCreateNewToken(string iotHub, int suggestedTimeToLive)
        {
            DateTime startTime = DateTime.UtcNow;
            string audience = SasTokenBuilder.BuildAudience(iotHub, this.DeviceId, this.ModuleId);
            string expiresOn = SasTokenBuilder.BuildExpiresOn(startTime, TimeSpan.FromSeconds(suggestedTimeToLive));
            string data = string.Join("\n", new List<string> { audience, expiresOn });
            string signature = await this.signatureProvider.SignAsync(data).ConfigureAwait(false);

            return SasTokenBuilder.BuildSasToken(audience, signature, expiresOn);
        }
    }
}
