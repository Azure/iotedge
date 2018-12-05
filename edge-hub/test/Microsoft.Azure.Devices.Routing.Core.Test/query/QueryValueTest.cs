// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core.Test.Query
{
    using System;
    using Microsoft.Azure.Devices.Routing.Core.Query;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class QueryValueTest
    {
        static readonly string DefaultRandomString = Guid.NewGuid().ToString();
        static readonly object DefaultEmptyObject = new object();

        static readonly QueryValue UndefinedQueryValue = QueryValue.Undefined;
        static readonly QueryValue NullQueryValue = QueryValue.Null;
        static readonly QueryValue BoolQueryValue = new QueryValue((Bool)true, QueryValueType.Bool);
        static readonly QueryValue DoubleQueryValue = new QueryValue(10, QueryValueType.Double);
        static readonly QueryValue StringQueryValue = new QueryValue(DefaultRandomString, QueryValueType.String);
        static readonly QueryValue ObjectQueryValue = new QueryValue(DefaultEmptyObject, QueryValueType.Object);

        [Fact, Unit]
        public void QueryValue_Undefined()
        {
            Assert.True(UndefinedQueryValue.CompareTo(QueryValue.Undefined) != 0);

            Assert.True(UndefinedQueryValue.CompareTo(BoolQueryValue) != 0);
            Assert.True(UndefinedQueryValue.CompareTo(NullQueryValue) != 0);
            Assert.True(UndefinedQueryValue.CompareTo(DoubleQueryValue) != 0);
            Assert.True(UndefinedQueryValue.CompareTo(StringQueryValue) != 0);
            Assert.True(UndefinedQueryValue.CompareTo(ObjectQueryValue) != 0);

            // Different object type comparisons
            Assert.True(UndefinedQueryValue.CompareTo(Guid.NewGuid()) != 0);
            Assert.True(UndefinedQueryValue.CompareTo(DateTime.UtcNow) != 0);
            Assert.True(UndefinedQueryValue.CompareTo(new InvalidOperationException()) != 0);
        }

        [Fact, Unit]
        public void QueryValue_Null()
        {
            Assert.Equal(NullQueryValue, QueryValue.Null);
            Assert.True(NullQueryValue.CompareTo(QueryValue.Null) == 0);

            Assert.True(NullQueryValue.CompareTo(UndefinedQueryValue) != 0);
            Assert.True(NullQueryValue.CompareTo(BoolQueryValue) != 0);
            Assert.True(NullQueryValue.CompareTo(DoubleQueryValue) != 0);
            Assert.True(NullQueryValue.CompareTo(StringQueryValue) != 0);
            Assert.True(NullQueryValue.CompareTo(ObjectQueryValue) != 0);

            // Different object type comparisons
            Assert.True(NullQueryValue.CompareTo(Guid.NewGuid()) != 0);
            Assert.True(NullQueryValue.CompareTo(DateTime.UtcNow) != 0);
            Assert.True(NullQueryValue.CompareTo(new InvalidOperationException()) != 0);
        }

        [Fact, Unit]
        public void QueryValue_Bool()
        {
            Assert.True(BoolQueryValue.CompareTo(Bool.True) == 0);
            Assert.True(BoolQueryValue.CompareTo(true) != 0);
            Assert.Equal(BoolQueryValue.CompareTo("true") == 0, Bool.False);

            Assert.True(BoolQueryValue.CompareTo(UndefinedQueryValue) != 0);
            Assert.True(BoolQueryValue.CompareTo(NullQueryValue) != 0);
            Assert.True(BoolQueryValue.CompareTo(DoubleQueryValue) != 0);
            Assert.True(BoolQueryValue.CompareTo(StringQueryValue) != 0);
            Assert.True(BoolQueryValue.CompareTo(ObjectQueryValue) != 0);

            // Different object type comparisons
            Assert.True(BoolQueryValue.CompareTo(Guid.NewGuid()) != 0);
            Assert.True(BoolQueryValue.CompareTo(DateTime.UtcNow) != 0);
            Assert.True(BoolQueryValue.CompareTo(new InvalidOperationException()) != 0);
        }

        [Fact, Unit]
        public void QueryValue_Double()
        {
            Assert.True(DoubleQueryValue.CompareTo(15.0) < 0);
            Assert.True(DoubleQueryValue.CompareTo(10.00) == 0);

            Assert.True(DoubleQueryValue.CompareTo(BoolQueryValue) != 0);
            Assert.True(DoubleQueryValue.CompareTo(UndefinedQueryValue) != 0);
            Assert.True(DoubleQueryValue.CompareTo(NullQueryValue) != 0);
            Assert.True(DoubleQueryValue.CompareTo(StringQueryValue) != 0);
            Assert.True(DoubleQueryValue.CompareTo(ObjectQueryValue) != 0);

            // Different object type comparisons
            Assert.True(DoubleQueryValue.CompareTo(Guid.NewGuid()) != 0);
            Assert.True(DoubleQueryValue.CompareTo(DateTime.UtcNow) != 0);
            Assert.True(DoubleQueryValue.CompareTo(new InvalidOperationException()) != 0);

            var longDoubleQueryValue = new QueryValue(1212312312312312312L, QueryValueType.Double);
            Assert.True(longDoubleQueryValue.CompareTo(123) != 0);
        }

        [Fact, Unit]
        public void QueryValue_String()
        {
            string stringToCompare = Guid.NewGuid().ToString();

            Assert.True(StringQueryValue.CompareTo(DefaultRandomString) == 0);
            Assert.Equal(StringQueryValue.CompareTo(stringToCompare),
                string.Compare(DefaultRandomString, stringToCompare, StringComparison.Ordinal));

            Assert.True(StringQueryValue.CompareTo(BoolQueryValue) != 0);
            Assert.True(StringQueryValue.CompareTo(UndefinedQueryValue) != 0);
            Assert.True(StringQueryValue.CompareTo(NullQueryValue) != 0);
            Assert.True(StringQueryValue.CompareTo(DoubleQueryValue) != 0);
            Assert.True(StringQueryValue.CompareTo(ObjectQueryValue) != 0);

            // Different object type comparisons
            Assert.True(StringQueryValue.CompareTo(Guid.NewGuid()) != 0);
            Assert.True(StringQueryValue.CompareTo(DateTime.UtcNow) != 0);
            Assert.True(StringQueryValue.CompareTo(new InvalidOperationException()) != 0);
        }

        [Fact, Unit]
        public void QueryValue_Object()
        {
            var objectToCompare = new object();

            Assert.True(ObjectQueryValue.CompareTo(ObjectQueryValue) == 0);
            Assert.True(ObjectQueryValue.CompareTo(objectToCompare) != 0);
            Assert.True(ObjectQueryValue.CompareTo(DefaultEmptyObject) != 0); //Fail because comparison is not on a QueryValue object.

            Assert.True(ObjectQueryValue.CompareTo(BoolQueryValue) != 0);
            Assert.True(ObjectQueryValue.CompareTo(UndefinedQueryValue) != 0);
            Assert.True(ObjectQueryValue.CompareTo(NullQueryValue) != 0);
            Assert.True(ObjectQueryValue.CompareTo(DoubleQueryValue) != 0);
            Assert.True(ObjectQueryValue.CompareTo(StringQueryValue) != 0);

            // Different object type comparisons
            Assert.True(ObjectQueryValue.CompareTo("random string") != 0);
            Assert.True(ObjectQueryValue.CompareTo(123) != 0);
            Assert.True(ObjectQueryValue.CompareTo(true) != 0);
            Assert.True(ObjectQueryValue.CompareTo(false) != 0);
            Assert.True(ObjectQueryValue.CompareTo(Guid.NewGuid()) != 0);
            Assert.True(ObjectQueryValue.CompareTo(DateTime.UtcNow) != 0);
            Assert.True(ObjectQueryValue.CompareTo(new InvalidOperationException()) != 0);
        }

        [Fact, Unit]
        public void QueryValue_None()
        {
            var noneQueryValue = new QueryValue(null, QueryValueType.None);

            // Check Undefined to Undefined comparison always returns -1
            Assert.Equal(QueryValue.Undefined.CompareTo(QueryValue.Undefined), -1);

            // Check any other None comparisons are reflexive.
            Assert.Equal(QueryValue.Undefined.CompareTo(noneQueryValue), -1 * noneQueryValue.CompareTo(QueryValue.Undefined));
        }
    }
}
