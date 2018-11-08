// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Query.Errors
{
    using System;
    using System.Globalization;

    using Microsoft.Azure.Devices.Routing.Core.Util;

    /// <summary>
    /// Compilation error line number and column. Line and column both start at 1
    /// </summary>
    public struct ErrorPosition : IComparable<ErrorPosition>
    {
        public ErrorPosition(int line, int column)
        {
            this.Line = Preconditions.CheckRange(line, 1);
            this.Column = Preconditions.CheckRange(column, 1);
        }

        public int Column { get; }

        public int Line { get; }

        public static bool operator ==(ErrorPosition x, ErrorPosition y)
        {
            return x.Equals(y);
        }

        public static bool operator >(ErrorPosition x, ErrorPosition y)
        {
            return x.CompareTo(y) > 0;
        }

        public static bool operator >=(ErrorPosition x, ErrorPosition y)
        {
            return x.CompareTo(y) >= 0;
        }

        public static bool operator !=(ErrorPosition x, ErrorPosition y)
        {
            return !x.Equals(y);
        }

        public static bool operator <(ErrorPosition x, ErrorPosition y)
        {
            return x.CompareTo(y) < 0;
        }

        public static bool operator <=(ErrorPosition x, ErrorPosition y)
        {
            return x.CompareTo(y) <= 0;
        }

        public int CompareTo(ErrorPosition other)
        {
            if (this.Line < other.Line)
            {
                return -1;
            }
            else if (this.Line > other.Line)
            {
                return 1;
            }
            else
            {
                return this.Column.CompareTo(other.Column);
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            return obj.GetType() == this.GetType() && this.Equals((ErrorPosition)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (this.Line * 397) ^ this.Column;
            }
        }

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0}:{1}", this.Line, this.Column);

        bool Equals(ErrorPosition other)
        {
            bool lines = this.Line == other.Line;
            bool columns = this.Column == other.Column;
            return lines && columns;
        }
    }
}
