// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Query.Errors
{
    using System.Globalization;

    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class ErrorRange
    {
        public ErrorRange(ErrorPosition start, ErrorPosition end)
        {
            Preconditions.CheckArgument(
                Preconditions.CheckNotNull(start) <= Preconditions.CheckNotNull(end),
                string.Format(CultureInfo.InvariantCulture, "Start postition must be less than or equal to end position. Given: {0} and {1}", start, end));
            this.Start = start;
            this.End = end;
        }

        public ErrorPosition End { get; }

        public ErrorPosition Start { get; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj.GetType() == this.GetType() && this.Equals((ErrorRange)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (this.Start.GetHashCode() * 397) ^ this.End.GetHashCode();
            }
        }

        protected bool Equals(ErrorRange other)
        {
            return this.Start.Equals(other.Start) && this.End.Equals(other.End);
        }
    }
}
