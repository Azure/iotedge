// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class ClientTokenProvider : ITokenProvider
    {
        readonly ISignatureProvider signatureProvider;
        readonly string deviceId;
        readonly Option<string> moduleId;
        readonly string iotHubHostName;
        readonly TimeSpan defaultTtl;

        public ClientTokenProvider(
            ISignatureProvider signatureProvider,
            string iotHubHostName,
            string deviceId,
            string moduleId,
            TimeSpan defaultTtl)
        {
            this.signatureProvider = signatureProvider ?? throw new ArgumentNullException(nameof(signatureProvider));
            this.iotHubHostName = iotHubHostName;
            this.deviceId = deviceId;
            this.moduleId = Option.Maybe(moduleId);
            this.defaultTtl = defaultTtl;
        }

        public ClientTokenProvider(
            ISignatureProvider signatureProvider,
            string iotHubHostName,
            string deviceId,
            TimeSpan defaultTtl)
            : this(signatureProvider, iotHubHostName, deviceId, null, defaultTtl)
        {
        }

        public async Task<string> GetTokenAsync(Option<TimeSpan> ttl)
        {
            DateTime startTime = DateTime.UtcNow;
            string audience = this.moduleId
                .Map(m => SasTokenHelper.BuildAudience(this.iotHubHostName, this.deviceId, m))
                .GetOrElse(() => SasTokenHelper.BuildAudience(this.iotHubHostName, this.deviceId));
            string expiresOn = SasTokenHelper.BuildExpiresOn(startTime, ttl.GetOrElse(this.defaultTtl));
            string data = string.Join(
                "\n",
                new List<string>
                {
                    audience,
                    expiresOn
                });
            string signature = await this.signatureProvider.SignAsync(data);

            return SasTokenHelper.BuildSasToken(audience, signature, expiresOn);
        }
    }
}
