// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Query
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq.Expressions;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Query;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class UndefinedTest : RoutingUnitTestBase
    {
        [Fact]
        [Unit]
        public void SmokeTest()
        {
            BinaryExpression expression = Expression.LessThan(Expression.Add(Expression.Constant(3.0, typeof(double)), Expression.Constant(Undefined.Instance)), Expression.Constant(4.0));
            Func<bool> rule = Expression.Lambda<Func<bool>>(Expression.Convert(expression, typeof(bool))).Compile();
            Assert.False(rule());
        }

        [Fact]
        [Unit]
        public void TestEquals()
        {
            var d1 = default(Undefined);
            var d2 = default(Undefined);
            Assert.Equal(Bool.Undefined, d1 == d2);
            Assert.Equal(Bool.Undefined, d1 != d2);

            Assert.Equal(Bool.Undefined, d1 == Bool.True);
            Assert.Equal(Bool.Undefined, d1 == Bool.False);
        }

        [Fact]
        [Unit]
        public void TestTypes()
        {
            Assert.Equal(Bool.True, Undefined.IsDefined("undefined"));
            Assert.Equal(Bool.False, Undefined.IsDefined((string)Undefined.Instance));
        }

        [Fact]
        [Unit]
        public void TestArthimetic()
        {
            var d1 = default(Undefined);
            var d2 = default(Undefined);

            Assert.Equal(double.NaN, d1 + 12.34);
            Assert.Equal(double.NaN, 12.34 + d1);
            Assert.Equal(double.NaN, d1 + d2);

            Assert.Equal(double.NaN, d1 - 12.34);
            Assert.Equal(double.NaN, 12.34 - d1);
            Assert.Equal(double.NaN, d1 - d2);

            Assert.Equal(double.NaN, d1 * 4.0);
            Assert.Equal(double.NaN, 4.0 * d1);
            Assert.Equal(double.NaN, d1 * d2);

            Assert.Equal(double.NaN, d1 / 4.0);
            Assert.Equal(double.NaN, 4.0 / d1);
            Assert.Equal(double.NaN, d1 / d2);
        }

        [Fact]
        [Unit]
        public void TestComparison()
        {
            var d1 = default(Undefined);
            var d2 = default(Undefined);

            Assert.Equal(Bool.Undefined, d1 < 12.34);
            Assert.Equal(Bool.Undefined, d1 > 12.34);
            Assert.Equal(Bool.Undefined, d1 < d2);

            Assert.Equal(Bool.Undefined, d1 > 12.34);
            Assert.Equal(Bool.Undefined, d1 < 12.34);
            Assert.Equal(Bool.Undefined, d1 > d2);

            Assert.Equal(Bool.Undefined, d1 <= 12.34);
            Assert.Equal(Bool.Undefined, d1 >= 12.34);
            Assert.Equal(Bool.Undefined, d1 <= d2);

            Assert.Equal(Bool.Undefined, d1 >= 12.34);
            Assert.Equal(Bool.Undefined, d1 <= 12.34);
            Assert.Equal(Bool.Undefined, d1 >= d2);
        }

        [Fact]
        [Unit]
        public void TestExpression()
        {
            // Add
            Func<Undefined> expression11 = Expression.Lambda<Func<Undefined>>(Expression.Add(Expression.Constant(3.0D), Expression.Constant(Undefined.Instance))).Compile();
            Func<Undefined> expression12 = Expression.Lambda<Func<Undefined>>(Expression.Add(Expression.Constant(Undefined.Instance), Expression.Constant(3.0D))).Compile();
            Func<Undefined> expression13 = Expression.Lambda<Func<Undefined>>(Expression.Add(Expression.Constant(Undefined.Instance), Expression.Constant(Undefined.Instance))).Compile();
            Func<double> expression14 = Expression.Lambda<Func<double>>(Expression.Add(Expression.Constant(3.0D), Expression.Constant(3.0D))).Compile();
            Assert.Equal(Undefined.Instance, expression11());
            Assert.Equal(Undefined.Instance, expression12());
            Assert.Equal(Undefined.Instance, expression13());
            Assert.Equal(6.0D, expression14());

            // Subtract
            Func<Undefined> expression21 = Expression.Lambda<Func<Undefined>>(Expression.Subtract(Expression.Constant(3.0D), Expression.Constant(Undefined.Instance))).Compile();
            Func<Undefined> expression22 = Expression.Lambda<Func<Undefined>>(Expression.Subtract(Expression.Constant(Undefined.Instance), Expression.Constant(3.0D))).Compile();
            Func<Undefined> expression23 = Expression.Lambda<Func<Undefined>>(Expression.Subtract(Expression.Constant(Undefined.Instance), Expression.Constant(Undefined.Instance))).Compile();
            Func<double> expression24 = Expression.Lambda<Func<double>>(Expression.Subtract(Expression.Constant(3.0D), Expression.Constant(3.0D))).Compile();
            Assert.Equal(Undefined.Instance, expression21());
            Assert.Equal(Undefined.Instance, expression22());
            Assert.Equal(Undefined.Instance, expression23());
            Assert.Equal(0.0, expression24());

            // Multiply
            Func<Undefined> expression31 = Expression.Lambda<Func<Undefined>>(Expression.Multiply(Expression.Constant(3.0D), Expression.Constant(Undefined.Instance))).Compile();
            Func<Undefined> expression32 = Expression.Lambda<Func<Undefined>>(Expression.Multiply(Expression.Constant(Undefined.Instance), Expression.Constant(3.0D))).Compile();
            Func<Undefined> expression33 = Expression.Lambda<Func<Undefined>>(Expression.Multiply(Expression.Constant(Undefined.Instance), Expression.Constant(Undefined.Instance))).Compile();
            Func<double> expression34 = Expression.Lambda<Func<double>>(Expression.Multiply(Expression.Constant(3.0D), Expression.Constant(3.0D))).Compile();
            Assert.Equal(Undefined.Instance, expression31());
            Assert.Equal(Undefined.Instance, expression32());
            Assert.Equal(Undefined.Instance, expression33());
            Assert.Equal(9.0, expression34());

            // Divide
            Func<Undefined> expression41 = Expression.Lambda<Func<Undefined>>(Expression.Divide(Expression.Constant(3.0D), Expression.Constant(Undefined.Instance))).Compile();
            Func<Undefined> expression42 = Expression.Lambda<Func<Undefined>>(Expression.Divide(Expression.Constant(Undefined.Instance), Expression.Constant(3.0D))).Compile();
            Func<Undefined> expression43 = Expression.Lambda<Func<Undefined>>(Expression.Divide(Expression.Constant(Undefined.Instance), Expression.Constant(Undefined.Instance))).Compile();
            Func<double> expression44 = Expression.Lambda<Func<double>>(Expression.Divide(Expression.Constant(3.0D), Expression.Constant(3.0D))).Compile();
            Assert.Equal(Undefined.Instance, expression41());
            Assert.Equal(Undefined.Instance, expression42());
            Assert.Equal(Undefined.Instance, expression43());
            Assert.Equal(1.0, expression44());

            // Less Than
            Func<Bool> expression51 = Expression.Lambda<Func<Bool>>(Expression.LessThan(Expression.Constant(3.0D), Expression.Constant(Undefined.Instance))).Compile();
            Func<Bool> expression52 = Expression.Lambda<Func<Bool>>(Expression.LessThan(Expression.Constant(Undefined.Instance), Expression.Constant(3.0D))).Compile();
            Func<Bool> expression53 = Expression.Lambda<Func<Bool>>(Expression.LessThan(Expression.Constant(Undefined.Instance), Expression.Constant(Undefined.Instance))).Compile();
            Func<bool> expression54 = Expression.Lambda<Func<bool>>(Expression.LessThan(Expression.Constant(3.0D), Expression.Constant(3.0D))).Compile();
            Assert.Equal(Bool.Undefined, expression51());
            Assert.Equal(Bool.Undefined, expression52());
            Assert.Equal(Bool.Undefined, expression53());
            Assert.False(expression54());

            // Greater Than
            Func<Bool> expression61 = Expression.Lambda<Func<Bool>>(Expression.GreaterThan(Expression.Constant(3.0D), Expression.Constant(Undefined.Instance))).Compile();
            Func<Bool> expression62 = Expression.Lambda<Func<Bool>>(Expression.GreaterThan(Expression.Constant(Undefined.Instance), Expression.Constant(3.0D))).Compile();
            Func<Bool> expression63 = Expression.Lambda<Func<Bool>>(Expression.GreaterThan(Expression.Constant(Undefined.Instance), Expression.Constant(Undefined.Instance))).Compile();
            Func<bool> expression64 = Expression.Lambda<Func<bool>>(Expression.GreaterThan(Expression.Constant(3.0D), Expression.Constant(3.0D))).Compile();
            Assert.Equal(Bool.Undefined, expression61());
            Assert.Equal(Bool.Undefined, expression62());
            Assert.Equal(Bool.Undefined, expression63());
            Assert.False(expression64());

            // Less Than Or Equal
            Func<Bool> expression71 = Expression.Lambda<Func<Bool>>(Expression.LessThanOrEqual(Expression.Constant(3.0D), Expression.Constant(Undefined.Instance))).Compile();
            Func<Bool> expression72 = Expression.Lambda<Func<Bool>>(Expression.LessThanOrEqual(Expression.Constant(Undefined.Instance), Expression.Constant(3.0D))).Compile();
            Func<Bool> expression73 = Expression.Lambda<Func<Bool>>(Expression.LessThanOrEqual(Expression.Constant(Undefined.Instance), Expression.Constant(Undefined.Instance))).Compile();
            Func<bool> expression74 = Expression.Lambda<Func<bool>>(Expression.LessThanOrEqual(Expression.Constant(3.0D), Expression.Constant(3.0D))).Compile();
            Assert.Equal(Bool.Undefined, expression71());
            Assert.Equal(Bool.Undefined, expression72());
            Assert.Equal(Bool.Undefined, expression73());
            Assert.True(expression74());

            // Greater Than Or Equal
            Func<Bool> expression81 = Expression.Lambda<Func<Bool>>(Expression.GreaterThanOrEqual(Expression.Constant(3.0D), Expression.Constant(Undefined.Instance))).Compile();
            Func<Bool> expression82 = Expression.Lambda<Func<Bool>>(Expression.GreaterThanOrEqual(Expression.Constant(Undefined.Instance), Expression.Constant(3.0D))).Compile();
            Func<Bool> expression83 = Expression.Lambda<Func<Bool>>(Expression.GreaterThanOrEqual(Expression.Constant(Undefined.Instance), Expression.Constant(Undefined.Instance))).Compile();
            Func<bool> expression84 = Expression.Lambda<Func<bool>>(Expression.GreaterThanOrEqual(Expression.Constant(3.0D), Expression.Constant(3.0D))).Compile();
            Assert.Equal(Bool.Undefined, expression81());
            Assert.Equal(Bool.Undefined, expression82());
            Assert.Equal(Bool.Undefined, expression83());
            Assert.True(expression84());

            // Equal
            Func<Bool> expression91 = Expression.Lambda<Func<Bool>>(Expression.Equal(Expression.Constant(3.0D), Expression.Constant(Undefined.Instance))).Compile();
            Func<Bool> expression92 = Expression.Lambda<Func<Bool>>(Expression.Equal(Expression.Constant(Undefined.Instance), Expression.Constant(3.0D))).Compile();
            Func<Bool> expression93 = Expression.Lambda<Func<Bool>>(Expression.Equal(Expression.Constant(Undefined.Instance), Expression.Constant(Undefined.Instance))).Compile();
            Func<bool> expression99 = Expression.Lambda<Func<bool>>(Expression.Equal(Expression.Constant(3.0D), Expression.Constant(3.0D))).Compile();
            Assert.Equal(Bool.Undefined, expression91());
            Assert.Equal(Bool.Undefined, expression92());
            Assert.Equal(Bool.Undefined, expression93());
            Assert.True(expression99());

            // NotEqual
            Func<Bool> expression101 = Expression.Lambda<Func<Bool>>(Expression.NotEqual(Expression.Constant(3.0D), Expression.Constant(Undefined.Instance))).Compile();
            Func<Bool> expression102 = Expression.Lambda<Func<Bool>>(Expression.NotEqual(Expression.Constant(Undefined.Instance), Expression.Constant(3.0D))).Compile();
            Func<Bool> expression103 = Expression.Lambda<Func<Bool>>(Expression.NotEqual(Expression.Constant(Undefined.Instance), Expression.Constant(Undefined.Instance))).Compile();
            Func<bool> expression104 = Expression.Lambda<Func<bool>>(Expression.NotEqual(Expression.Constant(3.0D), Expression.Constant(3.0D))).Compile();
            Assert.Equal(Bool.Undefined, expression101());
            Assert.Equal(Bool.Undefined, expression102());
            Assert.Equal(Bool.Undefined, expression103());
            Assert.False(expression104());

            // Modulo
            Func<Undefined> expression111 = Expression.Lambda<Func<Undefined>>(Expression.Modulo(Expression.Constant(3.0D), Expression.Constant(Undefined.Instance))).Compile();
            Func<Undefined> expression112 = Expression.Lambda<Func<Undefined>>(Expression.Modulo(Expression.Constant(Undefined.Instance), Expression.Constant(3.0D))).Compile();
            Func<Undefined> expression113 = Expression.Lambda<Func<Undefined>>(Expression.Modulo(Expression.Constant(Undefined.Instance), Expression.Constant(Undefined.Instance))).Compile();
            Func<double> expression114 = Expression.Lambda<Func<double>>(Expression.Modulo(Expression.Constant(3.0D), Expression.Constant(3.0D))).Compile();
            Assert.Equal(Undefined.Instance, expression111());
            Assert.Equal(Undefined.Instance, expression112());
            Assert.Equal(Undefined.Instance, expression113());
            Assert.Equal(0.0, expression114());

            // Uninary Minus
            Func<Undefined> expression121 = Expression.Lambda<Func<Undefined>>(Expression.Negate(Expression.Constant(Undefined.Instance))).Compile();
            Func<double> expression124 = Expression.Lambda<Func<double>>(Expression.Negate(Expression.Constant(3.0D))).Compile();
            Assert.Equal(Undefined.Instance, expression121());
            Assert.Equal(-3.0, expression124());

            // Not
            Func<Bool> expression131 = Expression.Lambda<Func<Bool>>(Expression.Not(Expression.Constant(Undefined.Instance))).Compile();
            Assert.Equal(Bool.Undefined, expression131());
        }
    }
}
