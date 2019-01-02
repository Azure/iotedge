// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Query.Errors
{
    using System.Globalization;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class ErrorRange
    {
        public ErrorPosition Start { get; }

        public ErrorPosition End { get; }

        public ErrorRange(ErrorPosition start, ErrorPosition end)
        {
            Preconditions.CheckArgument(Preconditions.CheckNotNull(start) <= Preconditions.CheckNotNull(end),
                string.Format(CultureInfo.InvariantCulture, "Start postition must be less than or equal to end position. Given: {0} and {1}", start, end));
            this.Start = start;
            this.End = end;
        }

        protected bool Equals(ErrorRange other)
        {
            return this.Start.Equals(other.Start) && this.End.Equals(other.End);
        }

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

    }
}
