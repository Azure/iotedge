// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.Threading.Tasks;

    public struct Option<T> : IEquatable<Option<T>>
    {
        internal Option(T value, bool hasValue)
        {
            this.Value = value;
            this.HasValue = hasValue;
        }

        public bool HasValue { get; }

        T Value { get; }

        [Pure]
        public static bool operator ==(Option<T> opt1, Option<T> opt2) => opt1.Equals(opt2);

        [Pure]
        public static bool operator !=(Option<T> opt1, Option<T> opt2) => !opt1.Equals(opt2);

        [Pure]
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

        [Pure]
        public override bool Equals(object obj) => obj is Option<T> && this.Equals((Option<T>)obj);

        [Pure]
        public override int GetHashCode()
        {
            if (this.HasValue)
            {
                return this.Value == null ? 1 : this.Value.GetHashCode();
            }

            return 0;
        }

        [Pure]
        public override string ToString() =>
            this.Map(v => v != null ? string.Format(CultureInfo.InvariantCulture, "Some({0})", v) : "Some(null)").GetOrElse("None");

        [Pure]
        public IEnumerable<T> ToEnumerable()
        {
            if (this.HasValue)
            {
                yield return this.Value;
            }
        }

        [Pure]
        public IEnumerator<T> GetEnumerator()
        {
            if (this.HasValue)
            {
                yield return this.Value;
            }
        }

        [Pure]
        public bool Contains(T value)
        {
            if (this.HasValue)
            {
                return this.Value == null ? value == null : this.Value.Equals(value);
            }

            return false;
        }

        /// <summary>
        /// Evaluates to true if and only if the option has a value and <paramref name="predicate"/>
        /// returns <c>true</c>.
        /// </summary>
        [Pure]
        public bool Exists(Func<T, bool> predicate) => this.HasValue && predicate(this.Value);

        /// <summary>
        /// If this option has a value then returns that. If there is no value then returns
        /// <paramref name="alternative"/>.
        /// </summary>
        /// <param name="alternative"></param>
        /// <returns></returns>
        public T GetOrElse(T alternative) => this.HasValue ? this.Value : alternative;

        public T GetOrElse(Func<T> alternativeMaker) => this.HasValue ? this.Value : alternativeMaker();

        public Option<T> Else(Option<T> alternativeOption) => this.HasValue ? this : alternativeOption;

        public Option<T> Else(Func<Option<T>> alternativeMaker) => this.HasValue ? this : alternativeMaker();

        [Pure]
        public T OrDefault() => this.HasValue ? this.Value : default(T);

        public T Expect<TException>(Func<TException> exception)
            where TException : Exception
        {
            return this.HasValue
                ? this.Value
                : throw exception();
        }

        /// <summary>
        /// If the option has a value then it invokes <paramref name="some"/>. If there is no value
        /// then it invokes <paramref name="none"/>.
        /// </summary>
        /// <returns>The value returned by either <paramref name="some"/> or <paramref name="none"/>.</returns>
        [Pure]
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

        public void ForEach(Action<T> action, Action none)
        {
            if (this.HasValue)
            {
                action(this.Value);
            }
            else
            {
                none();
            }
        }

        public Task ForEachAsync(Func<T, Task> action) => this.HasValue ? action(this.Value) : Task.CompletedTask;

        public Task ForEachAsync(Func<T, Task> action, Func<Task> none) => this.HasValue ? action(this.Value) : none();

        /// <summary>
        /// If this option has a value then it transforms it into a new option instance by
        /// calling the <paramref name="mapping"/> callback.  It will follow exception if callback returns null.
        /// Returns <see cref="Option.None{T}"/> if there is no value.
        /// </summary>
        [Pure]
        public Option<TResult> Map<TResult>(Func<T, TResult> mapping)
        {
            return this.HasValue
                ? Option.Some(mapping(this.Value))
                : Option.None<TResult>();
        }

        [Pure]
        public Option<TResult> FlatMap<TResult>(Func<T, Option<TResult>> mapping) => this.Match(
            some: mapping,
            none: Option.None<TResult>);

        /// <summary>
        /// This method returns <c>this</c> if <paramref name="predicate"/> returns <c>true</c> and
        /// <c>Option.None&lt;T&gt;()</c> if it returns <c>false</c>. If the <c>Option&lt;T&gt;</c>
        /// does not have a value then it returns <c>this</c> instance as is.
        /// </summary>
        /// <param name="predicate">The callback function defining the filter condition.</param>
        /// <returns><c>this</c> if <paramref name="predicate"/> returns <c>true</c> and
        /// <c>Option.None&lt;T&gt;()</c> if it returns <c>false</c>. If the option has no
        /// value then it returns <c>this</c> instance as is.</returns>
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
        [Pure]
        public Option<T> Filter(Func<T, bool> predicate)
        {
            Option<T> original = this;
            return this.HasValue
                ? (predicate(this.Value)
                      ? original
                      : Option.None<T>()
                   )
                : original;
        }
    }

    public static class Option
    {
        public static IEnumerable<T> FilterMap<T>(this IEnumerable<Option<T>> source, Func<T, bool> predicate)
        {
            Preconditions.CheckNotNull(source, nameof(source));
            Preconditions.CheckNotNull(predicate, nameof(predicate));

            foreach (var item in source)
            {
                if (item.Filter(predicate).HasValue)
                {
                    yield return item.OrDefault();
                }
            }
        }

        public static IEnumerable<T> FilterMap<T>(this IEnumerable<Option<T>> source)
        {
            Preconditions.CheckNotNull(source, nameof(source));

            foreach (var item in source)
            {
                if (item.HasValue)
                {
                    yield return item.OrDefault();
                }
            }
        }

        /// <summary>
        /// Creates an <c>Option &lt;T&gt;</c> with <paramref name="value"/> and marks
        /// the option object as having a value, i.e., <c>Option&lt;T&gt;.HasValue == true</c>.
        /// </summary>
        public static Option<T> Some<T>(T value)
        {
            Preconditions.CheckNotNull(value, nameof(value));

            return new Option<T>(value, true);
        }

        /// <summary>
        /// Creates an <c>Option &lt;T&gt;</c> with a default value (<c>default(T)</c>) and marks
        /// the option object as having no value, i.e., <c>Option&lt;T&gt;.HasValue == false</c>.
        /// </summary>
        public static Option<T> None<T>() => new Option<T>(default(T), false);

        public static Option<T> Maybe<T>(T value)
            where T : class => value == null ? None<T>() : Some(value);

        public static Option<T> Maybe<T>(T? value)
            where T : struct, IComparable => value.HasValue ? Some(value.Value) : None<T>();
    }
}
