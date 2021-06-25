// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    extern alias akka;
    using akka::Akka.IO;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface ILogMessageParser
    {
        ModuleLogMessageData Parse(ByteString byteString, string moduleId);
    }
}
