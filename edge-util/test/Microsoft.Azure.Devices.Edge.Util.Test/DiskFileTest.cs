// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class DiskFileTest : IDisposable
    {
        readonly string tempFileName;

        public DiskFileTest()
        {
            this.tempFileName = Path.GetTempFileName();
        }

        public void Dispose()
        {
            if (!string.IsNullOrEmpty(this.tempFileName) && File.Exists(this.tempFileName))
            {
                File.Delete(this.tempFileName);
            }
        }

        [Fact]
        [Unit]
        public async Task InvalidInputFails()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => DiskFile.ReadAllAsync(string.Empty));
            await Assert.ThrowsAsync<ArgumentException>(() => DiskFile.WriteAllAsync(string.Empty, "test"));
            await Assert.ThrowsAsync<ArgumentException>(() => DiskFile.WriteAllAsync("temp", string.Empty));
        }

        [Fact]
        [Unit]
        public async Task ReadMatchesWrite()
        {
            string written = "edge hub content";
            await DiskFile.WriteAllAsync(this.tempFileName, written);
            string content = await DiskFile.ReadAllAsync(this.tempFileName);
            Assert.True(written == content);
        }

        [Fact]
        [Unit]
        public async Task OverwriteSuccess()
        {
            string written = "edge hub content";
            await DiskFile.WriteAllAsync(this.tempFileName, written);
            string content = await DiskFile.ReadAllAsync(this.tempFileName);
            Assert.True(content.Length == written.Length);
            Assert.True(written == content);

            written = "edge hub";
            await DiskFile.WriteAllAsync(this.tempFileName, written);
            content = await DiskFile.ReadAllAsync(this.tempFileName);
            Assert.True(content.Length == written.Length);
            Assert.True(written == content);
        }

        [Unit]
        [Fact]
        public async Task TimeoutWriteTest()
        {
            // Arrange
            string testString = new string('*', 5000000);
            TimeSpan timeout = TimeSpan.Zero;

            // Act / Assert
            await Assert.ThrowsAsync<TimeoutException>(() => DiskFile.WriteAllAsync(this.tempFileName, testString, timeout));

            // To allow for the write operation to finish so that the file can be cleaned up
            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        [Unit]
        [Fact]
        public async Task TimeoutReadTest()
        {
            // Arrange
            string testString = new string('*', 5000000);
            await DiskFile.WriteAllAsync(this.tempFileName, testString);
            TimeSpan timeout = TimeSpan.Zero;

            // Assert
            await Assert.ThrowsAsync<TimeoutException>(() => DiskFile.ReadAllAsync(this.tempFileName, timeout));

            // To allow for the write operation to finish so that the file can be cleaned up
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}
