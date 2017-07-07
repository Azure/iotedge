// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using System.Threading.Tasks;
	using System.IO;

	public class DiskFileTest
    {
		[Fact]
		[Unit]
		public async Task InvalidInputFails()
		{
			await Assert.ThrowsAsync<ArgumentException>(() => DiskFile.ReadAllAsync(""));
			await Assert.ThrowsAsync<ArgumentException>(() => DiskFile.WriteAllAsync("", "test"));
			await Assert.ThrowsAsync<ArgumentException>(() => DiskFile.WriteAllAsync("temp", ""));
		}

		[Fact]
		[Unit]
		public async Task ReadMatchesWrite()
		{
			string tempFileName = Path.GetTempFileName();
			string written = "edge hub content";
			await DiskFile.WriteAllAsync(tempFileName, written);
			string content = await DiskFile.ReadAllAsync(tempFileName);
			File.Delete(tempFileName);
			Assert.True(written == content);
		}

		[Fact]
		[Unit]
		public async Task OverwriteSuccess()
		{
			string tempFileName = Path.GetTempFileName();
			string written = "edge hub content";
			await DiskFile.WriteAllAsync(tempFileName, written);
			string content = await DiskFile.ReadAllAsync(tempFileName);
			Assert.True(content.Length == written.Length);
			Assert.True(written == content);

			written = "edge hub";
			await DiskFile.WriteAllAsync(tempFileName, written);
			content = await DiskFile.ReadAllAsync(tempFileName);
			Assert.True(content.Length == written.Length);
			Assert.True(written == content);

			File.Delete(tempFileName);
		}
    }
}
