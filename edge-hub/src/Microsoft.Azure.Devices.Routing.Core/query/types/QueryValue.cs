// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Query.Types
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using Newtonsoft.Json.Linq;

    public class QueryValue : IComparable, IComparable<QueryValue>
    {
        public static readonly QueryValue Undefined = new QueryValue(null, QueryValueType.None);

        public static readonly QueryValue Null = new QueryValue(Query.Null.Instance, QueryValueType.Null);

        public QueryValueType ValueType { get; set; }

        public object Value { get; set; }

        public QueryValue(object value, QueryValueType valueType)
        {
            this.Value = value;
            this.ValueType = valueType;
        }

        public static QueryValue Create(JValue jsonValue)
        {
            switch (jsonValue.Type)
            {
                case JTokenType.Boolean:
                    return new QueryValue(jsonValue.ToObject<Bool>(), QueryValueType.Bool);

                case JTokenType.Integer:
                case JTokenType.Float:
                    return new QueryValue(jsonValue.ToObject<double>(), QueryValueType.Double);

                case JTokenType.String:
                    return new QueryValue(jsonValue.ToString(CultureInfo.InvariantCulture), QueryValueType.String);

                case JTokenType.Null:
                    return new QueryValue(Query.Null.Instance, QueryValueType.Null);

                default:
                    return new QueryValue(jsonValue, QueryValueType.Object);
            }
        }

        public static QueryValue Create(object value)
        {
            switch (value.GetType().Name)
            {
                case "Boolean":
                    return new QueryValue((Bool)Convert.ToBoolean(value), QueryValueType.Bool);

                case "Int32":
                case "Int64":
                case "Int16":
                case "Single":
                case "Double":
                case "Byte":
                case "SByte":
                    return new QueryValue(Convert.ToDouble(value), QueryValueType.Double);

                case "String":
                    return new QueryValue(value as string, QueryValueType.String);

                default:
                    return new QueryValue(value, QueryValueType.Object);
            }
        }

        // Conversions
        public static implicit operator QueryValue(Bool x)
        {
            if (!Query.Undefined.IsDefined(x))
            {
                return Undefined;
            }

            return new QueryValue(x, QueryValueType.Bool);
        }

        public static implicit operator QueryValue(double x)
        {
            if (!Query.Undefined.IsDefined(x))
            {
                return Undefined;
            }

            return new QueryValue(x, QueryValueType.Double);
        }

        public static implicit operator QueryValue(string x)
        {
            // On comparison of QueryValue to Undefined, Undefined gets implicitly casted to string, which then gets casted to QueryValue
            // Return Undefined rather than converting to a real string object.
            if (!Query.Undefined.IsDefined(x))
            {
                return Undefined;
            }

            return new QueryValue(x, QueryValueType.String);
        }

        public static implicit operator QueryValue(Null x) => new QueryValue(Query.Null.Instance, QueryValueType.Null);

        public static implicit operator QueryValue(Undefined x) => Undefined;

        public static explicit operator Bool(QueryValue x)
        {
            if (x?.ValueType == QueryValueType.Bool)
            {
                return (Bool)x.Value;
            }

            return Bool.Undefined;
        }

        public static QueryValue operator +(QueryValue v1, QueryValue v2)
        {
            if (v1.ValueType == QueryValueType.Double && v2.ValueType == QueryValueType.Double)
            {
                return new QueryValue((double)v1.Value + (double)v2.Value, QueryValueType.Double);
            }

            return Undefined;
        }

        public static QueryValue operator -(QueryValue v1, QueryValue v2)
        {
            if (v1.ValueType == QueryValueType.Double && v2.ValueType == QueryValueType.Double)
            {
                return new QueryValue((double)v1.Value - (double)v2.Value, QueryValueType.Double);
            }

            return Undefined;
        }

        public static QueryValue operator *(QueryValue v1, QueryValue v2)
        {
            if (v1.ValueType == QueryValueType.Double && v2.ValueType == QueryValueType.Double)
            {
                return new QueryValue((double)v1.Value * (double)v2.Value, QueryValueType.Double);
            }

            return Undefined;
        }

        public static QueryValue operator /(QueryValue v1, QueryValue v2)
        {
            if (v1.ValueType == QueryValueType.Double && v2.ValueType == QueryValueType.Double)
            {
                return new QueryValue((double)v1.Value / (double)v2.Value, QueryValueType.Double);
            }

            return Undefined;
        }

        public static QueryValue operator !(QueryValue v1)
        {
            if (v1.ValueType == QueryValueType.Bool)
            {
                return new QueryValue(!((Bool)v1.Value), QueryValueType.Bool);
            }

            return Undefined;
        }

        // Used for logical operators with Bool (e.g. &&)
        public static QueryValue operator &(QueryValue x, QueryValue y)
        {
            if (x.ValueType == QueryValueType.Bool && y.ValueType == QueryValueType.Bool)
            {
                return ((Bool)x.Value) & ((Bool)y.Value);
            }

            return Undefined;
        }

        // Used for logical operators with Bool e.g. ||) 
        public static QueryValue operator |(QueryValue x, QueryValue y)
        {
            if (x.ValueType == QueryValueType.Bool && y.ValueType == QueryValueType.Bool)
            {
                return ((Bool)x.Value) | ((Bool)y.Value);
            }

            return Undefined;
        }

        // Used for logical operators with Bool
        public static bool operator true(QueryValue x) => x.ValueType == QueryValueType.Bool && (Bool)x.Value;

        // Used for logical operators with Bool
        public static bool operator false(QueryValue x) => x.ValueType == QueryValueType.Bool && (Bool)x.Value;

        public int CompareTo(object obj)
        {
            return this.CompareTo(obj as QueryValue);
        }

        public int CompareTo(QueryValue other)
        {
            if (other == null)
            {
                return 1;
            }

            return Compare(this, other);
        }

        static int Compare(QueryValue value1, QueryValue value2)
        {
            Debug.Assert(value1 != null);

            if (value2 == null)
            {
                return 1;
            }

            if (value1.ValueType == QueryValueType.None || value2.ValueType == QueryValueType.None)
            {
                return ReferenceEquals(value1, value2) ? -1 :
                    value1.GetHashCode().CompareTo(value2.GetHashCode());
            }

            if (value1.ValueType != value2.ValueType)
            {
                return value1.ValueType.CompareTo(value2.ValueType);
            }

            switch (value1.ValueType)
            {
                case QueryValueType.Bool:

                    return ((Bool)value1.Value).CompareTo((Bool)value2.Value);

                case QueryValueType.String:

                    return string.Compare((string)value1.Value, (string)value2.Value, StringComparison.Ordinal);

                case QueryValueType.Double:

                    double v1 = Convert.ToDouble(value1.Value);
                    double v2 = Convert.ToDouble(value2.Value);

                    return v1.CompareTo(v2);

                case QueryValueType.Null:

                    return 0;

                case QueryValueType.Object:

                    // We do not support value based comparisons on object. Just return based on reference equals.
                    return ReferenceEquals(value1.Value, value2.Value) ? 0 :
                        value1.Value.GetHashCode().CompareTo(value2.Value.GetHashCode());

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static bool IsSupportedType(Type t)
        {
            return t == typeof(Null) ||
                t == typeof(double) ||
                t == typeof(string) ||
                t == typeof(Bool);
        }
    }
}
