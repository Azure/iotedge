// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System.Threading.Tasks;

    public interface ILogsUploader
    {
        Task Upload(string uri, string module, byte[] payload, LogsContentEncoding logsContentEncoding, LogsContentType logsContentType);
    }
}
