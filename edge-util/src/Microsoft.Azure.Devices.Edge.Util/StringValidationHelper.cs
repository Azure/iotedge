// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    public static class StringValidationHelper
    {
        private const char Base64Padding = '=';

        private static readonly HashSet<char> base64Table =
            new HashSet<char>
            {
                'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O',
                'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd',
                'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's',
                't', 'u', 'v', 'w', 'x', 'y', 'z', '0', '1', '2', '3', '4', '5', '6', '7',
                '8', '9', '+', '/'
            };

        public static void EnsureNullOrBase64String(string value, string paramName)
        {
            if (!IsNullOrBase64String(value))
            {
                throw new ArgumentException(CommonResources.GetString(Resources.StringIsNotBase64, value), paramName);
            }
        }

        public static bool IsNullOrBase64String(string value)
        {
            if (value == null)
            {
                return true;
            }

            return IsBase64String(value);
        }

        public static bool IsBase64String(string value)
        {
#if NETSTANDARD2_1
            value = value.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);
#else
            value = value.Replace("\r", string.Empty).Replace("\n", string.Empty);
#endif

            if (value.Length == 0 || (value.Length % 4) != 0)
            {
                return false;
            }

            var lengthNoPadding = value.Length;
            value = value.TrimEnd(Base64Padding);
            var lengthPadding = value.Length;

            if ((lengthNoPadding - lengthPadding) > 2)
            {
                return false;
            }

            foreach (char c in value)
            {
                if (!base64Table.Contains(c))
                {
                    return false;
                }
            }

            return true;
        }

        internal class Resources
        {
            private static global::System.Globalization.CultureInfo resourceCulture;

            private static global::System.Resources.ResourceManager resourceMan;

            /// <summary>
            ///   Overrides the current thread's CurrentUICulture property for all
            ///   resource lookups using this strongly typed resource class.
            /// </summary>
            [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
            internal static global::System.Globalization.CultureInfo Culture
            {
                get
                {
                    return resourceCulture;
                }
                set
                {
                    resourceCulture = value;
                }
            }

            /// <summary>
            ///   Returns the cached ResourceManager instance used by this class.
            /// </summary>
            [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
            internal static global::System.Resources.ResourceManager ResourceManager
            {
                get
                {
                    if (resourceMan is null)
                    {
                        global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Microsoft.Azure.Devices.Common.Resources", typeof(Resources).GetTypeInfo().Assembly);
                        resourceMan = temp;
                    }
                    return resourceMan;
                }
            }

            /// <summary>
            ///   Looks up a localized string similar to &apos;{0}&apos; is not a valid Base64 encoded string..
            /// </summary>
            internal static string StringIsNotBase64
            {
                get
                {
                    return ResourceManager.GetString("StringIsNotBase64", resourceCulture);
                }
            }
        }

        internal sealed class CommonResources : Resources
        {
            internal static string GetString(string value, params object[] args)
            {
                if (args != null && args.Length > 0)
                {
                    for (int i = 0; i < args.Length; i++)
                    {
                        if (args[i] is string text && text.Length > 1024)
                        {
                            args[i] = text.Substring(0, 1021) + "...";
                        }
                    }

                    return string.Format(Culture, value, args);
                }

                return value;
            }
        }
    }
}
