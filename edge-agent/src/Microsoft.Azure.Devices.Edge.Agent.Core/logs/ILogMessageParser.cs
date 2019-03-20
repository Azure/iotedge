// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    extern alias akka;
    using akka::Akka.IO;

    public interface ILogMessageParser
    {
        ModuleLogMessage Parse(ByteString byteString, string moduleId);
    }
}
