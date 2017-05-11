// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Test.Query
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Query;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class ConditionCompilerTest : RoutingUnitTestBase
    {
        static readonly IMessage Message1 = new Message(MessageSource.Telemetry, new byte[0],
            new Dictionary<string, string> { { "key1", "value1" }, { "key2", "3" }, { "key3", "VALUE3"}, { "null_value", null }, { "$sys4", "value4" } },
            new Dictionary<string, string> { { "sys1", "sysvalue1" }, { "sys2", "4" }, { "sys3", "SYSVALUE3"}, { "sysnull", null}, { "sys4", "sysvalue4" } });

        static class TestSuccessDataSource
        {
            public static IEnumerable<object[]> TestData => Data;

            static readonly IList<object[]> Data = new List<object[]>
            {
                // Empty
                new object[] { "", Bool.Undefined },
                new object[] { "  ", Bool.Undefined },

                // Literals
                new object[] { "3", Bool.Undefined },
                new object[] { "\"a string\"", Bool.Undefined },
                new object[] { "'a string'", Bool.Undefined },

                // Equals
                new object[] { "3 = 3", Bool.True },
                new object[] { "3 = 4", Bool.False },
                new object[] { "\"3\" = \"3\"", Bool.True },
                new object[] { "\"3\" = \"4\"", Bool.False },
                new object[] { "'3' = '3'", Bool.True },
                new object[] { "'3' = '4'", Bool.False },
                new object[] { "true = true", Bool.True },
                new object[] { "true = false", Bool.False },
                new object[] { "false = false", Bool.True },
                new object[] { "false = true", Bool.False },
                new object[] { "key2 = \"3\"", Bool.True },
                new object[] { "key2 = '3'", Bool.True },
                new object[] { "as_number(key2) = 3", Bool.True },
                new object[] { "as_number(key2) = 4", Bool.False },
                new object[] { "is_defined(none = \"4\")", Bool.False },
                new object[] { "as_number(none) = 3", Bool.Undefined },
                new object[] { "$sys2 = \"4\"", Bool.True },
                new object[] { "$sys2 = '4'", Bool.True },
                new object[] { "as_number($sys2) = 4", Bool.True },
                new object[] { "as_number($sys2) = 3", Bool.False },
                new object[] { "is_defined($none)", Bool.False },
                new object[] { "as_number($none) = 3", Bool.Undefined },

                // Not equals
                new object[] { "3 != 3", Bool.False },
                new object[] { "3 != 4", Bool.True },
                new object[] { "\"3\" != \"3\"", Bool.False },
                new object[] { "\"3\" != \"4\"", Bool.True },
                new object[] { "'3' != '3'", Bool.False },
                new object[] { "'3' != '4'", Bool.True },
                new object[] { "true != true", Bool.False },
                new object[] { "true != false", Bool.True },
                new object[] { "false != false", Bool.False },
                new object[] { "false != true", Bool.True },
                new object[] { "key2 != \"3\"", Bool.False },
                new object[] { "key2 != '3'", Bool.False },
                new object[] { "as_number(key2) != 3", Bool.False },
                new object[] { "as_number(key2) != 4", Bool.True },
                new object[] { "none != \"4\"", Bool.Undefined },
                new object[] { "as_number(none) != 3", Bool.Undefined },
                new object[] { "3 <> 3", Bool.False },
                new object[] { "3 <> 4", Bool.True },
                new object[] { "\"3\" <> \"3\"", Bool.False },
                new object[] { "\"3\" <> \"4\"", Bool.True },
                new object[] { "'3' <> '3'", Bool.False },
                new object[] { "'3' <> '4'", Bool.True },
                new object[] { "true <> true", Bool.False },
                new object[] { "true <> false", Bool.True },
                new object[] { "false <> false", Bool.False },
                new object[] { "false <> true", Bool.True },
                new object[] { "key2 <> \"3\"", Bool.False },
                new object[] { "key2 <> '3'", Bool.False },
                new object[] { "as_number(key2) <> 3", Bool.False },
                new object[] { "as_number(key2) <> 4", Bool.True },
                new object[] { "none <> \"4\"", Bool.Undefined },
                new object[] { "as_number(none) <> 3", Bool.Undefined },

                // And, Or, Not
                new object[] { "true", Bool.True },
                new object[] { "false", Bool.False },
                new object[] { "not true", Bool.False },
                new object[] { "not false", Bool.True },
                new object[] { "not (true and false)", Bool.True },
                new object[] { "true AND true", Bool.True },
                new object[] { "true AND not true", Bool.False },
                new object[] { "true and true", Bool.True },
                new object[] { "true and false", Bool.False },
                new object[] { "false and true", Bool.False },
                new object[] { "false and false", Bool.False },
                new object[] { "true OR true", Bool.True },
                new object[] { "true or true", Bool.True },
                new object[] { "true or false", Bool.True },
                new object[] { "false or true", Bool.True },
                new object[] { "false or false", Bool.False },

                // Undefined
                new object[] { "undefined", Bool.Undefined },
                new object[] { "undefined or false", Bool.Undefined },
                new object[] { "undefined or true", Bool.True },
                new object[] { "true or undefined", Bool.True },
                new object[] { "false or undefined", Bool.Undefined },
                new object[] { "undefined or undefined", Bool.Undefined },
                new object[] { "true and undefined", Bool.Undefined },
                new object[] { "false and undefined", Bool.False },
                new object[] { "undefined and false", Bool.False },
                new object[] { "undefined and undefined", Bool.Undefined },
                new object[] { "undefined < undefined", Bool.Undefined },

                new object[] { "true or none = 'true'", Bool.True },
                new object[] { "false or none = 'true'", Bool.Undefined },
                new object[] { "none = 'true' or true", Bool.True },
                new object[] { "none = 'true' or false", Bool.Undefined },
                new object[] { "none = 'true' or none2 = 'true'", Bool.Undefined },

                new object[] { "true and none = 'true'", Bool.Undefined },
                new object[] { "false and none = 'true'", Bool.False },
                new object[] { "none = 'true' and true", Bool.Undefined },
                new object[] { "none = 'true' and false", Bool.False },
                new object[] { "none = 'true' and none2 = 'true'", Bool.Undefined },

                new object[] { "not (none = 'true')", Bool.Undefined },

                // Coalesce
                new object[] { "undefined ?? undefined", Bool.Undefined },
                new object[] { "(key1 ?? \"hello\") = \"value1\"", Bool.True },
                new object[] { "(none ?? \"hello\") = \"hello\"", Bool.True },
                new object[] { "(none ?? none ?? \"hello\") = \"hello\"", Bool.True },
                new object[] { "(as_number(key2) ?? 12.34) = 3", Bool.True },
                new object[] { "(as_number(key1) ?? 12.34) = 12.34", Bool.True },
                new object[] { "(3 % 0 ?? 12.34) = 12.34", Bool.True },
                new object[] { "(none ?? none) = \"value\"", Bool.Undefined },

                // Null
                new object[] { "null and null", Bool.False },
                new object[] { "(null and null) = null", Bool.True },
                new object[] { "(null and false) = null", Bool.True },
                new object[] { "(null and true) = null", Bool.True },
                new object[] { "null or true", Bool.True },
                new object[] { "null or false", Bool.False },
                new object[] { "(null or null) = null", Bool.True },
                new object[] { "null", Bool.False },
                new object[] { "not null", Bool.True },

                // Addition
                new object[] { "3 + 4 = 7", Bool.True },
                new object[] { "(3 + as_number(key2)) = 6", Bool.True },
                new object[] { "is_defined(3 + as_number(none))", Bool.False },
                new object[] { "is_defined(3 + as_number(key1))", Bool.False },

                // Subtraction
                new object[] { "3 - 4 = -1", Bool.True },
                new object[] { "(3 - as_number(key2)) = 0", Bool.True },
                new object[] { "is_defined(3 - as_number(none))", Bool.False },
                new object[] { "is_defined(3 - as_number(key1))", Bool.False },

                // Multiplication
                new object[] { "3 * 4 = 12", Bool.True },
                new object[] { "(3 * as_number(key2)) = 9", Bool.True },
                new object[] { "is_defined(3 * as_number(none))", Bool.False },
                new object[] { "is_defined(3 * as_number(key1))", Bool.False },

                // Division
                new object[] { "3 / 4 = 0.75", Bool.True },
                new object[] { "(3 / as_number(key2)) = 1", Bool.True },
                new object[] { "is_defined(3 / as_number(none))", Bool.False },
                new object[] { "is_defined(3 / as_number(key1))", Bool.False },

                // Modulo
                new object[] { "4 % 3 = 1", Bool.True },
                new object[] { "(3 % as_number(key2)) = 0", Bool.True },
                new object[] { "is_defined(3 % 0)", Bool.False },
                new object[] { "is_defined(3 % as_number(none))", Bool.False },
                new object[] { "is_defined(3 % as_number(key1))", Bool.False },

                // Negation
                new object[] { "-1 < 0", Bool.True },
                new object[] { "-(3 + 4) = -7", Bool.True },
                new object[] { "is_defined(-undefined)", Bool.False },
                new object[] { "is_defined(-undefined)", Bool.False },

                // Less than
                new object[] { "4 < 7", Bool.True },
                new object[] { "7 < 7", Bool.False },
                new object[] { "8 < 7", Bool.False },
                new object[] { "3 + 4 < 7", Bool.False },
                new object[] { "3 < as_number(none)", Bool.Undefined },
                new object[] { "3 < as_number(null_value)", Bool.Undefined },
                new object[] { "'a' < 'b'", Bool.True },
                new object[] { "'a' < 'a'", Bool.False },
                new object[] { "'b' < 'a'", Bool.False },
                new object[] { "'a' < none", Bool.Undefined },

                // Less than or equal
                new object[] { "4 <= 7", Bool.True },
                new object[] { "7 <= 7", Bool.True },
                new object[] { "8 <= 7", Bool.False },
                new object[] { "3 + 4 <= 7", Bool.True },
                new object[] { "3 <= as_number(none)", Bool.Undefined },
                new object[] { "3 <= as_number(null_value)", Bool.Undefined },
                new object[] { "'a' <= 'b'", Bool.True },
                new object[] { "'a' <= 'a'", Bool.True },
                new object[] { "'b' <= 'a'", Bool.False },
                new object[] { "'a' <= none", Bool.Undefined },

                // Greater than
                new object[] { "4 > 7", Bool.False },
                new object[] { "7 > 7", Bool.False },
                new object[] { "8 > 7", Bool.True },
                new object[] { "3 + 4 > 7", Bool.False },
                new object[] { "3 > as_number(none)", Bool.Undefined },
                new object[] { "3 > as_number(null_value)", Bool.Undefined },
                new object[] { "'a' > 'b'", Bool.False },
                new object[] { "'a' > 'a'", Bool.False },
                new object[] { "'b' > 'a'", Bool.True },
                new object[] { "'a' > none", Bool.Undefined },

                // Greater than or equal
                new object[] { "4 >= 7", Bool.False },
                new object[] { "7 >= 7", Bool.True },
                new object[] { "8 >= 7", Bool.True },
                new object[] { "3 + 4 >= 7", Bool.True },
                new object[] { "3 >= as_number(none)", Bool.Undefined },
                new object[] { "3 >= as_number(null_value)", Bool.Undefined },
                new object[] { "'a' >= 'b'", Bool.False },
                new object[] { "'a' >= 'a'", Bool.True },
                new object[] { "'b' >= 'a'", Bool.True },
                new object[] { "'a' > none", Bool.Undefined },

                // Access
                new object[] { "key1 = \"value1\"", Bool.True },
                new object[] { "KEY1 = \"value1\"", Bool.True },
                new object[] { "key1 = \"value1\" and key1 = \"value1\"", Bool.True },
                new object[] { "key1 = \"value2\" or key1 = \"value1\"", Bool.True },
                new object[] { "key1 = \"value2\"", Bool.False },
                new object[] { "key2 = \"value1\"", Bool.False },
                new object[] { "none = \"value1\"", Bool.Undefined },
                new object[] { "$sys4 = \"value4\"", Bool.True },
                new object[] { "{$sys4} = \"sysvalue4\"", Bool.True },

                // system properties
                new object[] { "$sys1 = \"sysvalue1\"", Bool.True },
                new object[] { "{$sys1} = \"sysvalue1\"", Bool.True },
                new object[] { "{ $sys1 } = \"sysvalue1\"", Bool.True },

                // as_number
                new object[] { "as_number(\"3\") = 3", Bool.True },
                new object[] { "AS_NUMBER(\"3\") = 3", Bool.True },
                new object[] { "AS_NUMBER('10e10') = 10e10", Bool.True },
                new object[] { "AS_NUMBER('10e1000') = 1/0", Bool.Undefined },
                new object[] { "AS_NUMBER('10000000000000000000000000') = 1e25", Bool.True },
                new object[] { "as_number(key2) = 3", Bool.True },
                new object[] { "as_number(key1) = 3", Bool.Undefined },
                new object[] { "as_number(key3) = 3", Bool.Undefined },
                new object[] { "as_number(key2) * 3 = 9", Bool.True },
                new object[] { "as_number(key3) * 3 = 9", Bool.Undefined },
                new object[] { "as_number(none) * 3 = 9", Bool.Undefined },
                new object[] { "as_number(null_value) * 3 = 9", Bool.Undefined },
                new object[] { "as_number(as_number(4)) * 3 = 12", Bool.True },

                // is_bool
                new object[] { "is_bool(12.34)", Bool.False },
                new object[] { "is_bool('not a number')", Bool.False },
                new object[] { "is_bool(\"not a number\")", Bool.False },
                new object[] { "is_bool(true)", Bool.True },
                new object[] { "IS_BOOL(true)", Bool.True },
                new object[] { "is_bool(false)", Bool.True },
                new object[] { "is_bool(none)", Bool.False },
                new object[] { "is_bool(key1)", Bool.False },

                // is_defined
                new object[] { "is_defined(undefined)", Bool.False },
                new object[] { "IS_DEFINED(undefined)", Bool.False },
                new object[] { "is_defined(key1)", Bool.True },
                new object[] { "is_defined(key4)", Bool.False },
                new object[] { "is_defined(3 * as_number(key4))", Bool.False },
                new object[] { "is_defined(undefined and true)", Bool.False },
                new object[] { "is_defined(as_number(\"3\"))", Bool.True },
                new object[] { "is_defined(as_number(\"apple\"))", Bool.False },
                new object[] { "is_defined(null_value)", Bool.True },
                new object[] { "is_defined(null)", Bool.True },
                new object[] { "is_defined(10e1000)", Bool.False },

                // is_null
                new object[] { "is_null(12.34)", Bool.False },
                new object[] { "is_null('not a number')", Bool.False },
                new object[] { "is_null(\"not a number\")", Bool.False },
                new object[] { "is_null(true)", Bool.False },
                new object[] { "is_null(false)", Bool.False },
                new object[] { "is_null(3 / 0)", Bool.False },
                new object[] { "is_null(as_number(key2))", Bool.False },
                new object[] { "is_null(as_number(key1))", Bool.False },
                new object[] { "is_null(as_number(key1) / as_number(key2))", Bool.False },
                new object[] { "is_null(null)", Bool.True },
                new object[] { "IS_NULL(null)", Bool.True },
                new object[] { "is_null(null_value)", Bool.True },
                new object[] { "is_null(none)", Bool.False },

                // is_number
                new object[] { "is_number(12.34)", Bool.True },
                new object[] { "IS_NUMBER(12.34)", Bool.True },
                new object[] { "is_number('not a number')", Bool.False },
                new object[] { "is_number(\"not a number\")", Bool.False },
                new object[] { "is_number(true)", Bool.False },
                new object[] { "is_number(false)", Bool.False },
                new object[] { "is_number(3 / 0)", Bool.False },
                new object[] { "is_number(as_number(key2))", Bool.True },
                new object[] { "is_number(as_number(key1))", Bool.False },
                new object[] { "is_number(as_number(key1) / as_number(key2))", Bool.False },

                // is_string
                new object[] { "is_string(12.34)", Bool.False },
                new object[] { "is_string('not a number')", Bool.True },
                new object[] { "IS_STRING('not a number')", Bool.True },
                new object[] { "is_string(\"not a number\")", Bool.True },
                new object[] { "is_string(true)", Bool.False },
                new object[] { "is_string(false)", Bool.False },
                new object[] { "is_string(none)", Bool.False },
                new object[] { "is_string(key1)", Bool.True },
                new object[] { "is_string(null_value)", Bool.False },
                new object[] { "is_string('a' || 'b')", Bool.True },

                // concat
                new object[] { "concat('a', 'b', 'c', 'd') = 'abcd'", Bool.True },
                new object[] { "CONCAT('a', 'b', 'c', 'd') = 'abcd'", Bool.True },
                new object[] { "concat('a', 'b') = concat('a', 'b')", Bool.True },
                new object[] { "concat('a', key1, 'd') = 'avalue1d'", Bool.True },
                new object[] { "is_defined(concat('a', none, 'd'))", Bool.False },
                new object[] { "'a' || 'b' || 'c' || 'd' = 'abcd'", Bool.True },
                new object[] { "'a' || key1 || 'd' = 'avalue1d'", Bool.True },
                new object[] { "'a' || null_value || 'd' = 'ad'", Bool.True },
                new object[] { "concat('a', null_value, 'd') = 'ad'", Bool.True },
                new object[] { "is_defined('a' || none || 'd')", Bool.False },
                new object[] { "is_defined('a' || null_value || 'd')", Bool.True },

                // length
                new object[] { "length('HELLO') = 5", Bool.True },
                new object[] { "LENGTH('') = 0", Bool.True },
                new object[] { "length(key3) = 6", Bool.True },
                new object[] { "is_defined(length(null_value))", Bool.False },
                new object[] { "is_defined(length(none))", Bool.False },

                // lower
                new object[] { "lower('HELLO') = 'hello'", Bool.True },
                new object[] { "LOWER('hello') = 'hello'", Bool.True },
                new object[] { "lower(key3) = 'value3'", Bool.True },
                new object[] { "is_defined(lower(null_value))", Bool.False },
                new object[] { "is_defined(lower(none))", Bool.False },

                // upper
                new object[] { "upper('HELLO') = 'HELLO'", Bool.True },
                new object[] { "UPPER('hello') = 'HELLO'", Bool.True },
                new object[] { "upper(key1) = 'VALUE1'", Bool.True },
                new object[] { "is_defined(upper(null_value))", Bool.False },
                new object[] { "is_defined(upper(none))", Bool.False },

                // substring
                new object[] { "substring('abc', 1) = 'bc'", Bool.True },
                new object[] { "substring('abc', 2) = 'c'", Bool.True },
                new object[] { "SUBSTRING('abc', 3) = 'c'", Bool.False },
                new object[] { "SUBSTRING('abc', 4) = 'c'", Bool.Undefined },
                new object[] { "substring(key1, 1) = 'alue1'", Bool.True },
                new object[] { "substring('abc', -1) = ''", Bool.Undefined },
                new object[] { "substring('abc', 1 % 0) = ''", Bool.Undefined },
                new object[] { "substring(null_value, 1) = 'a'", Bool.Undefined },
                new object[] { "substring(none, 1) = 'a'", Bool.Undefined },

                new object[] { "substring('abc', 1, 1) = 'b'", Bool.True },
                new object[] { "substring('abc', 1, 0) = ''", Bool.True },
                new object[] { "SUBSTRING(key1, 1, 1) = 'a'", Bool.True },
                new object[] { "substring('abc', -1, 0) = ''", Bool.Undefined },
                new object[] { "substring('abc', 1, -2) = ''", Bool.Undefined },
                new object[] { "substring('abc', 1 % 0, 1) = ''", Bool.Undefined },
                new object[] { "substring('abc', 1, 1 % 0) = ''", Bool.Undefined },
                new object[] { "substring(null_value, 1, 1) = 'a'", Bool.Undefined },
                new object[] { "substring(none, 1, 1) = 'a'", Bool.Undefined },

                new object[] { "substring('hello', 0, 5) = 'hello'", Bool.True },
                new object[] { "substring('hello', 0, 4) = 'hell'", Bool.True },
                new object[] { "substring('hello', 1, 4) = 'ello'", Bool.True },
                new object[] { "substring('hello', 4, 1) = 'o'", Bool.True },
                new object[] { "substring('hello', 4, 0) = ''", Bool.True },

                // index_of
                new object[] { "index_of('abcdef', 'cd') = 2", Bool.True },
                new object[] { "index_of('abc', 'xz') = -1", Bool.True },
                new object[] { "INDEX_OF(key1, 'val') = 0", Bool.True },
                new object[] { "index_of('lue1value1', key1) = 4", Bool.True },
                new object[] { "index_of('abc', key1) = -1", Bool.True },
                new object[] { "index_of(null_value, '1') = 0", Bool.Undefined },
                new object[] { "index_of(none, '1') = 0", Bool.Undefined },

                // starts_with
                new object[] { "starts_with('abc', 'ab')", Bool.True },
                new object[] { "starts_with('abc', 'xz')", Bool.False },
                new object[] { "STARTS_WITH(key1, 'val')", Bool.True },
                new object[] { "starts_with('value1value1', key1)", Bool.True },
                new object[] { "starts_with('abc', key1)", Bool.False },
                new object[] { "starts_with(null_value, '1')", Bool.Undefined },
                new object[] { "starts_with(none, '1')", Bool.Undefined },

                // ends_with
                new object[] { "ends_with('abc', 'bc')", Bool.True },
                new object[] { "ends_with('abc', 'xz')", Bool.False },
                new object[] { "ENDS_WITH(key1, 'e1')", Bool.True },
                new object[] { "ends_with('value1value1', key1)", Bool.True },
                new object[] { "ends_with('abc', key1)", Bool.False },
                new object[] { "ends_with(null_value, '1')", Bool.Undefined },
                new object[] { "ends_with(none, '1')", Bool.Undefined },

                // contains
                new object[] { "contains('abc', 'ab')", Bool.True },
                new object[] { "contains('abc', 'xz')", Bool.False },
                new object[] { "CONTAINS(key1, 'val')", Bool.True },
                new object[] { "contains('value1value1', key1)", Bool.True },
                new object[] { "contains('abc', key1)", Bool.False },
                new object[] { "contains(null_value, '1')", Bool.Undefined },
                new object[] { "contains(none, '1')", Bool.Undefined },

                // abs
                new object[] { "abs(-12) = 12", Bool.True },
                new object[] { "abs(12) = 12", Bool.True },
                new object[] { "ABS(as_number(key2)) = 3", Bool.True },
                new object[] { "is_defined(abs(as_number(key1)))", Bool.False },
                new object[] { "is_defined(abs(3 / 0))", Bool.True },
                new object[] { "is_defined(abs(3 % 0))", Bool.False },

                // exp
                new object[] { "exp(0) = 1", Bool.True },
                new object[] { "abs(exp(1) - 2.718281) < 1e-6", Bool.True },
                new object[] { "abs(EXP(as_number(key2)) - 20.085536) < 1e-6", Bool.True },
                new object[] { "exp(4000) = 3/0", Bool.True },
                new object[] { "is_defined(exp(as_number(key1)))", Bool.False },
                new object[] { "is_defined(exp(3 / 0))", Bool.True },
                new object[] { "is_defined(exp(3 % 0))", Bool.False },

                // power
                new object[] { "power(2, 3) = 8", Bool.True },
                new object[] { "POWER(3, 2) = 9", Bool.True },
                new object[] { "POWER(10, 4000) = 3/0", Bool.True },
                new object[] { "power(as_number(key2), as_number(key2)) = 27", Bool.True },
                new object[] { "is_defined(power(as_number(key1), as_number(key1)))", Bool.False },
                new object[] { "is_defined(power(3 / 0, 2))", Bool.True },
                new object[] { "is_defined(power(2, 3 / 0))", Bool.True },
                new object[] { "is_defined(power(3 % 0, 2))", Bool.False },
                new object[] { "is_defined(power(2, 3 % 0))", Bool.False },

                // square
                new object[] { "square(2) = 4", Bool.True },
                new object[] { "SQUARE(3) = 9", Bool.True },
                new object[] { "square(as_number(key2)) = 9", Bool.True },
                new object[] { "is_defined(square(as_number(key1)))", Bool.False },
                new object[] { "is_defined(square(3 / 0))", Bool.True },
                new object[] { "is_defined(square(3 % 0))", Bool.False },

                // ceiling
                new object[] { "ceiling(2) = 2", Bool.True },
                new object[] { "ceiling(2.1) = 3", Bool.True },
                new object[] { "ceiling(-2.1) = -2", Bool.True },
                new object[] { "CEILING(-2) = -2", Bool.True },
                new object[] { "ceiling(as_number(key2)) = 3", Bool.True },
                new object[] { "is_defined(ceiling(as_number(key1)))", Bool.False },
                new object[] { "is_defined(ceiling(3 / 0))", Bool.True },
                new object[] { "is_defined(ceiling(3 % 0))", Bool.False },

                // floor
                new object[] { "floor(2) = 2", Bool.True },
                new object[] { "floor(2.1) = 2", Bool.True },
                new object[] { "floor(-2.1) = -3", Bool.True },
                new object[] { "FLOOR(-2) = -2", Bool.True },
                new object[] { "floor(as_number(key2)) = 3", Bool.True },
                new object[] { "is_defined(floor(as_number(key1)))", Bool.False },
                new object[] { "is_defined(floor(3 / 0))", Bool.True },
                new object[] { "is_defined(floor(3 % 0))", Bool.False },

                // sign
                new object[] { "sign(0) = 0", Bool.True },
                new object[] { "sign(2) = 1", Bool.True },
                new object[] { "sign(2.1) = 1", Bool.True },
                new object[] { "sign(-2.1) = -1", Bool.True },
                new object[] { "SIGN(-2) = -1", Bool.True },
                new object[] { "sign(as_number(key2)) = 1", Bool.True },
                new object[] { "is_defined(sign(as_number(key1)))", Bool.False },
                new object[] { "sign(3 / 0) = 1", Bool.True },
                new object[] { "sign(-3 / 0) = -1", Bool.True },
                new object[] { "is_defined(sign(3 % 0))", Bool.False },

                // sqrt
                new object[] { "sqrt(4) = 2", Bool.True },
                new object[] { "SQRT(-4) = -2", Bool.Undefined },
                new object[] { "abs(sqrt(as_number(key2)) - 1.732050) < 1e-6", Bool.True },
                new object[] { "is_defined(sqrt(as_number(key1)))", Bool.False },
                new object[] { "is_defined(sqrt(3 / 0))", Bool.True },
                new object[] { "is_defined(sqrt(3 % 0))", Bool.False },
            };
        }

        [Theory, Unit]
        [MemberData(nameof(TestSuccessDataSource.TestData), MemberType = typeof(TestSuccessDataSource))]
        public void TestSuccess(string condition, Bool expected)
        {
            var route = new Route("id", condition, "hub", MessageSource.Telemetry, new HashSet<Endpoint>());
            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route);
            Assert.Equal(expected, rule(Message1));
        }

        [Fact, Unit]
        public void TestTest()
        {
            string condition = "as_number(key2) = 3";
            Bool expected = Bool.True;

            var route = new Route("id", condition, "hub", MessageSource.Telemetry, new HashSet<Endpoint>());
            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route);
            Assert.Equal(expected, rule(Message1));
        }

        [Fact, Unit]
        public void TestSubstringLimits()
        {
            for (int i = -10; i <= 10; i++)
            {
                string condition = $"substring('hello', {i}) = 'he'";
                var route = new Route("id", condition, "hub", MessageSource.Telemetry, new HashSet<Endpoint>());

                // assert doesn't throw
                Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route);
                rule(Message1);
            }

            for (int i = -10; i <= 10; i++)
            {
                for (int j = -10; j <= 10; j++)
                {
                    string condition = $"substring('hello', {i}, {j}) = 'he'";
                    var route = new Route("id", condition, "hub", MessageSource.Telemetry, new HashSet<Endpoint>());

                    // assert doesn't throw
                    Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route);
                    rule(Message1);
                }
            }
        }

        static class TestCompilationFailureDataSource
        {
            public static IEnumerable<object[]> TestData => Data;

            static readonly IList<object[]> Data = new List<object[]>
            {
                new object[] { "is_defined(3 + none)" },
                new object[] { "is_defined(3 + null_value)" },
                new object[] { "is_defined(true + 3)" },
                new object[] { "is_defined(3 + true)" },
                new object[] { "is_defined(3 + undefined)" },
                new object[] { "is_defined('a' + 'b')" },
                new object[] { "is_defined(undefined + 3)" },
                new object[] { "is_defined(undefined + undefined)" },
                new object[] { "is_defined(3 - none)" },
                new object[] { "is_defined(3 - null_value)" },
                new object[] { "is_defined(true - 3)" },
                new object[] { "is_defined(3 - true)" },
                new object[] { "is_defined(3 - undefined)" },
                new object[] { "is_defined('a' - 'b')" },
                new object[] { "is_defined(undefined - 3)" },
                new object[] { "is_defined(undefined - undefined)" },
                new object[] { "is_defined(3 * none)" },
                new object[] { "is_defined(3 * null_value)" },
                new object[] { "is_defined(true * 3)" },
                new object[] { "is_defined(3 * true)" },
                new object[] { "is_defined(3 * undefined)" },
                new object[] { "is_defined('a' * 'b')" },
                new object[] { "is_defined(undefined * 3)" },
                new object[] { "is_defined(undefined * undefined)" },
                new object[] { "is_defined(3 / none)" },
                new object[] { "is_defined(3 / null_value)" },
                new object[] { "is_defined(true / 3)" },
                new object[] { "is_defined(3 / true)" },
                new object[] { "is_defined(3 / undefined)" },
                new object[] { "is_defined('a' / 'b')" },
                new object[] { "is_defined(undefined / 3)" },
                new object[] { "is_defined(undefined / undefined)" },
                new object[] { "is_defined(3 % none)" },
                new object[] { "is_defined(3 % null_value)" },
                new object[] { "is_defined(true % 3)" },
                new object[] { "is_defined(3 % true)" },
                new object[] { "is_defined(3 % undefined)" },
                new object[] { "is_defined('a' % 'b')" },
                new object[] { "is_defined(undefined % 3)" },
                new object[] { "is_defined(undefined % undefined)" },
                new object[] { "is_defined(3 = none)" },
                new object[] { "is_defined(3 != none)" },
                new object[] { "is_defined(3 <> none)" },
                new object[] { "is_defined(3 > none)" },
                new object[] { "is_defined(3 >= none)" },
                new object[] { "is_defined(3 < none)" },
                new object[] { "is_defined(3 <= none)" },
                new object[] { "is_defined(3 <= none)" },
                new object[] { "is_defined(3 <= none.key1)" },
                new object[] { "is_defined(3 <= none.key1.key2)" },
                new object[] { "is_defined(3 = null_value)" },
                new object[] { "is_defined(3 != null_value)" },
                new object[] { "is_defined(3 <> null_value)" },
                new object[] { "is_defined(3 > null_value)" },
                new object[] { "is_defined(3 >= null_value)" },
                new object[] { "is_defined(3 < null_value)" },
                new object[] { "is_defined(3 <= null_value)" },
                new object[] { "is_defined(3 <= null_value)" },
                // TODO = These tests don't pass at the moment, need to fix them. Might have to look into fixing the grammar.
                // Note - looks like Antlr code has changed internally from the version used in IoTHub codebase
                // which might affect the behavior.
                //new object[] { "3 $ 4" },
                //new object[] { "$ sys1 = 'sysvalue1'" },

                // Coalesce
                new object[] { "key1 ?? \"hello\" = \"value1\"" },
                new object[] { "(key1 ?? 12.34)" },
                new object[] { "(12.34 ?? key1)" },
                new object[] { "(as_number(key2) ?? key1)" },
                new object[] { "(key1 ?? null)" },
                new object[] { "(null && key1)" },
                new object[] { "(key1 ?? true)" },
                new object[] { "(true && key1)" },

                // Concat
                new object[] { "concat() = 'avalue1d'" },
                new object[] { "concat('a') = 'avalue1d'" },
                new object[] { "concat(3) = 'avalue1d'" },
                new object[] { "concat('a', null) = 'avalue1d'" },
                new object[] { "concat('a', undefined) = 'avalue1d'" },
                new object[] { "concat('a', true) = 'avalue1d'" },
                new object[] { "'a' || null = 'avalue1d'" },
                new object[] { "'a' || undefined = 'avalue1d'" },
                new object[] { "'a' || true = 'avalue1d'" },

                // Length
                new object[] { "length(12.34) = 'hello'" },
                new object[] { "length(null) = 'hello'" },
                new object[] { "length(undefined) = 'value1'" },
                new object[] { "length(true) = 'value1'" },
                new object[] { "length(false) = 'value1'" },

                // Lower
                new object[] { "lower(12.34) = 'hello'" },
                new object[] { "lower(null) = 'hello'" },
                new object[] { "lower(undefined) = 'value1'" },
                new object[] { "lower(true) = 'value1'" },
                new object[] { "lower(false) = 'value1'" },

                // Upper
                new object[] { "upper(12.34) = 'hello'" },
                new object[] { "upper(null) = 'hello'" },
                new object[] { "upper(undefined) = 'value1'" },
                new object[] { "upper(true) = 'value1'" },
                new object[] { "upper(false) = 'value1'" },

                // substring
                new object[] { "substring() = 'hello'" },
                new object[] { "substring('hello') = 'hello'" },
                new object[] { "substring('hello', 'a') = 'hello'" },
                new object[] { "substring('hello', 'a', 'b') = 'hello'" },
                new object[] { "substring(12.34) = 'hello'" },
                new object[] { "substring(null) = 'hello'" },
                new object[] { "substring(undefined) = 'value1'" },
                new object[] { "substring(true) = 'value1'" },
                new object[] { "substring(false) = 'value1'" },

                // index_of
                new object[] { "index_of()" },
                new object[] { "index_of('hello')" },
                new object[] { "index_of('hello', 'a', 'b')" },
                new object[] { "index_of('hello', 12.34)" },
                new object[] { "index_of(12.34, 'hello')" },
                new object[] { "index_of(12.34, 12.34)" },
                new object[] { "index_of(null, 'hello')" },
                new object[] { "index_of('hello', null)" },
                new object[] { "index_of(null, null)" },
                new object[] { "index_of(undefined, 'hello')" },
                new object[] { "index_of('hello', undefined)" },
                new object[] { "index_of(undefined, undefined)" },
                new object[] { "index_of(true, 'hello')" },
                new object[] { "index_of('hello', true)" },
                new object[] { "index_of(true, true)" },

                // starts_with
                new object[] { "starts_with()" },
                new object[] { "starts_with('hello')" },
                new object[] { "starts_with('hello', 'a', 'b')" },
                new object[] { "starts_with('hello', 12.34)" },
                new object[] { "starts_with(12.34, 'hello')" },
                new object[] { "starts_with(12.34, 12.34)" },
                new object[] { "starts_with(null, 'hello')" },
                new object[] { "starts_with('hello', null)" },
                new object[] { "starts_with(null, null)" },
                new object[] { "starts_with(undefined, 'hello')" },
                new object[] { "starts_with('hello', undefined)" },
                new object[] { "starts_with(undefined, undefined)" },
                new object[] { "starts_with(true, 'hello')" },
                new object[] { "starts_with('hello', true)" },
                new object[] { "starts_with(true, true)" },

                // ends_with
                new object[] { "ends_with()" },
                new object[] { "ends_with('hello')" },
                new object[] { "ends_with('hello', 'a', 'b')" },
                new object[] { "ends_with('hello', 12.34)" },
                new object[] { "ends_with(12.34, 'hello')" },
                new object[] { "ends_with(12.34, 12.34)" },
                new object[] { "ends_with(null, 'hello')" },
                new object[] { "ends_with('hello', null)" },
                new object[] { "ends_with(null, null)" },
                new object[] { "ends_with(undefined, 'hello')" },
                new object[] { "ends_with('hello', undefined)" },
                new object[] { "ends_with(undefined, undefined)" },
                new object[] { "ends_with(true, 'hello')" },
                new object[] { "ends_with('hello', true)" },
                new object[] { "ends_with(true, true)" },

                // contains
                new object[] { "contains()" },
                new object[] { "contains('hello')" },
                new object[] { "contains('hello', 'a', 'b')" },
                new object[] { "contains('hello', 12.34)" },
                new object[] { "contains(12.34, 'hello')" },
                new object[] { "contains(12.34, 12.34)" },
                new object[] { "contains(null, 'hello')" },
                new object[] { "contains('hello', null)" },
                new object[] { "contains(null, null)" },
                new object[] { "contains(undefined, 'hello')" },
                new object[] { "contains('hello', undefined)" },
                new object[] { "contains(undefined, undefined)" },
                new object[] { "contains(true, 'hello')" },
                new object[] { "contains('hello', true)" },
                new object[] { "contains(true, true)" },

                // Abs
                new object[] { "is_defined(abs(null))" },
                new object[] { "is_defined(abs(undefined))" },
                new object[] { "is_defined(abs(true))" },
                new object[] { "is_defined(abs(false))" },
                new object[] { "is_defined(abs(key1))" },
                new object[] { "is_defined(abs(3, 4))" },

                // Exp
                new object[] { "is_defined(exp(null))" },
                new object[] { "is_defined(exp(undefined))" },
                new object[] { "is_defined(exp(true))" },
                new object[] { "is_defined(exp(false))" },
                new object[] { "is_defined(exp(key1))" },
                new object[] { "is_defined(exp(3, 4))" },

                // Power
                new object[] { "is_defined(power(null, 2))" },
                new object[] { "is_defined(power(2, null))" },
                new object[] { "is_defined(power(null, null))" },
                new object[] { "is_defined(power(undefined, 2))" },
                new object[] { "is_defined(power(2, undefined))" },
                new object[] { "is_defined(power(undefined, undefined))" },
                new object[] { "is_defined(power(true, 2))" },
                new object[] { "is_defined(power(2, true))" },
                new object[] { "is_defined(power(true, true))" },
                new object[] { "is_defined(power(key1, 2))" },
                new object[] { "is_defined(power(2, key1))" },
                new object[] { "is_defined(power(2, 3, 4))" },

                // Square
                new object[] { "is_defined(square(null))" },
                new object[] { "is_defined(square(undefined))" },
                new object[] { "is_defined(square(true))" },
                new object[] { "is_defined(square(false))" },
                new object[] { "is_defined(square(key1))" },
                new object[] { "is_defined(square(3, 3))" },

                // Ceiling
                new object[] { "is_defined(ceiling(null))" },
                new object[] { "is_defined(ceiling(undefined))" },
                new object[] { "is_defined(ceiling(true))" },
                new object[] { "is_defined(ceiling(false))" },
                new object[] { "is_defined(ceiling(key1))" },
                new object[] { "is_defined(ceiling(3, 3))" },

                // Floor
                new object[] { "is_defined(floor(null))" },
                new object[] { "is_defined(floor(undefined))" },
                new object[] { "is_defined(floor(true))" },
                new object[] { "is_defined(floor(false))" },
                new object[] { "is_defined(floor(key1))" },
                new object[] { "is_defined(floor(3, 3))" },

                // Sign
                new object[] { "is_defined(sign(null))" },
                new object[] { "is_defined(sign(undefined))" },
                new object[] { "is_defined(sign(true))" },
                new object[] { "is_defined(sign(false))" },
                new object[] { "is_defined(sign(key1))" },
                new object[] { "is_defined(sign(3, 3))" },

                // Sqrt
                new object[] { "is_defined(sqrt(null))" },
                new object[] { "is_defined(sqrt(undefined))" },
                new object[] { "is_defined(sqrt(true))" },
                new object[] { "is_defined(sqrt(false))" },
                new object[] { "is_defined(sqrt(key1))" },
                new object[] { "is_defined(sqrt(3, 3))" },

                new object[] { "test == true" },
                new object[] { "true == test" },

                // empty unterminated string
                new object[] { "none = \""},

                // and
                new object[] { "none and true" },
                new object[] { "undefined and \"hello\"" },

                // or
                new object[] { "none or true" },

                // negate
                new object[] { "- (key1)" },

                // not
                new object[] { "is_defined(not key1)"},
            };
        }

        [Theory, Unit]
        [MemberData(nameof(TestCompilationFailureDataSource.TestData), MemberType = typeof(TestCompilationFailureDataSource))]
        public void TestCompilationFailure(string condition)
        {
            var route = new Route("id", condition, "hub", MessageSource.Telemetry, new HashSet<Endpoint>());
            Assert.Throws<RouteCompilationException>(() => RouteCompiler.Instance.Compile(route));
        }
    }
}