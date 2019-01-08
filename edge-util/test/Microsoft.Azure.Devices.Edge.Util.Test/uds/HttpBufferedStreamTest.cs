// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test.Uds
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util.Uds;
    using Xunit;

    [Unit]
    public class HttpBufferedStreamTest
    {
        [Fact]
        public async Task TestReadLines_ShouldReturnResponse()
        {
            string expected = "GET /modules/testModule/sign?api-version=2018-06-28 HTTP/1.1\r\nHost: localhost:8081\r\nConnection: close\r\nContent-Type: application/json\r\nContent-Length: 100\r\n\r\n";

            byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
            var memory = new MemoryStream(expectedBytes, true);

            IList<string> lines = new List<string>();
            var buffered = new HttpBufferedStream(memory);
            CancellationToken cancellationToken = default(CancellationToken);
            string line = await buffered.ReadLineAsync(cancellationToken);

            while (!string.IsNullOrEmpty(line))
            {
                lines.Add(line);
                line = await buffered.ReadLineAsync(cancellationToken);
            }

            Assert.Equal(5, lines.Count);
            Assert.Equal("GET /modules/testModule/sign?api-version=2018-06-28 HTTP/1.1", lines[0]);
            Assert.Equal("Host: localhost:8081", lines[1]);
            Assert.Equal("Connection: close", lines[2]);
            Assert.Equal("Content-Type: application/json", lines[3]);
            Assert.Equal("Content-Length: 100", lines[4]);
        }
    }
}
