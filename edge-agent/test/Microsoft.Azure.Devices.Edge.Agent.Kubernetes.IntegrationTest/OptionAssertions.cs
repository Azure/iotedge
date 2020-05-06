// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.IntegrationTest
{
    using System.Diagnostics.Contracts;
    using FluentAssertions;
    using FluentAssertions.Execution;
    using FluentAssertions.Primitives;
    using Microsoft.Azure.Devices.Edge.Util;

    public static class OptionAssertionExtensions
    {
        [Pure]
        public static OptionAssertions<T> Should<T>(this Option<T> actualValue) => new OptionAssertions<T>(actualValue);
    }

    public class OptionAssertions<T> : ReferenceTypeAssertions<Option<T>, OptionAssertions<T>>
    {
        public OptionAssertions(Option<T> actualValue)
            : base(actualValue)
        {
        }

        protected override string Identifier => typeof(Option<T>).ToString();

        public AndConstraint<OptionAssertions<T>> BeNone(
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .ForCondition(this.Subject != null)
                .BecauseOf(because, becauseArgs).WithDefaultIdentifier(this.Identifier)
                .FailWith("Expected {context} to be <None>{reason}, but found {0}.", (object)this.Subject);

            Execute.Assertion
                .ForCondition(this.Subject.Equals(Option.None<T>()))
                .BecauseOf(because, becauseArgs).WithDefaultIdentifier(this.Identifier)
                .FailWith("Expected {context} to be <None>{reason}, but found {0}.", (object)this.Subject);

            return new AndConstraint<OptionAssertions<T>>(this);
        }

        public AndConstraint<OptionAssertions<T>> NotBeNone(
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .ForCondition(this.Subject != null)
                .BecauseOf(because, becauseArgs).WithDefaultIdentifier(this.Identifier)
                .FailWith("Expected {context} not to be <None>{reason}, but found {0}.", (object)this.Subject);

            Execute.Assertion
                .ForCondition(!this.Subject.Equals(Option.None<T>()))
                .BecauseOf(because, becauseArgs).WithDefaultIdentifier(this.Identifier)
                .FailWith("Expected {context} not to be <None>{reason}, but found {0}.", (object)this.Subject);

            return new AndConstraint<OptionAssertions<T>>(this);
        }

        public AndConstraint<OptionAssertions<T>> Be(
            Option<T> expected,
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .ForCondition(this.Subject != null)
                .BecauseOf(because, becauseArgs).WithDefaultIdentifier(this.Identifier)
                .FailWith("Expected {context} to be <{0}>{reason}, but found {1}.", expected, (object)this.Subject);

            Execute.Assertion
                .ForCondition(this.Subject.Equals(expected))
                .BecauseOf(because, becauseArgs).WithDefaultIdentifier(this.Identifier)
                .FailWith("Expected {context} to be <{0}>{reason}, but found {1}.", expected, (object)this.Subject);

            return new AndConstraint<OptionAssertions<T>>(this);
        }

        public AndConstraint<OptionAssertions<T>> NotBe(
            Option<T> expected,
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .ForCondition(this.Subject != null)
                .BecauseOf(because, becauseArgs).WithDefaultIdentifier(this.Identifier)
                .FailWith("Expected {context} not to be <{0}>{reason}, but found {1}.", expected, (object)this.Subject);

            Execute.Assertion
                .ForCondition(!this.Subject.Equals(expected))
                .BecauseOf(because, becauseArgs).WithDefaultIdentifier(this.Identifier)
                .FailWith("Expected {context} not to be <{0}>{reason}, but found {1}.", expected, (object)this.Subject);

            return new AndConstraint<OptionAssertions<T>>(this);
        }

        public AndConstraint<OptionAssertions<T>> BeSome(
            T expected,
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .ForCondition(this.Subject == null)
                .BecauseOf(because, becauseArgs).WithDefaultIdentifier(this.Identifier)
                .FailWith("Expected {context} to be <Some({0})>{reason}, but found {1}.", expected, (object)this.Subject);

            Execute.Assertion
                .ForCondition(this.Subject.Equals(Option.Some(expected)))
                .BecauseOf(because, becauseArgs).WithDefaultIdentifier(this.Identifier)
                .FailWith("Expected {context} to be <Some({0})>{reason}, but found {1}.", expected, (object)this.Subject);

            return new AndConstraint<OptionAssertions<T>>(this);
        }

        public AndConstraint<OptionAssertions<T>> NotBeSome(
            T expected,
            string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion
                .ForCondition(this.Subject == null)
                .BecauseOf(because, becauseArgs).WithDefaultIdentifier(this.Identifier)
                .FailWith("Expected {context} not to be <Some({0})>{reason}, but found {1}.", expected, (object)this.Subject);

            Execute.Assertion
                .ForCondition(this.Subject.Equals(Option.Some(expected)))
                .BecauseOf(because, becauseArgs).WithDefaultIdentifier(this.Identifier)
                .FailWith("Expected {context} not to be <Some({0})>{reason}, but found {1}.", expected, (object)this.Subject);

            return new AndConstraint<OptionAssertions<T>>(this);
        }
    }
}
