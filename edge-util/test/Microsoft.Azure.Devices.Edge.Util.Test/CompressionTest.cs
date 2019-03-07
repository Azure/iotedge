// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class CompressionTest
    {
        const string TestCompressionString = @"[
  {
    ""IoTHub"": ""OfflineTestHub1"",
    ""DeviceId"": ""d101"",
    ""ModuleId"": ""edgeAgent"",
    ""Stream"": ""stdout"",
    ""LogLevel"": 6,
    ""Text"": ""[2019-02-16 07:07:57 : Starting Edge Agent\n""
  },
  {
    ""IoTHub"": ""OfflineTestHub1"",
    ""DeviceId"": ""d101"",
    ""ModuleId"": ""edgeAgent"",
    ""Stream"": ""stdout"",
    ""LogLevel"": 6,
    ""Text"": ""[02/16/2019 07:07:57.630 AM] Edge Agent Main()\n""
  },
  {
    ""IoTHub"": ""OfflineTestHub1"",
    ""DeviceId"": ""d101"",
    ""ModuleId"": ""edgeAgent"",
    ""Stream"": ""stdout"",
    ""LogLevel"": 6,
    ""Text"": ""2019-02-16 07:07:57.866 +00:00 [INF] - Starting module management agent.\n""
  },
  {
    ""IoTHub"": ""OfflineTestHub1"",
    ""DeviceId"": ""d101"",
    ""ModuleId"": ""edgeAgent"",
    ""Stream"": ""stdout"",
    ""LogLevel"": 6,
    ""Text"": ""2019-02-16 07:07:58.068 +00:00 [INF] - Version - 1.0.6.19913336 (8288bc9bd6f6e15295fea506cd3f99d7f6347a6a)\n""
  },
  {
    ""IoTHub"": ""OfflineTestHub1"",
    ""DeviceId"": ""d101"",
    ""ModuleId"": ""edgeAgent"",
    ""Stream"": ""stdout"",
    ""LogLevel"": 6,
    ""Text"": ""2019-02-16 07:07:58.069 +00:00 [INF] - \n""
  },
  {
    ""IoTHub"": ""OfflineTestHub1"",
    ""DeviceId"": ""d101"",
    ""ModuleId"": ""edgeAgent"",
    ""Stream"": ""stdout"",
    ""LogLevel"": 6,
    ""Text"": ""        █████╗ ███████╗██╗   ██╗██████╗ ███████╗\n""
  },
  {
    ""IoTHub"": ""OfflineTestHub1"",
    ""DeviceId"": ""d101"",
    ""ModuleId"": ""edgeAgent"",
    ""Stream"": ""stdout"",
    ""LogLevel"": 6,
    ""Text"": ""       ██╔══██╗╚══███╔╝██║   ██║██╔══██╗██╔════╝\n""
  },
  {
    ""IoTHub"": ""OfflineTestHub1"",
    ""DeviceId"": ""d101"",
    ""ModuleId"": ""edgeAgent"",
    ""Stream"": ""stdout"",
    ""LogLevel"": 6,
    ""Text"": ""       ███████║  ███╔╝ ██║   ██║██████╔╝█████╗\n""
  },
  {
    ""IoTHub"": ""OfflineTestHub1"",
    ""DeviceId"": ""d101"",
    ""ModuleId"": ""edgeAgent"",
    ""Stream"": ""stdout"",
    ""LogLevel"": 6,
    ""Text"": ""       ██╔══██║ ███╔╝  ██║   ██║██╔══██╗██╔══╝\n""
  },
  {
    ""IoTHub"": ""OfflineTestHub1"",
    ""DeviceId"": ""d101"",
    ""ModuleId"": ""edgeAgent"",
    ""Stream"": ""stdout"",
    ""LogLevel"": 6,
    ""Text"": ""       ██║  ██║███████╗╚██████╔╝██║  ██║███████╗\n""
  },
  {
    ""IoTHub"": ""OfflineTestHub1"",
    ""DeviceId"": ""d101"",
    ""ModuleId"": ""edgeAgent"",
    ""Stream"": ""stdout"",
    ""LogLevel"": 6,
    ""Text"": ""       ╚═╝  ╚═╝╚══════╝ ╚═════╝ ╚═╝  ╚═╝╚══════╝\n""
  },
  {
    ""IoTHub"": ""OfflineTestHub1"",
    ""DeviceId"": ""d101"",
    ""ModuleId"": ""edgeAgent"",
    ""Stream"": ""stdout"",
    ""LogLevel"": 6,
    ""Text"": ""\n""
  },
  {
    ""IoTHub"": ""OfflineTestHub1"",
    ""DeviceId"": ""d101"",
    ""ModuleId"": ""edgeAgent"",
    ""Stream"": ""stdout"",
    ""LogLevel"": 6,
    ""Text"": "" ██╗ ██████╗ ████████╗    ███████╗██████╗  ██████╗ ███████╗\n""
  },
  {
    ""IoTHub"": ""OfflineTestHub1"",
    ""DeviceId"": ""d101"",
    ""ModuleId"": ""edgeAgent"",
    ""Stream"": ""stdout"",
    ""LogLevel"": 6,
    ""Text"": "" ██║██╔═══██╗╚══██╔══╝    ██╔════╝██╔══██╗██╔════╝ ██╔════╝\n""
  },
  {
    ""IoTHub"": ""OfflineTestHub1"",
    ""DeviceId"": ""d101"",
    ""ModuleId"": ""edgeAgent"",
    ""Stream"": ""stdout"",
    ""LogLevel"": 6,
    ""Text"": "" ██║██║   ██║   ██║       █████╗  ██║  ██║██║  ███╗█████╗\n""
  },
  {
    ""IoTHub"": ""OfflineTestHub1"",
    ""DeviceId"": ""d101"",
    ""ModuleId"": ""edgeAgent"",
    ""Stream"": ""stdout"",
    ""LogLevel"": 6,
    ""Text"": "" ██║██║   ██║   ██║       ██╔══╝  ██║  ██║██║   ██║██╔══╝\n""
  },
  {
    ""IoTHub"": ""OfflineTestHub1"",
    ""DeviceId"": ""d101"",
    ""ModuleId"": ""edgeAgent"",
    ""Stream"": ""stdout"",
    ""LogLevel"": 6,
    ""Text"": "" ██║╚██████╔╝   ██║       ███████╗██████╔╝╚██████╔╝███████╗\n""
  },
  {
    ""IoTHub"": ""OfflineTestHub1"",
    ""DeviceId"": ""d101"",
    ""ModuleId"": ""edgeAgent"",
    ""Stream"": ""stdout"",
    ""LogLevel"": 6,
    ""Text"": "" ╚═╝ ╚═════╝    ╚═╝       ╚══════╝╚═════╝  ╚═════╝ ╚══════╝\n""
  },
  {
    ""IoTHub"": ""OfflineTestHub1"",
    ""DeviceId"": ""d101"",
    ""ModuleId"": ""edgeAgent"",
    ""Stream"": ""stdout"",
    ""LogLevel"": 6,
    ""Text"": ""\n""
  }
]";

        [Unit]
        [Fact]
        public static void CompressionRoundtripTest()
        {
            // Arrange
            byte[] payload = Encoding.UTF8.GetBytes(TestCompressionString);

            // Act
            byte[] compressedBytes = Compression.CompressToGzip(payload);

            // Assert
            Assert.NotNull(compressedBytes);
            Assert.True(payload.Length > compressedBytes.Length);

            // Act
            byte[] decompressedBytes = Compression.DecompressFromGzip(compressedBytes);

            // Assert
            Assert.NotNull(decompressedBytes);
            Assert.Equal(decompressedBytes.Length, payload.Length);
            string decompressedPayload = Encoding.UTF8.GetString(decompressedBytes);
            Assert.Equal(decompressedPayload, TestCompressionString);
        }
    }
}
