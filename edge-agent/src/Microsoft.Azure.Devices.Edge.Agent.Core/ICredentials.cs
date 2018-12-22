// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    public interface ICredentials
    {
        CredentialType CredentialsType { get; }
    }
}
