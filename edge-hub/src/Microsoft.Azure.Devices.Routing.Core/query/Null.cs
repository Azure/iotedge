// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Query
{
    public struct Null
    {
        public static Null Instance { get; } = new Null();

        // Add
        public static double operator +(Null x, double y) => y;

        public static double operator +(double x, Null y) => x;

        public static double operator +(Null x, Null y) => 0;

        // And
        public static Null operator &(Null x, Bool y) => Instance;

        public static Null operator &(Bool x, Null y) => Instance;

        public static Null operator &(Null x, Null y) => Instance;

        // Or
        public static Bool operator |(Null x, Bool y) => y;

        public static Bool operator |(Bool x, Null y) => x;

        public static Null operator |(Null x, Null y) => Instance;

        // Divide
        public static double operator /(Null x, double y) => 0;

        public static double operator /(double x, Null y) => x >= 0 ? double.PositiveInfinity : double.NegativeInfinity;

        public static double operator /(Null x, Null y) => double.NaN;

        // Equal
        public static Bool operator ==(Null x, Null y) => Bool.True;

        public static Bool operator ==(Bool x, Null y) => Bool.Undefined;

        public static Bool operator ==(Null x, Bool y) => Bool.Undefined;

        public static Bool operator ==(double x, Null y) => Bool.Undefined;

        public static Bool operator ==(Null x, double y) => Bool.Undefined;

        public static Bool operator ==(string x, Null y) => Bool.Undefined;

        public static Bool operator ==(Null x, string y) => Bool.Undefined;

        // Conversions
        public static explicit operator Bool(Null x) => Bool.False;

        // GreaterThan
        public static Bool operator >(Null x, Null y) => Bool.False;

        public static Bool operator >(Null x, double y) => Bool.Undefined;

        public static Bool operator >(double x, Null y) => Bool.Undefined;

        public static Bool operator >(Null x, Bool y) => Bool.Undefined;

        public static Bool operator >(Bool x, Null y) => Bool.Undefined;

        public static Bool operator >(Null x, string y) => Bool.Undefined;

        public static Bool operator >(string x, Null y) => Bool.Undefined;

        // GreaterThanOrEqual
        public static Bool operator >=(Null x, Null y) => Bool.True;

        public static Bool operator >=(Null x, double y) => Bool.Undefined;

        public static Bool operator >=(double x, Null y) => Bool.Undefined;

        public static Bool operator >=(Null x, Bool y) => Bool.Undefined;

        public static Bool operator >=(Bool x, Null y) => Bool.Undefined;

        public static Bool operator >=(Null x, string y) => Bool.Undefined;

        public static Bool operator >=(string x, Null y) => Bool.Undefined;

        // Not Equal
        public static Bool operator !=(Null x, Null y) => Bool.False;

        public static Bool operator !=(Bool x, Null y) => Bool.Undefined;

        public static Bool operator !=(Null x, Bool y) => Bool.Undefined;

        public static Bool operator !=(double x, Null y) => Bool.Undefined;

        public static Bool operator !=(Null x, double y) => Bool.Undefined;

        public static Bool operator !=(string x, Null y) => Bool.Undefined;

        public static Bool operator !=(Null x, string y) => Bool.Undefined;

        // LessThan
        public static Bool operator <(Null x, Null y) => Bool.False;

        public static Bool operator <(Null x, double y) => Bool.Undefined;

        public static Bool operator <(double x, Null y) => Bool.Undefined;

        public static Bool operator <(Null x, Bool y) => Bool.Undefined;

        public static Bool operator <(Bool x, Null y) => Bool.Undefined;

        public static Bool operator <(Null x, string y) => Bool.Undefined;

        public static Bool operator <(string x, Null y) => Bool.Undefined;

        // LessThanOrEqual
        public static Bool operator <=(Null x, Null y) => Bool.True;

        public static Bool operator <=(Null x, double y) => Bool.Undefined;

        public static Bool operator <=(double x, Null y) => Bool.Undefined;

        public static Bool operator <=(Null x, Bool y) => Bool.Undefined;

        public static Bool operator <=(Bool x, Null y) => Bool.Undefined;

        public static Bool operator <=(Null x, string y) => Bool.Undefined;

        public static Bool operator <=(string x, Null y) => Bool.Undefined;

        // Not
        public static Bool operator !(Null x) => Bool.True;

        // Modulo
        public static double operator %(Null x, double y) => 0;

        public static double operator %(double x, Null y) => double.NaN;

        public static double operator %(Null x, Null y) => double.NaN;

        // Multiply
        public static double operator *(Null x, double y) => 0;

        public static double operator *(double x, Null y) => 0;

        public static double operator *(Null x, Null y) => 0;

        // Subtract
        public static double operator -(Null x, double y) => -y;

        public static double operator -(double x, Null y) => x;

        public static double operator -(Null x, Null y) => 0;

        // Unary Minus
        public static double operator -(Null x) => -0;

        public bool Equals(Null other) => true;

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            return obj is Null && this.Equals((Null)obj);
        }

        public override int GetHashCode() => 0;

        public override string ToString() => "null";
    }
}
