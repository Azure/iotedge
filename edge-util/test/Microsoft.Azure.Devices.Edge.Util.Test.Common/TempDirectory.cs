using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    public class TempDirectory : IDisposable
    {
        private List<string> dirs = new List<string>();

        protected string GetTempDir()
        {
            string newDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(newDir);
            dirs.Add(newDir);

            return newDir;
        }

        public void Dispose()
        {
            foreach (var dir in dirs)
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
