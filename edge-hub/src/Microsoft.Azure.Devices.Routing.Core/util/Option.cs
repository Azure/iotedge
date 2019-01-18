// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Util
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Globalization;

    public struct Option<T> : IEquatable<Option<T>>
    {
        internal Option(T value, bool hasValue)
        {
            this.Value = value;
            this.HasValue = hasValue;
        }

        public bool HasValue { get; }

        T Value { get; }

        public static bool operator ==(Option<T> opt1, Option<T> opt2) => opt1.Equals(opt2);

        public static bool operator !=(Option<T> opt1, Option<T> opt2) => !opt1.Equals(opt2);

        public bool Equals(Option<T> other)
        {
            if (!this.HasValue && !other.HasValue)
            {
                return true;
            }
            else if (this.HasValue && other.HasValue)
            {
                return EqualityComparer<T>.Default.Equals(this.Value, other.Value);
            }

            return false;
        }

        public override bool Equals(object obj) => obj is Option<T> && this.Equals((Option<T>)obj);

        public override int GetHashCode()
        {
            if (this.HasValue)
            {
                return this.Value == null ? 1 : this.Value.GetHashCode();
            }

            return 0;
        }

        public override string ToString() =>
            this.Map(v => v != null ? string.Format(CultureInfo.InvariantCulture, "Some({0})", v) : "Some(null)").GetOrElse("None");

        public IEnumerable<T> ToEnumerable()
        {
            if (this.HasValue)
            {
                yield return this.Value;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            if (this.HasValue)
            {
                yield return this.Value;
            }
        }

        public bool Contains(T value)
        {
            if (this.HasValue)
            {
                return this.Value == null ? value == null : this.Value.Equals(value);
            }

            return false;
        }

        public bool Exists(Func<T, bool> predicate) => this.HasValue && predicate(this.Value);

        public T GetOrElse(T alternative) => this.HasValue ? this.Value : alternative;

        public Option<T> Else(Option<T> alternativeOption) => this.HasValue ? this : alternativeOption;

        public T OrDefault() => this.HasValue ? this.Value : default(T);

        public TResult Match<TResult>(Func<T, TResult> some, Func<TResult> none) => this.HasValue ? some(this.Value) : none();

        public void ForEach(Action<T> action)
        {
            if (this.HasValue)
            {
                action(this.Value);
            }
        }

        public Option<TResult> Map<TResult>(Func<T, TResult> mapping)
        {
            return this.Match(
                some: value => Option.Some(mapping(value)),
                none: Option.None<TResult>);
        }

        public Option<TResult> FlatMap<TResult>(Func<T, Option<TResult>> mapping)
        {
            return this.Match(
                some: mapping,
                none: Option.None<TResult>);
        }

        [Pure]
        public Option<T> Filter(Func<T, bool> predicate)
        {
            Option<T> original = this;
            return this.Match(
                some: value => predicate(value) ? original : Option.None<T>(),
                none: () => original);
        }
    }

    public static class Option
    {
        public static Option<T> Some<T>(T value) => new Option<T>(value, true);

        public static Option<T> None<T>() => new Option<T>(default(T), false);
    }
}
