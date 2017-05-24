// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    public struct Option<T> : IEquatable<Option<T>>
    {
        public bool HasValue { get; }

        T Value { get; }

        internal Option(T value, bool hasValue)
        {
            this.Value = value;
            this.HasValue = hasValue;
        }

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

        public static bool operator ==(Option<T> opt1, Option<T> opt2) => opt1.Equals(opt2);

        public static bool operator !=(Option<T> opt1, Option<T> opt2) => !opt1.Equals(opt2);

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

        /// <summary>
        /// Conditionally invokes <paramref name="action"/> with the value of this option
        /// object if this option has a value. This method is a no-op if there is no value
        /// stored in this option.
        /// </summary>
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
                none: Option.None<TResult>
            );
        }

        public Option<TResult> FlatMap<TResult>(Func<T, Option<TResult>> mapping) => this.Match(
            some: mapping,
            none: Option.None<TResult>
        );

        /// <summary>
        /// This method returns <c>this</c> if <paramref name="predicate"/> returns <c>true</c> and
        /// <c>Option.None&lt;T&gt;()</c> if it returns <c>false</c>.
        /// </summary>
        /// <param name="predicate">The callback function defining the filter condition.</param>
        /// <returns><c>this</c> if <paramref name="predicate"/> returns <c>true</c> and
        /// <c>Option.None&lt;T&gt;()</c> if it returns <c>false</c></returns>
        /// <remarks>
        /// Think of this like a standard C# "if" statement. For e.g., the following code:
        /// 
        /// <code>
        /// Option&lt;string&gt; o = Option.Some("foo");
        /// o.Filter(s =&gt; s.Contains("foo")).ForEach(s =&gt; Console.WriteLine($"s = {s}"));
        /// </code>
        /// 
        /// is semantically equivalent to:
        /// 
        /// <code>
        /// string s = "foo";
        /// if (s != null &amp;&amp; s.Contains("foo"))
        /// {
        ///     Console.WriteLine($"s = {s}");
        /// }
        /// </code>
        /// </remarks>
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
        /// <summary>
        /// Creates an <c>Option &lt;T&gt;</c> with <paramref name="value"/> and marks
        /// the option object as having a value, i.e., <c>Option&lt;T&gt;.HasValue == true</c>.
        /// </summary>
        public static Option<T> Some<T>(T value) => new Option<T>(value, true);

        /// <summary>
        /// Creates an <c>Option &lt;T&gt;</c> with a default value (<c>default(T)</c>) and marks
        /// the option object as having no value, i.e., <c>Option&lt;T&gt;.HasValue == false</c>.
        /// </summary>
        public static Option<T> None<T>() => new Option<T>(default(T), false);
    }
}