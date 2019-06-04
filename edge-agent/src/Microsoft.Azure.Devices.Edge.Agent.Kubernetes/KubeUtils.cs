// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Linq;
    using System.Text;
    using CoreConstants = Microsoft.Azure.Devices.Edge.Agent.Core.Constants;

    public static class KubeUtils
    {
        const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string AllowedCharsLabelValues = "-._";
        const string AllowedCharsDns = "-";
        const string AllowedCharsGeneric = "-.";
        const int MaxK8SValueLength = 253;
        const int MaxDnsNameLength = 63;
        const int MaxLabelValueLength = 63;

        static bool IsAlpha(char ch) => Alphabet.IndexOf(ch) != -1;

        static bool IsAlphaNumeric(char ch) => IsAlpha(ch) || Char.IsDigit(ch);

        public static readonly string K8sNamespace = Constants.k8sNamespaceBaseName;

        static KubeUtils()
        {
            // build the k8s namespace to which all things belong
            string nsBaseName = Environment.GetEnvironmentVariable(Constants.k8sNamespaceBaseName);
            string deviceId = Environment.GetEnvironmentVariable(CoreConstants.DeviceIdVariableName);
            string hubName = Environment.GetEnvironmentVariable(CoreConstants.IotHubHostnameVariableName);
            if (hubName != null && deviceId != null && nsBaseName != null)
            {
                hubName = hubName.Split('.').FirstOrDefault() ?? hubName;

                K8sNamespace = $"{nsBaseName}-{hubName.ToLower()}-{deviceId.ToLower()}";
            }
        }

        // Valid annotation keys have two segments: an optional prefix and name, separated by a slash (/). 
        // The name segment is required and must be 63 characters or less, beginning and ending with an 
        // alphanumeric character ([a-z0-9A-Z]) with dashes (-), underscores (_), dots (.), and alphanumerics between. 
        // The prefix is optional. If specified, the prefix must be a DNS subdomain
        public static string SanitizeAnnotationKey(string key)
        {
            char[] annotationSplit = { '/' };
            string[] keySegments = key.Split(annotationSplit, 2);
            if (keySegments.Count() == 2)
            {
                return SanitizeDNSDomain(keySegments[0]) + "/" + SanitizeNameValue(keySegments[1]);
            }
            else
            {
                return SanitizeNameValue(key);
            }
        }

        // Alphanumeric, '-' and '.' up to 253 characters.
        public static string SanitizeK8sValue(string name)
        {
            name = name.ToLower();

            var output = new StringBuilder();
            for (int i = 0, len = 0; i < name.Length && len < MaxK8SValueLength; i++)
            {
                if (IsAlphaNumeric(name[i]) ||
                    (len < MaxK8SValueLength-1 && AllowedCharsGeneric.IndexOf(name[i]) != -1))
                {
                    output.Append(name[i]);
                    len++;
                }
            }

            return output.ToString();
        }

        // DNS label (as per RFC 1035)
        public static string SanitizeDNSValue(string name, int maxLength = MaxDnsNameLength)
        {
            // The name returned from here must conform to following rules (as per RFC 1035):
            //  - length must be <= 63 characters
            //  - must be all lower case alphanumeric characters or '-'
            //  - must start with an alphabet
            //  - must end with an alphanumeric character

            name = name.ToLower();

            // get index of first character from the left that is an alphabet
            int start = 0;
            while (start < name.Length && !IsAlpha(name[start]))
            {
                start++;
            }

            // get index of last character from right that's an alphanumeric
            int end = Math.Max(start, name.Length - 1);
            while (end > start && !IsAlphaNumeric(name[end]))
            {
                end--;
            }

            // build a new string from start-end (inclusive) excluding characters
            // that aren't alphanumeric or the symbol '-'
            var output = new StringBuilder();
            for (int i = start, len = 0; i <= end && len < maxLength; i++)
            {
                if (IsAlphaNumeric(name[i]) ||
                    (len < maxLength-1 && AllowedCharsDns.IndexOf(name[i]) != -1))
                {
                    output.Append(name[i]);
                    len++;
                }
            }

            return output.ToString();
        }

        // DNS subdomains are DNS labels separated by '.', max 253 characters.
        public static string SanitizeDNSDomain(string name)
        {
            int maxRemaining = MaxK8SValueLength;
            char[] nameSplit = { '.' };
            string[] dnsSegments = name.Split(nameSplit);
            var output = new StringBuilder();
            bool firstSegment = true;
            foreach (var segment in dnsSegments)
            {
                string sanitized = SanitizeDNSValue(segment, Math.Min(MaxDnsNameLength, maxRemaining));
                if (sanitized == string.Empty)
                {
                    continue;
                }

                if (firstSegment)
                {
                    output.Append(sanitized);
                    firstSegment = false;
                    maxRemaining -= sanitized.Length;
                }
                else
                {
                    output.Append(".");
                    output.Append(sanitized);
                    maxRemaining -= sanitized.Length + 1;
                }
            }
            return output.ToString();
        }

        private static string SanitizeNameValue(string name)
        {
            // The name returned from here must conform to following rules:
            //  - length must be <= 63 characters
            //  - must be all alphanumeric characters or ['-','.','_']
            //  - must start with an alphabet
            //  - must end with an alphanumeric character

            // get index of first character from the left that is an alphabet
            int start = 0;
            while (start < name.Length && !IsAlphaNumeric(name[start]))
            {
                start++;
            }

            // get index of last character from right that's an alphanumeric
            int end = Math.Max(start, name.Length - 1);
            while (end > start && !IsAlphaNumeric(name[end]))
            {
                end--;
            }

            // build a new string from start-end (inclusive) excluding characters
            // that aren't alphanumeric or the symbol '-'
            var output = new StringBuilder();
            for (int i = start, len = 0; i <= end && len < MaxLabelValueLength; i++)
            {
                if (IsAlphaNumeric(name[i]) ||
                    (len < MaxLabelValueLength-1 && AllowedCharsLabelValues.IndexOf(name[i]) != -1))
                {
                    output.Append(name[i]);
                    len++;
                }
            }

            return output.ToString();
        }

        public static string SanitizeLabelValue(string name)
        {
            // The name returned from here must conform to following rules:
            //  - length must be <= 63 characters
            //  - must be all lower case alphanumeric characters or ['-','.','_']
            //  - must start with an alphabet
            //  - must end with an alphanumeric character

            return SanitizeNameValue(name.ToLower());
        }
    }
}
