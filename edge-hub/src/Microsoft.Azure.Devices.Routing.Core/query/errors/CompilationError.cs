// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Query.Errors
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class CompilationError
    {
        public CompilationError(ErrorSeverity severity, string message, ErrorRange location)
        {
            this.Severity = severity;
            this.Message = Preconditions.CheckNotNull(message);
            this.Location = Preconditions.CheckNotNull(location);
        }

        public string Message { get; }

        public ErrorSeverity Severity { get; }

        public ErrorRange Location { get; }

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

            return obj.GetType() == this.GetType() && this.Equals((CompilationError)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.Message.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)this.Severity;
                hashCode = (hashCode * 397) ^ this.Location.GetHashCode();
                return hashCode;
            }
        }

        protected bool Equals(CompilationError other)
        {
            return string.Equals(this.Message, other.Message) && this.Severity == other.Severity && Equals(this.Location, other.Location);
        }
    }
}
