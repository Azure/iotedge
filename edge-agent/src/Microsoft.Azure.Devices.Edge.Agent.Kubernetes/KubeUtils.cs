// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Text;

    public static class KubeUtils
    {
        const string LOWER_ASCII = "abcdefghijklmnopqrstuvwxyz";
        const string ALLOWED_CHARS_LABEL_VALUES = "-._";
        const string ALLOWED_CHARS_DNS = "-";
        const string ALLOWED_CHARS_GENERIC = "-.";
        const int MAX_K8S_VALUE_LENGTH = 253;
        const int MAX_DNS_NAME_LENGTH = 63;
        const int MAX_LABEL_VALUE_LENGTH = 63;

        static bool IsAsciiLowercase(char ch) => LOWER_ASCII.IndexOf(ch) != -1;

        static bool IsAlphaNumeric(char ch) => IsAsciiLowercase(ch) || Char.IsDigit(ch);

        public static string SanitizeK8sValue(string name)
        {
            name = name.ToLower();

            var output = new StringBuilder();
            for (int i = 0, len = 0; i < name.Length && len < MAX_K8S_VALUE_LENGTH; i++)
            {
                if (IsAlphaNumeric(name[i]) || ALLOWED_CHARS_GENERIC.IndexOf(name[i]) != -1)
                {
                    output.Append(name[i]);
                    len++;
                }
            }

            return output.ToString();
        }

        public static string SanitizeDNSValue(string name)
        {
            // The name returned from here must conform to following rules (as per RFC 1035):
            //  - length must be <= 63 characters
            //  - must be all lower case alphanumeric characters or '-'
            //  - must start with an alphabet
            //  - must end with an alphanumeric character

            name = name.ToLower();

            // get index of first character from the left that is an alphabet
            int start = 0;
            while (start < name.Length && !IsAsciiLowercase(name[start]))
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
            for (int i = start, len = 0; i <= end && len < MAX_DNS_NAME_LENGTH; i++)
            {
                if (IsAlphaNumeric(name[i]) || ALLOWED_CHARS_DNS.IndexOf(name[i]) != -1)
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

            name = name.ToLower();

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
            for (int i = start, len = 0; i <= end && len < MAX_LABEL_VALUE_LENGTH; i++)
            {
                if (IsAlphaNumeric(name[i]) || ALLOWED_CHARS_LABEL_VALUES.IndexOf(name[i]) != -1)
                {
                    output.Append(name[i]);
                    len++;
                }
            }

            return output.ToString();
        }
    }
}
