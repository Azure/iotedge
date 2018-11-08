// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Query.Errors
{
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class CompilationError
    {
        public CompilationError(ErrorSeverity severity, string message, ErrorRange location)
        {
            this.Severity = severity;
            this.Message = Preconditions.CheckNotNull(message);
            this.Location = Preconditions.CheckNotNull(location);
        }

        public ErrorRange Location { get; }

        public string Message { get; }

        public ErrorSeverity Severity { get; }

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
