// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Edged;

    ///// <summary>
    ///// Authentication method that uses HSM to get a SAS token. 
    ///// </summary>
    //public class ModuleAuthentication : ModuleAuthenticationWithTokenRefresh
    //{
    //    readonly ISignatureProvider signatureProvider;

    //    /// <summary>
    //    /// Initializes a new instance of the <see cref="ModuleAuthentication"/> class.
    //    /// </summary>
    //    public ModuleAuthentication(ISignatureProvider signatureProvider, string deviceId, string moduleId)
    //        : base(deviceId, moduleId)
    //    {
    //        this.signatureProvider = signatureProvider ?? throw new ArgumentNullException(nameof(signatureProvider));
    //    }

    //    protected override async Task<string> SafeCreateNewToken(string iotHub, int suggestedTimeToLive)
    //    {
    //        DateTime startTime = DateTime.UtcNow;
    //        string audience = SasTokenBuilder.BuildAudience(iotHub, this.DeviceId, this.ModuleId);
    //        string expiresOn = SasTokenBuilder.BuildExpiresOn(startTime, TimeSpan.FromSeconds(suggestedTimeToLive));
    //        string data = string.Join("\n", new List<string> { audience, expiresOn });
    //        string signature = await this.signatureProvider.SignAsync(data).ConfigureAwait(false);

    //        return SasTokenBuilder.BuildSasToken(audience, signature, expiresOn);
    //    }
    //}

    public interface ITokenProvider
    {
        Task<string> GetTokenAsync(Option<TimeSpan> ttl);
    }

    public class EdgeHubTokenProvider : ITokenProvider
    {
        readonly ISignatureProvider signatureProvider;
        readonly string deviceId;
        readonly string moduleId;
        readonly string iotHubHostName;
        readonly TimeSpan defaultTtl;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleAuthentication"/> class.
        /// </summary>
        public EdgeHubTokenProvider(ISignatureProvider signatureProvider, string iotHubHostName,
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
