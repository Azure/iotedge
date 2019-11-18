namespace MetricsCollector
{
    using System;

    public static class SchemaVersionHelper
    {
        public static int CompareMajorVersion(this Version expectedVersion, string actualVersionString, string context)
        {
            if (string.IsNullOrWhiteSpace(actualVersionString) || !Version.TryParse(actualVersionString, out Version version))
            {
                throw new InvalidOperationException($"Invalid {context} version {actualVersionString ?? string.Empty}");
            }

            if (expectedVersion.Major != version.Major)
            {
                throw new InvalidOperationException($"The {context} version {actualVersionString} is not compatible with the expected version {expectedVersion}");
            }

            return expectedVersion.CompareTo(version);
        }
    }
}