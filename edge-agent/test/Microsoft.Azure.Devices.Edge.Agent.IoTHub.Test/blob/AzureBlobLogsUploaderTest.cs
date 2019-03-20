// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test.Blob
{
    using System;
    using System.Globalization;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.Blob;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Moq;
    using Xunit;
    using Match = System.Text.RegularExpressions.Match;

    [Unit]
    public class AzureBlobLogsUploaderTest
    {
        const string BlobNameRegexPattern = @"(?<iothub>.*)/(?<deviceid>.*)/(?<id>.*)-(?<timestamp>\d{4}-\d{2}-\d{2}--\d{2}-\d{2}-\d{2}).(?<extension>\w{2})";

        [Theory]
        [InlineData(LogsContentEncoding.Gzip, LogsContentType.Json, "gz")]
        [InlineData(LogsContentEncoding.Gzip, LogsContentType.Text, "gz")]
        [InlineData(LogsContentEncoding.None, LogsContentType.Json, "json")]
        [InlineData(LogsContentEncoding.None, LogsContentType.Text, "log")]
        public void GetExtensionTest(LogsContentEncoding contentEncoding, LogsContentType contentType, string expectedExtension)
        {
            Assert.Equal(expectedExtension, AzureBlobLogsUploader.GetExtension(contentEncoding, contentType));
        }

        [Fact]
        public void GetBlobNameTest()
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "abcd";
            string id = "pqr";
            string extension = "gz";

            var regex = new Regex(BlobNameRegexPattern);

            var azureBlobLogsUploader = new AzureBlobLogsUploader(iotHub, deviceId, Mock.Of<IAzureBlobUploader>());

            // Act
            string blobName = azureBlobLogsUploader.GetBlobName(id, LogsContentEncoding.Gzip, LogsContentType.Json);

            // Assert
            Assert.NotNull(blobName);
            Match match = regex.Match(blobName);
            Assert.True(match.Success);
            string receivedIotHub = match.Groups["iothub"].Value;
            string receivedDeviceId = match.Groups["deviceid"].Value;
            string receivedId = match.Groups["id"].Value;
            string receivedTimestamp = match.Groups["timestamp"].Value;
            string receivedExtension = match.Groups["extension"].Value;
            Assert.Equal(id, receivedId);
            Assert.Equal(iotHub, receivedIotHub);
            Assert.Equal(deviceId, receivedDeviceId);
            Assert.Equal(extension, receivedExtension);
            Assert.True(DateTime.UtcNow - DateTime.ParseExact(receivedTimestamp, "yyyy-MM-dd--HH-mm-ss", CultureInfo.InvariantCulture) < TimeSpan.FromSeconds(10));
        }

        [Fact]
        public async Task UploadTest()
        {
            // Arrange
            string iotHub = "foo.azure-devices.net";
            string deviceId = "abcd";
            string id = "pqr";
            string sasUri = @"http://testuri/";
            var regex = new Regex(BlobNameRegexPattern);

            string receivedBlobName = null;
            Uri receivedSasUri = null;
            byte[] receivedPayload = null;

            byte[] payload = Encoding.UTF8.GetBytes("Test payload string");

            var azureBlob = new Mock<IAzureBlob>();
            azureBlob.Setup(a => a.BlobProperties)
                .Returns(new BlobProperties());
            azureBlob.Setup(a => a.Name)
                .Returns(() => receivedBlobName);
            azureBlob.Setup(a => a.UploadFromByteArrayAsync(payload))
                .Callback<byte[]>(b => receivedPayload = b)
                .Returns(Task.CompletedTask);

            var azureBlobUploader = new Mock<IAzureBlobUploader>();
            azureBlobUploader.Setup(a => a.GetBlob(It.IsAny<Uri>(), It.IsAny<string>()))
                .Callback<Uri, string>((u, b) =>
                {
                    receivedSasUri = u;
                    receivedBlobName = b;
                })
                .Returns(azureBlob.Object);

            var azureBlobLogsUploader = new AzureBlobLogsUploader(iotHub, deviceId, azureBlobUploader.Object);

            // Act
            await azureBlobLogsUploader.Upload(sasUri, id, payload, LogsContentEncoding.Gzip, LogsContentType.Json);

            // Assert
            Assert.NotNull(receivedBlobName);
            Match match = regex.Match(receivedBlobName);
            Assert.True(match.Success);
            Assert.NotNull(receivedSasUri);
            Assert.Equal(sasUri, receivedSasUri.ToString());
            Assert.NotNull(receivedPayload);
            Assert.Equal(payload, receivedPayload);
        }
    }
}
