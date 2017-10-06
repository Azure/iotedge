namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public static class StringExtensions
    {
        public static string RemoveWhitespace(this string input) =>
            new string(input.Where(ch => !char.IsWhiteSpace(ch)).ToArray());

        public static string SingleToDoubleQuotes(this string input) => input.Replace('\'', '"');

        public static byte[] ToBody(this string input) =>
            Encoding.UTF8.GetBytes(input.RemoveWhitespace().SingleToDoubleQuotes());

        public const string LowerCaseAlphabet = "abcdefghijklmnopqrstuvwyxz";

        public static string GenerateUniqueString(IEnumerable<string> existingStrings, int size, Random rng) =>
            GenerateUniqueString(existingStrings, size, rng, LowerCaseAlphabet);

        public static string GenerateUniqueString(IEnumerable<string> existingStrings, int size, Random rng, string alphabet)
        {
            string str;
            do
            {
                str = GenerateString(size, rng, alphabet);
            } while (existingStrings.Contains(str));

            return str;
        }

        /// <summary>
        /// Taken from https://stackoverflow.com/a/976674.
        /// </summary>
        public static string GenerateString(int size, Random rng, string alphabet)
        {
            char[] chars = new char[size];
            for (int i = 0; i < size; i++)
            {
                chars[i] = alphabet[rng.Next(alphabet.Length)];
            }

            return new string(chars);
        }
    }
}