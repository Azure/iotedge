// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    public enum DeploymentStatusCode
    {
        Successful = 200,
        ConfigFormatError = 400,
        ConfigEmptyError = 417,
        InvalidSchemaVersion = 412,
        Unknown = 406,
        Failed = 500
    }
}
