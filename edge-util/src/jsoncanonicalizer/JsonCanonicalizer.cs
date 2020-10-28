/*
 *  Copyright 2006-2019 WebPKI.org (http://webpki.org).
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *
 *      https://www.apache.org/licenses/LICENSE-2.0
 *
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 *
 */

using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Org.Webpki.Es6NumberSerialization;

// JSON canonicalizer for .NET Core

namespace Org.Webpki.JsonCanonicalizer
{
    public class JsonCanonicalizer
    {
        StringBuilder buffer;

        public JsonCanonicalizer(string jsonData)
        {
            buffer = new StringBuilder();
            Serialize(new JsonDecoder(jsonData).root);
        }

        public JsonCanonicalizer(byte[] jsonData)
            : this(new UTF8Encoding(false, true).GetString(jsonData))
        {

        }

        private void Escape(char c)
        {
            buffer.Append('\\').Append(c);
        }

        private void SerializeString(string value)
        {
            buffer.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\n':
                        Escape('n');
                        break;

                    case '\b':
                        Escape('b');
                        break;

                    case '\f':
                        Escape('f');
                        break;

                    case '\r':
                        Escape('r');
                        break;

                    case '\t':
                        Escape('t');
                        break;

                    case '"':
                    case '\\':
                        Escape(c);
                        break;

                    default:
                        if (c < ' ')
                        {
                            buffer.Append("\\u").Append(((int)c).ToString("x04"));
                        }
                        else
                        {
                            buffer.Append(c);
                        }
                        break;
                }
            }
            buffer.Append('"');
        }

        void Serialize(object o)
        {
            if (o is SortedDictionary<string, object>)
            {
                buffer.Append('{');
                bool next = false;
                foreach (var keyValuePair in (SortedDictionary<string, object>)o)
                {
                    if (next)
                    {
                        buffer.Append(',');
                    }
                    next = true;
                    SerializeString(keyValuePair.Key);
                    buffer.Append(':');
                    Serialize(keyValuePair.Value);
                }
                buffer.Append('}');
            }
            else if (o is List<object>)
            {
                buffer.Append('[');
                bool next = false;
                foreach (object value in (List<object>)o)
                {
                    if (next)
                    {
                        buffer.Append(',');
                    }
                    next = true;
                    Serialize(value);
                }
                buffer.Append(']');
            }
            else if (o == null)
            {
                buffer.Append("null");
            }
            else if (o is String)
            {
                SerializeString((string)o);
            }
            else if (o is Boolean)
            {
                buffer.Append(o.ToString().ToLowerInvariant());
            }
            else if (o is Double)
            {
                buffer.Append(NumberToJson.SerializeNumber((Double)o));
            }
            else
            {
                throw new InvalidOperationException("Unknown object: " + o);
            }
        }

        public string GetEncodedString()
        {
            return buffer.ToString();
        }

        public byte[] GetEncodedUTF8()
        {
            return new UTF8Encoding(false, true).GetBytes(GetEncodedString());
        }
    }

    static class JsonToNumber
    {
        public static double Convert(string number)
        {
            return double.Parse(number, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }

    class JsonDecoder
    {
        const char LEFT_CURLY_BRACKET  = '{';
        const char RIGHT_CURLY_BRACKET = '}';
        const char DOUBLE_QUOTE        = '"';
        const char COLON_CHARACTER     = ':';
        const char LEFT_BRACKET        = '[';
        const char RIGHT_BRACKET       = ']';
        const char COMMA_CHARACTER     = ',';
        const char BACK_SLASH          = '\\';

        static Regex NUMBER_PATTERN  = new Regex("^-?[0-9]+(\\.[0-9]+)?([eE][-+]?[0-9]+)?$");
        static Regex BOOLEAN_PATTERN = new Regex("^true|false$");

        int index;
        string jsonData;

        internal object root;

        internal JsonDecoder(string jsonData)
        {
            this.jsonData = jsonData;
            if (TestNextNonWhiteSpaceChar() == LEFT_BRACKET)
            {
                Scan();
                root = ParseArray();
            }
            else
            {
                ScanFor(LEFT_CURLY_BRACKET);
                root = ParseObject();
            }
            while (index < jsonData.Length)
            {
                if (!IsWhiteSpace(jsonData[index++]))
                {
                    throw new IOException("Improperly terminated JSON object");
                }
            }
        }

        object ParseElement()
        {
            switch (Scan())
            {
                case LEFT_CURLY_BRACKET:
                    return ParseObject();

                case DOUBLE_QUOTE:
                    return ParseQuotedString();

                case LEFT_BRACKET:
                    return ParseArray();

                default:
                    return ParseSimpleType();
            }
        }

        object ParseObject()
        {
            SortedDictionary<string, object> dict =
                new SortedDictionary<string, object>(StringComparer.Ordinal);
            bool next = false;
            while (TestNextNonWhiteSpaceChar() != RIGHT_CURLY_BRACKET)
            {
                if (next)
                {
                    ScanFor(COMMA_CHARACTER);
                }
                next = true;
                ScanFor(DOUBLE_QUOTE);
                string name = ParseQuotedString();
                ScanFor(COLON_CHARACTER);
                dict.Add(name, ParseElement());
            }
            Scan();
            return dict;
        }

        object ParseArray()
        {
            var list = new List<object>();
            bool next = false;
            while (TestNextNonWhiteSpaceChar() != RIGHT_BRACKET)
            {
                if (next)
                {
                    ScanFor(COMMA_CHARACTER);
                }
                else
                {
                    next = true;
                }
                list.Add(ParseElement());
            }
            Scan();
            return list;
        }

        object ParseSimpleType()
        {
            index--;
            StringBuilder tempBuffer = new StringBuilder();
            char c;
            while ((c = TestNextNonWhiteSpaceChar()) != COMMA_CHARACTER && c != RIGHT_BRACKET && c != RIGHT_CURLY_BRACKET)
            {
                if (IsWhiteSpace(c = NextChar()))
                {
                    break;
                }
                tempBuffer.Append(c);
            }
            String token = tempBuffer.ToString();
            if (token.Length == 0)
            {
                throw new IOException("Missing argument");
            }
            if (NUMBER_PATTERN.IsMatch(token))
            {
                return JsonToNumber.Convert(token);
            }
            else if (BOOLEAN_PATTERN.IsMatch(token))
            {
                return Boolean.Parse(token);
            }
            else if (token.Equals("null"))
            {
                return null;
            }
            throw new IOException("Unrecognized or malformed JSON token: " + token);
        }

        String ParseQuotedString()
        {
            StringBuilder result = new StringBuilder();
            while (true)
            {
                char c = NextChar();
                if (c < ' ')
                {
                    throw new IOException(c == '\n' ? "Unterminated string literal" :
                        "Unescaped control character: 0x" + ((int)c).ToString("x02"));
                }
                if (c == DOUBLE_QUOTE)
                {
                    break;
                }
                if (c == BACK_SLASH)
                {
                    switch (c = NextChar())
                    {
                        case '"':
                        case '\\':
                        case '/':
                            break;

                        case 'b':
                            c = '\b';
                            break;

                        case 'f':
                            c = '\f';
                            break;

                        case 'n':
                            c = '\n';
                            break;

                        case 'r':
                            c = '\r';
                            break;

                        case 't':
                            c = '\t';
                            break;

                        case 'u':
                            c = (char)0;
                            for (int i = 0; i < 4; i++)
                            {
                                c = (char)((c << 4) + GetHexChar());
                            }
                            break;

                        default:
                            throw new IOException("Unsupported escape:" + c);
                    }
                }
                result.Append(c);
            }
            return result.ToString();
        }

        char GetHexChar()
        {
            char c = NextChar();
            switch (c)
            {
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    return (char)(c - '0');

                case 'a':
                case 'b':
                case 'c':
                case 'd':
                case 'e':
                case 'f':
                    return (char)(c - 'a' + 10);

                case 'A':
                case 'B':
                case 'C':
                case 'D':
                case 'E':
                case 'F':
                    return (char)(c - 'A' + 10);
            }
            throw new IOException("Bad hex in \\u escape: " + c);
        }

        char TestNextNonWhiteSpaceChar()
        {
            int save = index;
            char c = Scan();
            index = save;
            return c;
        }

        void ScanFor(char expected)
        {
            char c = Scan();
            if (c != expected)
            {
                throw new IOException("Expected '" + expected + "' but got '" + c + "'");
            }
        }

        char NextChar()
        {
            if (index < jsonData.Length)
            {
                return jsonData[index++];
            }
            throw new IOException("Unexpected EOF reached");
        }

        bool IsWhiteSpace(char c)
        {
            return c == 0x20 || c == 0x0A || c == 0x0D || c == 0x09;
        }

        char Scan()
        {
            while (true)
            {
                char c = NextChar();
                if (IsWhiteSpace(c))
                {
                    continue;
                }
                return c;
            }
        }
    }
}
