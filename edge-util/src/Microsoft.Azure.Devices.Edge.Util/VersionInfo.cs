// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Newtonsoft.Json;

    public class VersionInfo : IEquatable<VersionInfo>
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
                if (File.Exists(fileName))
                {
                    string fileText = File.ReadAllText(fileName);
                    return JsonConvert.DeserializeObject<VersionInfo>(fileText);
                }
            }
            catch (Exception ex) when (!ex.IsFatal()) { }
            return Empty;
        }

        [JsonProperty(PropertyName = "version")]
        public string Version { get; }

        [JsonProperty(PropertyName = "build")]
        public string Build { get; }

        [JsonProperty(PropertyName = "commit")]
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

        public override bool Equals(object obj) => this.Equals(obj as VersionInfo);

        public bool Equals(VersionInfo other) =>
            other != null
            &&
            this.Version == other.Version
            &&
            this.Build == other.Build
            &&
            this.Commit == other.Commit;

        public override int GetHashCode()
        {
            int hashCode = -2113050370;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.Version);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.Build);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.Commit);
            return hashCode;
        }

        public static bool operator ==(VersionInfo info1, VersionInfo info2) => EqualityComparer<VersionInfo>.Default.Equals(info1, info2);

        public static bool operator !=(VersionInfo info1, VersionInfo info2) => !(info1 == info2);
    }
}
