// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    public interface IRequestsUploader
    {
        Task UploadLogs(string uri, string module, byte[] payload, LogsContentEncoding logsContentEncoding, LogsContentType logsContentType);

        Task<Func<ArraySegment<byte>, Task>> GetLogsUploaderCallback(string uri, string module, LogsContentEncoding logsContentEncoding, LogsContentType logsContentType);

        Task UploadSupportBundle(string uri, Stream source);
    }
}
