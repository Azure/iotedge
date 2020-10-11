// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;

    public interface ITry<T>
    {
        T Value { get; }

        Exception Exception { get; }

        Option<T> Ok();

        Option<T> ToOption();

        bool Success { get; }

        ITry<TU> Map<TU>(Func<T, TU> mapper);

        ITry<TU> FlatMap<TU>(Func<T, ITry<TU>> mapper);

        ITry<TU> Fold<TU>(Func<T, TU> valueHandler, Func<Exception, TU> exceptionHandler);

        ITry<T> OrElse(Func<ITry<T>> supplier);

        ITry<T> GetOrElse(Func<T> supplier);

        ITry<T> GetOrElse(T value);
    }

    public static class Try
    {
        public static ITry<T> Success<T>(T value) => new Succeed<T>(value);

        public static ITry<T> Failure<T>(Exception exception) => new Failure<T>(exception);
    }

    class Succeed<T> : ITry<T>
    {
        public Succeed(T value)
        {
            this.Value = value;
        }

        public T Value { get; }

        public Exception Exception => throw new InvalidOperationException("Success instance of Try has no exception.");

        public bool Success => true;

        public Option<T> ToOption() => this.Value == null ? Option.None<T>() : Option.Some(this.Value);

        public Option<T> Ok() => this.ToOption();

        public ITry<TU> Map<TU>(Func<T, TU> mapper)
        {
            try
            {
                return Try.Success(mapper(this.Value));
            }
            catch (Exception ex)
            {
                return Try.Failure<TU>(ex);
            }
        }

        public ITry<TU> FlatMap<TU>(Func<T, ITry<TU>> mapper)
        {
            try
            {
                return mapper(this.Value);
            }
            catch (Exception ex)
            {
                return Try.Failure<TU>(ex);
            }
        }

        public ITry<TU> Fold<TU>(Func<T, TU> valueHandler, Func<Exception, TU> _) => this.Map(valueHandler);

        public ITry<T> OrElse(Func<ITry<T>> _) => this;

        public ITry<T> GetOrElse(Func<T> _) => this;

        public ITry<T> GetOrElse(T _) => this;
    }

    class Failure<T> : ITry<T>
    {
        public Failure(Exception exception)
        {
            this.Exception = exception;
        }

        public Exception Exception { get; }

        public T Value => throw new InvalidOperationException("Failure instance of Try has no value.", this.Exception);

        public bool Success => false;

        public Option<T> Ok() => this.ToOption();

        public Option<T> ToOption() => Option.None<T>();

        public ITry<TU> Map<TU>(Func<T, TU> _) => Try.Failure<TU>(this.Exception);

        public ITry<TU> FlatMap<TU>(Func<T, ITry<TU>> _) => Try.Failure<TU>(this.Exception);

        public ITry<TU> Fold<TU>(Func<T, TU> _, Func<Exception, TU> exceptionHandler)
        {
            try
            {
                return Try.Success(exceptionHandler(this.Exception));
            }
            catch (Exception ex)
            {
                return Try.Failure<TU>(ex);
            }
        }

        public ITry<T> OrElse(Func<ITry<T>> supplier)
        {
            try
            {
                return supplier();
            }
            catch (Exception ex)
            {
                return Try.Failure<T>(ex);
            }
        }

        public ITry<T> GetOrElse(Func<T> supplier)
        {
            try
            {
                return Try.Success(supplier());
            }
            catch (Exception ex)
            {
                return Try.Failure<T>(ex);
            }
        }

        public ITry<T> GetOrElse(T value) => Try.Success(value);
    }
}
