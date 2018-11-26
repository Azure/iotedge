// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;

    using Microsoft.Azure.Devices.Common.Security;

    class TokenHelper
    {
        public static DateTime GetTokenExpiry(string hostName, string token)
        {
            try
            {
                SharedAccessSignature sharedAccessSignature = SharedAccessSignature.Parse(hostName, token);
                DateTime expiryTime = sharedAccessSignature.ExpiresOn.ToUniversalTime();
                return expiryTime;
            }
            catch (UnauthorizedAccessException)
            {
                return DateTime.MinValue;
            }
        }

        public static TimeSpan GetTokenExpiryTimeRemaining(string hostName, string token) => GetTokenExpiry(hostName, token) - DateTime.UtcNow;

        public static bool IsTokenExpired(string hostName, string token)
        {
            try
            {
                SharedAccessSignature sharedAccessSignature = SharedAccessSignature.Parse(hostName, token);
                return sharedAccessSignature.IsExpired();
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }
    }
}
