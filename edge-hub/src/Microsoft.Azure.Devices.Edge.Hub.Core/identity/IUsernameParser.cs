// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    public interface IUsernameParser
    {
        (string deviceId, string moduleId, string deviceClientType) Parse(string username);
    }
}
