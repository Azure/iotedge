// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.IO;
    using System.Text;
    using Newtonsoft.Json;

    public class VersionInfo
    {        
        [JsonConstructor]
        public VersionInfo(string version, string build, string commit)
        {
            this.Version = version;
            this.Build = build;
            this.Commit = commit;
        }

        public static VersionInfo Empty { get; } = new VersionInfo(string.Empty, string.Empty, string.Empty);

        public static VersionInfo Get(string fileName)
        {
            try
            {
                Preconditions.CheckNonWhiteSpace(fileName, nameof(fileName));
                if(File.Exists(fileName))
                {
                    string fileText = File.ReadAllText(fileName);
                    return JsonConvert.DeserializeObject<VersionInfo>(fileText);
                }
            }
            catch (Exception ex) when (!ex.IsFatal()) { }
            return Empty;
        }

        public string Version { get; }

        public string Build { get; }

        public string Commit { get; }

        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(this.Version))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.Append(this.Version);

            if (!string.IsNullOrWhiteSpace(this.Build))
            {
                sb.Append($".{this.Build}");
            }

            if (!string.IsNullOrWhiteSpace(this.Commit))
            {
                sb.Append($" ({this.Commit})");
            }

            return sb.ToString();
        }
    }
}
