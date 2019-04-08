// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System;
    using System.Threading.Tasks;

    public interface ILogsUploader
    {
        Task Upload(string uri, string module, byte[] payload, LogsContentEncoding logsContentEncoding, LogsContentType logsContentType);

        Func<byte[], Task> GetUploaderCallback(string uri, string module, LogsContentEncoding logsContentEncoding, LogsContentType logsContentType);
    }

    public interface ILogsUploaderInstance
    {
        Task Append(byte[] bytes);
    }
}
