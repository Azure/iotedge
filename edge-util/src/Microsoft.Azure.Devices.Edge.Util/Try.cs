// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Data;
    using System.Runtime.ExceptionServices;

    public struct Try<T>
    {
        readonly T value;

        public Try(T value)
        {
            this.value = Preconditions.CheckNotNull(value, nameof(value));
            this.Exception = null;
        }

        public Try(Exception exception)
        {
            this.value = default(T);
            this.Exception = Preconditions.CheckNotNull(exception);
        }

        public T Value
        {
            get
            {
                if (this.Exception != null)
                {
                    ExceptionDispatchInfo.Capture(this.Exception).Throw();
                }

                return this.value;
            }
        }

        public Exception Exception { get; }

        public bool Success => this.Exception == null;

        public static Try<T> Failure(Exception exception) => new Try<T>(exception);

        public static implicit operator Try<T>(T value) => new Try<T>(value);

        public Option<T> Ok() => this.Success ? Option.Some(this.Value) : Option.None<T>();

        public Try<TU> Map<TU>(Func<T, TU> mapper)
        {
            if (this.Success)
            {
                var value = this.Value;
                return Try.Of(() => mapper(value));
            }
            else
            {
                return Try.Fail<TU>(this.Exception);
            }
        }

        public Try<TU> FlatMap<TU>(Func<T, Try<TU>> mapper)
        {
            if (this.Success)
            {
                return mapper(this.Value);
            }
            else
            {
                return Try.Fail<TU>(this.Exception);
            }
        }
    }

    public static class Try
    {
        public static Try<T> Success<T>(T value) => new Try<T>(value);

        public static Try<T> Fail<T>(Exception exception) => new Try<T>(exception);

        public static Try<T> Of<T>(Func<T> supplier)
        {
            try
            {
                return Success(supplier());
            }
            catch (Exception e)
            {
                return Fail<T>(e);
            }
        }
    }
}
