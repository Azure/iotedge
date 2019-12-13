// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLibTest
{
    using System;
    using DevOpsLib;
    using NUnit.Framework;

    [TestFixture]
    public class ValidationUtilTest
    {
        [Test]
        public void TestThrowIfNullOrWhiteSpace([Values(null, "")]string value)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => ValidationUtil.ThrowIfNullOrWhiteSpace(value, nameof(value)));

            Assert.NotNull(ex);
            Assert.True(ex.Message.StartsWith("Cannot be null or white space."));
            Assert.AreEqual("value", ex.ParamName);
        }

        [Test]
        public void TestThrowIfNullOrWhiteSpaceWithoutFieldName([Values(null, "")]string value)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => ValidationUtil.ThrowIfNullOrWhiteSpace(value, null));

            Assert.NotNull(ex);
            Assert.True(ex.Message.StartsWith("Cannot be null or white space."));
            Assert.AreEqual(string.Empty, ex.ParamName);
        }

        [Test]
        public void TestThrowIfNonPositive([Values(-1, 0)]int value)
        {
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => ValidationUtil.ThrowIfNonPositive(value, nameof(value)));

            Assert.NotNull(ex);
            Assert.True(ex.Message.StartsWith("Cannot be less than or equal to zero."));
            Assert.AreEqual("value", ex.ParamName);
        }

        [Test]
        public void TestThrowIfNonPositiveWithoutFieldName([Values(-1, 0)]int value)
        {
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => ValidationUtil.ThrowIfNonPositive(value, null));

            Assert.NotNull(ex);
            Assert.True(ex.Message.StartsWith("Cannot be less than or equal to zero."));
            Assert.AreEqual(string.Empty, ex.ParamName);
        }

        [Test]
        public void TestThrowIfNegative([Values(-1)]int value)
        {
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => ValidationUtil.ThrowIfNegative(value, nameof(value)));

            Assert.NotNull(ex);
            Assert.True(ex.Message.StartsWith("Cannot be negative."));
            Assert.AreEqual("value", ex.ParamName);
        }

        [Test]
        public void TestThrowIfNegativeWithoutFieldName([Values(-1)]int value)
        {
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => ValidationUtil.ThrowIfNegative(value, null));

            Assert.NotNull(ex);
            Assert.True(ex.Message.StartsWith("Cannot be negative."));
            Assert.AreEqual(string.Empty, ex.ParamName);
        }

        [Test]
        public void TestThrowIfNulOrEmptySet([Values(null, new int[]{})]int[] value)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => ValidationUtil.ThrowIfNulOrEmptySet(value, nameof(value)));

            Assert.NotNull(ex);
            Assert.True(ex.Message.StartsWith("Cannot be null or empty collection."));
            Assert.AreEqual("value", ex.ParamName);
        }

        [Test]
        public void TestThrowIfNulOrEmptySetWithoutFieldName([Values(null, new int[] { })]int[] value)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => ValidationUtil.ThrowIfNulOrEmptySet(value, null));

            Assert.NotNull(ex);
            Assert.True(ex.Message.StartsWith("Cannot be null or empty collection."));
            Assert.AreEqual(string.Empty, ex.ParamName);
        }

        [Test]
        public void TestThrowIfNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => ValidationUtil.ThrowIfNull(null, "value"));

            Assert.NotNull(ex);
            Assert.True(ex.Message.StartsWith("Cannot be null."));
            Assert.AreEqual("value", ex.ParamName);
        }

        [Test]
        public void TestThrowIfNullWithoutFieldName()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => ValidationUtil.ThrowIfNull(null, null));

            Assert.NotNull(ex);
            Assert.True(ex.Message.StartsWith("Cannot be null."));
            Assert.AreEqual(string.Empty, ex.ParamName);
        }

    }
}
