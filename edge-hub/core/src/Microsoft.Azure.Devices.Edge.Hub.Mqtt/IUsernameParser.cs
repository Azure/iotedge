// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    public interface IUsernameParser
    {
        ClientInfo Parse(string username);
    }
}
