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


        static KubeUtils()
        {
        }

        // Valid annotation keys have two segments: an optional prefix and name, separated by a slash (/). 
        // The name segment is required and must be 63 characters or less, beginning and ending with an 
        // alphanumeric character ([a-z0-9A-Z]) with dashes (-), underscores (_), dots (.), and alphanumerics between. 
        // The prefix is optional. If specified, the prefix must be a DNS subdomain
        public static string SanitizeAnnotationKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new InvalidKubernetesNameException($"Key '{key}' is null or empty");
            }

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
            if (name == null)
            {
                // Values sometimes may be null, and that's OK.
                return name;
            }

            name = name.ToLower();

            var output = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (IsAlphaNumeric(name[i]) || AllowedCharsGeneric.IndexOf(name[i]) != -1)
                {
                    output.Append(name[i]);
                }
            }

            if (output.Length > MaxK8SValueLength)
            {
                throw new InvalidKubernetesNameException($"Value '{name}' as sanitized exceeded maximum length {MaxK8SValueLength}");
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
            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidKubernetesNameException($"Name '{name}' is null or empty");
            }

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
            for (int i = start; i <= end; i++)
            {
                if (IsAlphaNumeric(name[i]) || AllowedCharsDns.IndexOf(name[i]) != -1)
                {
                    output.Append(name[i]);
                }
            }
            if (output.Length > maxLength)
            {
                throw new InvalidKubernetesNameException($"DNS name '{name}' exceeded maximum length of {maxLength}");
            }

            return output.ToString();
        }

        // DNS subdomains are DNS labels separated by '.', max 253 characters.
        public static string SanitizeDNSDomain(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidKubernetesNameException($"Name '{name}' is null or empty");
            }

            char[] nameSplit = { '.' };
            string[] dnsSegments = name.Split(nameSplit);
            var output = new StringBuilder();
            bool firstSegment = true;
            foreach (var segment in dnsSegments)
            {
                if (segment == string.Empty)
                {
                    continue;
                }
                string sanitized = SanitizeDNSValue(segment);
                if (sanitized == string.Empty)
                {
                    continue;
                }

                if (firstSegment)
                {
                    output.Append(sanitized);
                    firstSegment = false;
                }
                else
                {
                    output.Append(".");
                    output.Append(sanitized);
                }
            }

            if (output.Length > MaxK8SValueLength)
            {
                throw new InvalidKubernetesNameException($"DNS subdomain '{name}' as sanitized exceeded maximum length of {MaxK8SValueLength}");
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
            for (int i = start; i <= end; i++)
            {
                if (IsAlphaNumeric(name[i]) || AllowedCharsLabelValues.IndexOf(name[i]) != -1)
                {
                    output.Append(name[i]);
                }
            }
            if (output.Length > MaxLabelValueLength)
            {
                throw new InvalidKubernetesNameException($"Name '{name}' exceeded maximum length of {MaxLabelValueLength}");
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
            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidKubernetesNameException($"Name '{name}' is null or empty");
            }
            return SanitizeNameValue(name.ToLower());
        }
    }
}
