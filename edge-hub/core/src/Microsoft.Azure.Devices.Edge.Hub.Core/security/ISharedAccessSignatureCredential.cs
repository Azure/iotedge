// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;

    interface ISharedAccessSignatureCredential
    {
        bool IsExpired();
        DateTime ExpiryTime();
        void Authenticate(SharedAccessSignatureAuthorizationRule sasAuthorizationRule);
        void Authorize(string hostName);
        void Authorize(Uri targetAddress);
    }
}
