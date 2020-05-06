// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    public class TempDirectory : IDisposable
    {
        List<string> dirs = new List<string>();

        public string CreateTempDir()
        {
            string newDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(newDir);
            this.dirs.Add(newDir);

            return newDir;
        }

        public void Dispose()
        {
            foreach (var dir in this.dirs)
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
