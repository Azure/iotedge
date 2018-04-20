// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.ClientWrapper
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;

    class ModuleAuthenticationWithEdgeToken : ModuleAuthenticationWithTokenRefresh
    {
        readonly ISignatureProvider signatureProvider;

        public ModuleAuthenticationWithEdgeToken(ISignatureProvider signatureProvider, string deviceId, string moduleId) : base(deviceId, moduleId)
        {
            Preconditions.CheckNotNull(signatureProvider, nameof(signatureProvider));
            Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.signatureProvider = signatureProvider;
        }

        protected override async Task<string> SafeCreateNewToken(string iotHub, int suggestedTimeToLive)
        {
            Preconditions.CheckNonWhiteSpace(iotHub, nameof(iotHub));
            Preconditions.CheckRange(suggestedTimeToLive, TimeSpan.MinValue.TotalSeconds, TimeSpan.MaxValue.TotalSeconds, nameof(suggestedTimeToLive));

            DateTime startTime = DateTime.UtcNow;
            string audience = SasTokenBuilder.BuildAudience(iotHub, this.DeviceId, this.ModuleId);
            string expiresOn = SasTokenBuilder.BuildExpiresOn(startTime, TimeSpan.FromSeconds(suggestedTimeToLive));
            string data = string.Join("\n", new List<string> { audience, expiresOn });
            string signature = await this.signatureProvider.SignAsync(this.ModuleId, data);

            return SasTokenBuilder.BuildSasToken(audience, signature, expiresOn, this.ModuleId);
        }
    }
}
