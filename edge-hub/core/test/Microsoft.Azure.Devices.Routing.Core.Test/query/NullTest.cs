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
    public class NullTest : RoutingUnitTestBase
    {
        [Fact]
        [Unit]
        public void SmokeTest()
        {
            BinaryExpression expression = Expression.LessThan(Expression.Add(Expression.Constant(3.0, typeof(double)), Expression.Constant(Null.Instance)), Expression.Constant(4.0));
            Func<bool> rule = Expression.Lambda<Func<bool>>(expression).Compile();
            Assert.True(rule());
        }

        [Fact]
        [Unit]
        public void TestEquals()
        {
            var d1 = default(Null);
            var d2 = default(Null);
            Assert.True(d1 == d2);
            Assert.False(d1 != d2);

            Assert.Equal(Bool.Undefined, d1 == Bool.True);
            Assert.Equal(Bool.Undefined, d1 == Bool.False);
        }

        [Fact]
        [Unit]
        public void TestArthimetic()
        {
            var d1 = default(Null);
            var d2 = default(Null);

            Assert.Equal(12.34, d1 + 12.34);
            Assert.Equal(12.34, 12.34 + d1);
            Assert.Equal(0, d1 + d2);

            Assert.Equal(-12.34, d1 - 12.34);
            Assert.Equal(12.34, 12.34 - d1);
            Assert.Equal(0, d1 - d2);

            Assert.Equal(0, d1 * 4.0);
            Assert.Equal(0, 4.0 * d1);
            Assert.Equal(0, d1 * d2);

            Assert.Equal(0, d1 / 4.0);
            Assert.Equal(double.PositiveInfinity, 4.0 / d1);
            Assert.Equal(double.NegativeInfinity, -4.0 / d1);
            Assert.Equal(double.NaN, d1 / d2);
        }

        [Fact]
        [Unit]
        public void TestComparison()
        {
            var d1 = default(Null);
            var d2 = default(Null);

            Assert.Equal(Bool.Undefined, d1 < 12.34);
            Assert.Equal(Bool.Undefined, d1 > 12.34);
            Assert.Equal(Bool.Undefined, d1 < "string");
            Assert.Equal(Bool.Undefined, d1 > "string");
            Assert.Equal(Bool.Undefined, d1 < Bool.True);
            Assert.Equal(Bool.Undefined, d1 > Bool.True);
            Assert.Equal(Bool.False, d1 < d2);

            Assert.Equal(Bool.Undefined, d1 > 12.34);
            Assert.Equal(Bool.Undefined, d1 < 12.34);
            Assert.Equal(Bool.Undefined, d1 > "string");
            Assert.Equal(Bool.Undefined, d1 < "string");
            Assert.Equal(Bool.Undefined, d1 > Bool.True);
            Assert.Equal(Bool.Undefined, d1 < Bool.True);
            Assert.Equal(Bool.False, d1 > d2);

            Assert.Equal(Bool.Undefined, d1 <= 12.34);
            Assert.Equal(Bool.Undefined, d1 >= 12.34);
            Assert.Equal(Bool.Undefined, d1 <= "string");
            Assert.Equal(Bool.Undefined, d1 >= "string");
            Assert.Equal(Bool.Undefined, d1 <= Bool.True);
            Assert.Equal(Bool.Undefined, d1 >= Bool.True);
            Assert.Equal(Bool.True, d1 <= d2);

            Assert.Equal(Bool.Undefined, d1 >= 12.34);
            Assert.Equal(Bool.Undefined, d1 <= 12.34);
            Assert.Equal(Bool.Undefined, d1 >= "string");
            Assert.Equal(Bool.Undefined, d1 <= "string");
            Assert.Equal(Bool.Undefined, d1 >= Bool.True);
            Assert.Equal(Bool.Undefined, d1 <= Bool.True);
            Assert.Equal(Bool.True, d1 >= d2);
        }

        [Fact]
        [Unit]
        public void TestExpression()
        {
            // Add
            Func<double> expression11 = Expression.Lambda<Func<double>>(Expression.Add(Expression.Constant(3.0D), Expression.Constant(Null.Instance))).Compile();
            Func<double> expression12 = Expression.Lambda<Func<double>>(Expression.Add(Expression.Constant(Null.Instance), Expression.Constant(3.0D))).Compile();
            Func<double> expression13 = Expression.Lambda<Func<double>>(Expression.Add(Expression.Constant(Null.Instance), Expression.Constant(Null.Instance))).Compile();
            Func<double> expression14 = Expression.Lambda<Func<double>>(Expression.Add(Expression.Constant(3.0D), Expression.Constant(3.0D))).Compile();
            Assert.Equal(3.0, expression11());
            Assert.Equal(3.0, expression12());
            Assert.Equal(0.0, expression13());
            Assert.Equal(6.0D, expression14());

            // Subtract
            Func<double> expression21 = Expression.Lambda<Func<double>>(Expression.Subtract(Expression.Constant(3.0D), Expression.Constant(Null.Instance))).Compile();
            Func<double> expression22 = Expression.Lambda<Func<double>>(Expression.Subtract(Expression.Constant(Null.Instance), Expression.Constant(3.0D))).Compile();
            Func<double> expression23 = Expression.Lambda<Func<double>>(Expression.Subtract(Expression.Constant(Null.Instance), Expression.Constant(Null.Instance))).Compile();
            Func<double> expression24 = Expression.Lambda<Func<double>>(Expression.Subtract(Expression.Constant(3.0D), Expression.Constant(3.0D))).Compile();
            Assert.Equal(3.0, expression21());
            Assert.Equal(-3.0, expression22());
            Assert.Equal(0.0, expression23());
            Assert.Equal(0.0, expression24());

            // Multiply
            Func<double> expression31 = Expression.Lambda<Func<double>>(Expression.Multiply(Expression.Constant(3.0D), Expression.Constant(Null.Instance))).Compile();
            Func<double> expression32 = Expression.Lambda<Func<double>>(Expression.Multiply(Expression.Constant(Null.Instance), Expression.Constant(3.0D))).Compile();
            Func<double> expression33 = Expression.Lambda<Func<double>>(Expression.Multiply(Expression.Constant(Null.Instance), Expression.Constant(Null.Instance))).Compile();
            Func<double> expression34 = Expression.Lambda<Func<double>>(Expression.Multiply(Expression.Constant(3.0D), Expression.Constant(3.0D))).Compile();
            Assert.Equal(0.0, expression31());
            Assert.Equal(0.0, expression32());
            Assert.Equal(0.0, expression33());
            Assert.Equal(9.0, expression34());

            // Divide
            Func<double> expression41 = Expression.Lambda<Func<double>>(Expression.Divide(Expression.Constant(3.0D), Expression.Constant(Null.Instance))).Compile();
            Func<double> expression42 = Expression.Lambda<Func<double>>(Expression.Divide(Expression.Constant(Null.Instance), Expression.Constant(3.0D))).Compile();
            Func<double> expression43 = Expression.Lambda<Func<double>>(Expression.Divide(Expression.Constant(Null.Instance), Expression.Constant(Null.Instance))).Compile();
            Func<double> expression44 = Expression.Lambda<Func<double>>(Expression.Divide(Expression.Constant(3.0D), Expression.Constant(3.0D))).Compile();
            Assert.Equal(double.PositiveInfinity, expression41());
            Assert.Equal(0.0, expression42());
            Assert.Equal(double.NaN, expression43());
            Assert.Equal(1.0, expression44());

            // Less Than
            Func<Bool> expression51 = Expression.Lambda<Func<Bool>>(Expression.LessThan(Expression.Constant(3.0D), Expression.Constant(Null.Instance))).Compile();
            Func<Bool> expression52 = Expression.Lambda<Func<Bool>>(Expression.LessThan(Expression.Constant(Null.Instance), Expression.Constant(3.0D))).Compile();
            Func<Bool> expression53 = Expression.Lambda<Func<Bool>>(Expression.LessThan(Expression.Constant(Null.Instance), Expression.Constant(Null.Instance))).Compile();
            Func<bool> expression54 = Expression.Lambda<Func<bool>>(Expression.LessThan(Expression.Constant(3.0D), Expression.Constant(3.0D))).Compile();
            Assert.Equal(Bool.Undefined, expression51());
            Assert.Equal(Bool.Undefined, expression52());
            Assert.Equal(Bool.False, expression53());
            Assert.False(expression54());

            // Greater Than
            Func<Bool> expression61 = Expression.Lambda<Func<Bool>>(Expression.GreaterThan(Expression.Constant(3.0D), Expression.Constant(Null.Instance))).Compile();
            Func<Bool> expression62 = Expression.Lambda<Func<Bool>>(Expression.GreaterThan(Expression.Constant(Null.Instance), Expression.Constant(3.0D))).Compile();
            Func<Bool> expression63 = Expression.Lambda<Func<Bool>>(Expression.GreaterThan(Expression.Constant(Null.Instance), Expression.Constant(Null.Instance))).Compile();
            Func<bool> expression64 = Expression.Lambda<Func<bool>>(Expression.GreaterThan(Expression.Constant(3.0D), Expression.Constant(3.0D))).Compile();
            Assert.Equal(Undefined.Instance, expression61());
            Assert.Equal(Undefined.Instance, expression62());
            Assert.Equal(Bool.False, expression63());
            Assert.False(expression64());

            // Less Than Or Equal
            Func<Bool> expression71 = Expression.Lambda<Func<Bool>>(Expression.LessThanOrEqual(Expression.Constant(3.0D), Expression.Constant(Null.Instance))).Compile();
            Func<Bool> expression72 = Expression.Lambda<Func<Bool>>(Expression.LessThanOrEqual(Expression.Constant(Null.Instance), Expression.Constant(3.0D))).Compile();
            Func<Bool> expression73 = Expression.Lambda<Func<Bool>>(Expression.LessThanOrEqual(Expression.Constant(Null.Instance), Expression.Constant(Null.Instance))).Compile();
            Func<bool> expression74 = Expression.Lambda<Func<bool>>(Expression.LessThanOrEqual(Expression.Constant(3.0D), Expression.Constant(3.0D))).Compile();
            Assert.Equal(Bool.Undefined, expression71());
            Assert.Equal(Bool.Undefined, expression72());
            Assert.Equal(Bool.True, expression73());
            Assert.True(expression74());

            // Greater Than Or Equal
            Func<Bool> expression81 = Expression.Lambda<Func<Bool>>(Expression.GreaterThanOrEqual(Expression.Constant(3.0D), Expression.Constant(Null.Instance))).Compile();
            Func<Bool> expression82 = Expression.Lambda<Func<Bool>>(Expression.GreaterThanOrEqual(Expression.Constant(Null.Instance), Expression.Constant(3.0D))).Compile();
            Func<Bool> expression83 = Expression.Lambda<Func<Bool>>(Expression.GreaterThanOrEqual(Expression.Constant(Null.Instance), Expression.Constant(Null.Instance))).Compile();
            Func<bool> expression84 = Expression.Lambda<Func<bool>>(Expression.GreaterThanOrEqual(Expression.Constant(3.0D), Expression.Constant(3.0D))).Compile();
            Assert.Equal(Bool.Undefined, expression81());
            Assert.Equal(Bool.Undefined, expression82());
            Assert.Equal(Bool.True, expression83());
            Assert.True(expression84());

            // Equal
            Func<Bool> expression91 = Expression.Lambda<Func<Bool>>(Expression.Equal(Expression.Constant(3.0D), Expression.Constant(Null.Instance))).Compile();
            Func<Bool> expression92 = Expression.Lambda<Func<Bool>>(Expression.Equal(Expression.Constant(Null.Instance), Expression.Constant(3.0D))).Compile();
            Func<Bool> expression93 = Expression.Lambda<Func<Bool>>(Expression.Equal(Expression.Constant(Null.Instance), Expression.Constant(Null.Instance))).Compile();
            Func<bool> expression99 = Expression.Lambda<Func<bool>>(Expression.Equal(Expression.Constant(3.0D), Expression.Constant(3.0D))).Compile();
            Assert.Equal(Bool.Undefined, expression91());
            Assert.Equal(Bool.Undefined, expression92());
            Assert.Equal(Bool.True, expression93());
            Assert.True(expression99());

            // NotEqual
            Func<Bool> expression101 = Expression.Lambda<Func<Bool>>(Expression.NotEqual(Expression.Constant(3.0D), Expression.Constant(Null.Instance))).Compile();
            Func<Bool> expression102 = Expression.Lambda<Func<Bool>>(Expression.NotEqual(Expression.Constant(Null.Instance), Expression.Constant(3.0D))).Compile();
            Func<Bool> expression103 = Expression.Lambda<Func<Bool>>(Expression.NotEqual(Expression.Constant(Null.Instance), Expression.Constant(Null.Instance))).Compile();
            Func<bool> expression104 = Expression.Lambda<Func<bool>>(Expression.NotEqual(Expression.Constant(3.0D), Expression.Constant(3.0D))).Compile();
            Assert.Equal(Bool.Undefined, expression101());
            Assert.Equal(Bool.Undefined, expression102());
            Assert.Equal(Bool.False, expression103());
            Assert.False(expression104());

            // Modulo
            Func<double> expression111 = Expression.Lambda<Func<double>>(Expression.Modulo(Expression.Constant(3.0D), Expression.Constant(Null.Instance))).Compile();
            Func<double> expression112 = Expression.Lambda<Func<double>>(Expression.Modulo(Expression.Constant(Null.Instance), Expression.Constant(3.0D))).Compile();
            Func<double> expression113 = Expression.Lambda<Func<double>>(Expression.Modulo(Expression.Constant(Null.Instance), Expression.Constant(Null.Instance))).Compile();
            Func<double> expression114 = Expression.Lambda<Func<double>>(Expression.Modulo(Expression.Constant(3.0D), Expression.Constant(3.0D))).Compile();
            Assert.Equal(double.NaN, expression111());
            Assert.Equal(0.0, expression112());
            Assert.Equal(double.NaN, expression113());
            Assert.Equal(0.0, expression114());

            // Uninary Minus
            Func<double> expression121 = Expression.Lambda<Func<double>>(Expression.Negate(Expression.Constant(Null.Instance))).Compile();
            Func<double> expression124 = Expression.Lambda<Func<double>>(Expression.Negate(Expression.Constant(3.0D))).Compile();
            Assert.Equal(-0.0, expression121());
            Assert.Equal(-3.0, expression124());

            // Not
            Func<Bool> expression131 = Expression.Lambda<Func<Bool>>(Expression.Not(Expression.Constant(Null.Instance))).Compile();
            Assert.Equal(Bool.True, expression131());
        }
    }
}
