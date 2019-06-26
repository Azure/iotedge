// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using CoreConstants = Microsoft.Azure.Devices.Edge.Agent.Core.Constants;

    public static class KubeUtils
    {
        const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string Numeric = "0123456789";
        const string AllowedCharsLabelValues = "-._";
        const string AllowedCharsDns = "-";
        const string AllowedCharsGeneric = "-.";
        const int MaxK8SValueLength = 253;
        const int MaxDnsNameLength = 63;
        const int MaxLabelValueLength = 63;
        static readonly HashSet<char> AlphaHashSet;
        static readonly HashSet<char> AlphaNumericHashSet;
        static readonly HashSet<char> AllowedLabelsHashSet;
        static readonly HashSet<char> AllowedDnsHashSet;
        static readonly HashSet<char> AllowedGenericHashSet;

        static bool IsAlpha(char ch) => AlphaHashSet.Contains(ch);

        static bool IsAlphaNumeric(char ch) => AlphaNumericHashSet.Contains(ch);

        static KubeUtils()
        {
            AlphaHashSet = new HashSet<char>();
            foreach (char c in Alphabet)
            {
                AlphaHashSet.Add(c);
            }
            AlphaNumericHashSet = new HashSet<char>(AlphaHashSet);
            foreach (char c in Numeric)
            {
                AlphaNumericHashSet.Add(c);
            }
            AllowedLabelsHashSet = new HashSet<char>(AlphaNumericHashSet);
            foreach (char c in AllowedCharsLabelValues)
            {
                AllowedLabelsHashSet.Add(c);
            }
            AllowedDnsHashSet = new HashSet<char>(AlphaNumericHashSet);
            foreach (char c in AllowedCharsDns)
            {
                AllowedDnsHashSet.Add(c);
            }
            AllowedGenericHashSet = new HashSet<char>(AlphaNumericHashSet);
            foreach (char c in AllowedCharsGeneric)
            {
                AllowedGenericHashSet.Add(c);
            }
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
                if (AllowedGenericHashSet.Contains(name[i]))
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
                if (AllowedDnsHashSet.Contains(name[i]))
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
                if (AllowedLabelsHashSet.Contains(name[i]))
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
