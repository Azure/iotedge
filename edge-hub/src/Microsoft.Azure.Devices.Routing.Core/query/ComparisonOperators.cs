// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Query
{
    using System;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;

    public static class ComparisonOperators
    {
        #region String comparison

        public static Bool Compare(CompareOp op, string s1, string s2)
        {
            if (!Undefined.IsDefined(s1) || !Undefined.IsDefined(s2) || s1 == null || s2 == null)
            {
                return Bool.Undefined;
            }

            Bool result;
            switch (op)
            {
                case CompareOp.Eq:
                    result = (Bool)(string.Compare(s1, s2, StringComparison.Ordinal) == 0);
                    break;
                case CompareOp.Ne:
                    result = (Bool)(string.Compare(s1, s2, StringComparison.Ordinal) != 0);
                    break;
                case CompareOp.Lt:
                    result = (Bool)(string.Compare(s1, s2, StringComparison.Ordinal) < 0);
                    break;
                case CompareOp.Le:
                    result = (Bool)(string.Compare(s1, s2, StringComparison.Ordinal) <= 0);
                    break;
                case CompareOp.Gt:
                    result = (Bool)(string.Compare(s1, s2, StringComparison.Ordinal) > 0);
                    break;
                case CompareOp.Ge:
                    result = (Bool)(string.Compare(s1, s2, StringComparison.Ordinal) >= 0);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(op), op, null);
            }

            return result;
        }

        #endregion

        #region Double comparison

        public static Bool Compare(CompareOp op, double d1, double d2)
        {
            if (!Undefined.IsDefined(d1) || !Undefined.IsDefined(d2))
            {
                return Bool.Undefined;
            }

            Bool result;
            switch (op)
            {
                case CompareOp.Eq:
                    result = (Bool)(d1.CompareTo(d2) == 0);
                    break;
                case CompareOp.Ne:
                    result = (Bool)(d1.CompareTo(d2) != 0);
                    break;
                case CompareOp.Lt:
                    result = (Bool)(d1.CompareTo(d2) < 0);
                    break;
                case CompareOp.Le:
                    result = (Bool)(d1.CompareTo(d2) <= 0);
                    break;
                case CompareOp.Gt:
                    result = (Bool)(d1.CompareTo(d2) > 0);
                    break;
                case CompareOp.Ge:
                    result = (Bool)(d1.CompareTo(d2) >= 0);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(op), op, null);
            }

            return result;
        }

        #endregion

        #region QueryValue comparison

        public static Bool Compare(CompareOp op, QueryValue v1, QueryValue v2)
        {
            if (!Undefined.IsDefined(v1) || !Undefined.IsDefined(v2) || v1 == null || v2 == null)
            {
                return Bool.Undefined;
            }

            Bool result;
            switch (op)
            {
                case CompareOp.Eq:
                    result = (Bool)(v1.CompareTo(v2) == 0);
                    break;
                case CompareOp.Ne:
                    result = (Bool)(v1.CompareTo(v2) != 0);
                    break;
                case CompareOp.Lt:
                    result = (Bool)(v1.CompareTo(v2) < 0);
                    break;
                case CompareOp.Le:
                    result = (Bool)(v1.CompareTo(v2) <= 0);
                    break;
                case CompareOp.Gt:
                    result = (Bool)(v1.CompareTo(v2) > 0);
                    break;
                case CompareOp.Ge:
                    result = (Bool)(v1.CompareTo(v2) >= 0);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(op), op, null);
            }

            return result;
        }

        #endregion

        #region bool Comparison

        public static Bool Compare(CompareOp op, Bool d1, Bool d2)
        {
            if (!Undefined.IsDefined(d1) || !Undefined.IsDefined(d2))
            {
                return Bool.Undefined;
            }

            Bool result;
            switch (op)
            {
                case CompareOp.Eq:
                    result = (Bool)(d1.CompareTo(d2) == 0);
                    break;
                case CompareOp.Ne:
                    result = (Bool)(d1.CompareTo(d2) != 0);
                    break;
                case CompareOp.Lt:
                    result = (Bool)(d1.CompareTo(d2) < 0);
                    break;
                case CompareOp.Le:
                    result = (Bool)(d1.CompareTo(d2) <= 0);
                    break;
                case CompareOp.Gt:
                    result = (Bool)(d1.CompareTo(d2) > 0);
                    break;
                case CompareOp.Ge:
                    result = (Bool)(d1.CompareTo(d2) >= 0);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(op), op, null);
            }

            return result;
        }

        #endregion

        #region Null Comparison

        public static Bool Compare(CompareOp op, Null n1, Null n2)
        {
            Bool result;
            switch (op)
            {
                case CompareOp.Eq:
                case CompareOp.Le:
                case CompareOp.Ge:
                    result = Bool.True;
                    break;
                case CompareOp.Ne:
                case CompareOp.Lt:
                case CompareOp.Gt:
                    result = Bool.False;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(op), op, null);
            }

            return result;
        }

        #endregion Null

        #region Undefined Comparison

        public static Bool Compare(CompareOp op, Undefined u1, Undefined u2)
        {
            Bool result;
            switch (op)
            {
                case CompareOp.Eq:
                case CompareOp.Ne:
                case CompareOp.Lt:
                case CompareOp.Le:
                case CompareOp.Gt:
                case CompareOp.Ge:
                    result = Bool.Undefined;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(op), op, null);
            }

            return result;
        }

        #endregion
    }
}
