namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    using System.Linq;
    using System.Text;

    public static class StringExtensions
    {
        public static string RemoveWhitespace(this string input) =>
            new string(input.Where(ch => !char.IsWhiteSpace(ch)).ToArray());

        public static string SingleToDoubleQuotes(this string input) => input.Replace('\'', '"');

        public static byte[] ToBody(this string input) =>
            Encoding.UTF8.GetBytes(input.RemoveWhitespace().SingleToDoubleQuotes());
    }
}