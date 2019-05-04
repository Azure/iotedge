// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Blob
{
    using System.Threading.Tasks;

    public interface IAzureBlob
    {
        string Name { get; }

        Task UploadFromByteArrayAsync(byte[] bytes);
    }
}
