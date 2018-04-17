// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.ClientWrapper.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class SasToken
    {
        public string Signature { get; private set; }

        public string Audience { get; private set; }

        public string ExpireOn { get; private set; }

        public string KeyName { get; private set; }

        private SasToken()
        {
        }

        public static SasToken Parse(string sasToken)
        {
            IEnumerable<string[]> parts = sasToken
           .Replace(SharedAccessSignatureConstants.SharedAccessSignature, "")
           .Trim()
           .Split("&")
           .Where(p => p.Trim().Length > 0)
           .Select(p => p.Split(new[] { '=' }, 2));

            IDictionary<string, string> map = parts.ToDictionary(kvp => kvp[0], (kvp) => kvp[1], StringComparer.OrdinalIgnoreCase);

            map.TryGetValue(SharedAccessSignatureConstants.SignatureFieldName, out string signature);
            map.TryGetValue(SharedAccessSignatureConstants.AudienceFieldName, out string audience);
            map.TryGetValue(SharedAccessSignatureConstants.ExpiryFieldName, out string expireOn);
            map.TryGetValue(SharedAccessSignatureConstants.KeyNameFieldName, out string keyName);

            var token = new SasToken();
            token.Signature = signature;
            token.Audience = audience;
            token.ExpireOn = expireOn;
            token.KeyName = keyName;
            return token;
        }
    }
}
