// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Nito.AsyncEx;

    public class ClientTokenProvider : ITokenProvider
    {
        readonly ISignatureProvider signatureProvider;
        readonly string deviceId;
        readonly Option<string> moduleId;
        readonly string iotHubHostName;
        readonly TimeSpan defaultTtl;
        readonly TokenCache tokenCache;
        readonly AsyncLock cacheUpdateLock = new AsyncLock();

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
            this.tokenCache = new TokenCache(defaultTtl / 2);
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
            return await this.tokenCache.GetToken().Match(
                t => Task.FromResult(t),
                async () =>
                {
                    using (await this.cacheUpdateLock.LockAsync())
                    {
                        return await this.tokenCache.GetToken().Match(
                            t => Task.FromResult(t),
                            async () =>
                            {
                                DateTime startTime = DateTime.UtcNow;
                                TimeSpan ttlVal = ttl.GetOrElse(this.defaultTtl);
                                DateTime expiryTime = startTime.Add(ttlVal);
                                string tokenVal = await this.GetTokenAsyncInternal(startTime, ttlVal);
                                this.tokenCache.UpdateToken(tokenVal, expiryTime);
                                return tokenVal;
                            });
                    }
                });
        }

        internal async Task<string> GetTokenAsyncInternal(DateTime startTime, TimeSpan ttl)
        {
            string audience = this.moduleId
                .Map(m => SasTokenHelper.BuildAudience(this.iotHubHostName, this.deviceId, m))
                .GetOrElse(() => SasTokenHelper.BuildAudience(this.iotHubHostName, this.deviceId));
            string expiresOn = SasTokenHelper.BuildExpiresOn(startTime, ttl);
            string data = string.Join(
                "\n",
                new List<string>
                {
                    audience,
                    expiresOn
                });
            try
            {
                string signature = await this.signatureProvider.SignAsync(data);
                return SasTokenHelper.BuildSasToken(audience, signature, expiresOn);
            }
            catch (SignatureProviderException e)
            {
                throw new TokenProviderException(e);
            }
        }

        class TokenCache
        {
            readonly TimeSpan expiryBuffer;

            Option<string> token;
            DateTime tokenExpiry;

            public TokenCache(TimeSpan expiryBuffer)
            {
                this.expiryBuffer = expiryBuffer;
            }

            public void UpdateToken(string token, DateTime tokenExpiry)
            {
                this.token = Option.Maybe(token);
                this.tokenExpiry = tokenExpiry;
            }

            public Option<string> GetToken()
            {
                if (this.tokenExpiry - DateTime.UtcNow > this.expiryBuffer)
                {
                    return this.token;
                }

                return Option.None<string>();
            }
        }
    }
}
