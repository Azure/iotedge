// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Diff
{
    using System.Collections.Generic;

    public sealed class Update<T>
    {
        public T From { get; }

        public T To { get; }

        public Update(T from, T to)
        {
            this.From = from;
            this.To = to;
        }

        bool Equals(Update<T> other) => EqualityComparer<T>.Default.Equals(this.From, other.From) && EqualityComparer<T>.Default.Equals(this.To, other.To);

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is Update<T> other && this.Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (EqualityComparer<T>.Default.GetHashCode(this.From) * 397) ^ EqualityComparer<T>.Default.GetHashCode(this.To);
            }
        }
    }
}
