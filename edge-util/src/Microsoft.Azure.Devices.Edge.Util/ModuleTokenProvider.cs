// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Edged;

    public class ModuleTokenProvider : ITokenProvider
    {
        readonly ISignatureProvider signatureProvider;
        readonly string deviceId;
        readonly string moduleId;
        readonly string iotHubHostName;
        readonly TimeSpan defaultTtl;

        public ModuleTokenProvider(ISignatureProvider signatureProvider, string iotHubHostName,
            string deviceId, string moduleId, TimeSpan defaultTtl)
        {
            this.signatureProvider = signatureProvider ?? throw new ArgumentNullException(nameof(signatureProvider));
            this.iotHubHostName = iotHubHostName;
            this.deviceId = deviceId;
            this.moduleId = moduleId;
            this.defaultTtl = defaultTtl;
        }

        public async Task<string> GetTokenAsync(Option<TimeSpan> ttl)
        {
            DateTime startTime = DateTime.UtcNow;
            string audience = SasTokenBuilder.BuildAudience(this.iotHubHostName, this.deviceId, this.moduleId);
            string expiresOn = SasTokenBuilder.BuildExpiresOn(startTime, ttl.GetOrElse(this.defaultTtl));
            string data = string.Join("\n", new List<string> { audience, expiresOn });
            string signature = await this.signatureProvider.SignAsync(data).ConfigureAwait(false);

            return SasTokenBuilder.BuildSasToken(audience, signature, expiresOn);
        }
    }
}
