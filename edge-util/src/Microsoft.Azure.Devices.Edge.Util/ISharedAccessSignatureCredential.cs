// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    /// <summary>
    /// Credential interface used for authentication and authorization.
    /// </summary>
    internal interface ISharedAccessSignatureCredential
    {
        /// <summary>
        /// Indicates if the token has expired.
        /// </summary>
        bool IsExpired();

        /// <summary>
        /// Authenticate against the IoT Hub using an authorization rule.
        /// </summary>
        /// <param name="sasAuthorizationRule">The properties that describe the keys to access the IotHub artifacts.</param>
        void Authenticate(SharedAccessSignatureAuthorizationRule sasAuthorizationRule);
    }
}