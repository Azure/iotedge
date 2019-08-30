// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Query
{
    public struct Null
    {
        public static Null Instance { get; } = default(Null);

        // Equal
        public static Bool operator ==(Null x, Null y) => Bool.True;

        public static Bool operator ==(Bool x, Null y) => Bool.Undefined;

        public static Bool operator ==(Null x, Bool y) => Bool.Undefined;

        public static Bool operator ==(double x, Null y) => Bool.Undefined;

        public static Bool operator ==(Null x, double y) => Bool.Undefined;

        public static Bool operator ==(string x, Null y) => Bool.Undefined;

        public static Bool operator ==(Null x, string y) => Bool.Undefined;

        // Not Equal
        public static Bool operator !=(Null x, Null y) => Bool.False;

        public static Bool operator !=(Bool x, Null y) => Bool.Undefined;

        public static Bool operator !=(Null x, Bool y) => Bool.Undefined;

        public static Bool operator !=(double x, Null y) => Bool.Undefined;

        public static Bool operator !=(Null x, double y) => Bool.Undefined;

        public static Bool operator !=(string x, Null y) => Bool.Undefined;

        public static Bool operator !=(Null x, string y) => Bool.Undefined;

        // Add
        public static double operator +(Null x, double y) => y;

        public static double operator +(double x, Null y) => x;

        public static double operator +(Null x, Null y) => 0;

        // Subtract
        public static double operator -(Null x, double y) => -y;

        public static double operator -(double x, Null y) => x;

        public static double operator -(Null x, Null y) => 0;

        // Multiply
        public static double operator *(Null x, double y) => 0;

        public static double operator *(double x, Null y) => 0;

        public static double operator *(Null x, Null y) => 0;

        // Divide
        public static double operator /(Null x, double y) => 0;

        public static double operator /(double x, Null y) => x >= 0 ? double.PositiveInfinity : double.NegativeInfinity;

        public static double operator /(Null x, Null y) => double.NaN;

        // Modulo
        public static double operator %(Null x, double y) => 0;

        public static double operator %(double x, Null y) => double.NaN;

        public static double operator %(Null x, Null y) => double.NaN;

        // Unary Minus
        public static double operator -(Null x) => -0;

        // LessThan
        public static Bool operator <(Null x, Null y) => Bool.False;

        public static Bool operator <(Null x, double y) => Bool.Undefined;

        public static Bool operator <(double x, Null y) => Bool.Undefined;

        public static Bool operator <(Null x, Bool y) => Bool.Undefined;

        public static Bool operator <(Bool x, Null y) => Bool.Undefined;

        public static Bool operator <(Null x, string y) => Bool.Undefined;

        public static Bool operator <(string x, Null y) => Bool.Undefined;

        // GreaterThan
        public static Bool operator >(Null x, Null y) => Bool.False;

        public static Bool operator >(Null x, double y) => Bool.Undefined;

        public static Bool operator >(double x, Null y) => Bool.Undefined;

        public static Bool operator >(Null x, Bool y) => Bool.Undefined;

        public static Bool operator >(Bool x, Null y) => Bool.Undefined;

        public static Bool operator >(Null x, string y) => Bool.Undefined;

        public static Bool operator >(string x, Null y) => Bool.Undefined;

        // LessThanOrEqual
        public static Bool operator <=(Null x, Null y) => Bool.True;

        public static Bool operator <=(Null x, double y) => Bool.Undefined;

        public static Bool operator <=(double x, Null y) => Bool.Undefined;

        public static Bool operator <=(Null x, Bool y) => Bool.Undefined;

        public static Bool operator <=(Bool x, Null y) => Bool.Undefined;

        public static Bool operator <=(Null x, string y) => Bool.Undefined;

        public static Bool operator <=(string x, Null y) => Bool.Undefined;

        // GreaterThanOrEqual
        public static Bool operator >=(Null x, Null y) => Bool.True;

        public static Bool operator >=(Null x, double y) => Bool.Undefined;

        public static Bool operator >=(double x, Null y) => Bool.Undefined;

        public static Bool operator >=(Null x, Bool y) => Bool.Undefined;

        public static Bool operator >=(Bool x, Null y) => Bool.Undefined;

        public static Bool operator >=(Null x, string y) => Bool.Undefined;

        public static Bool operator >=(string x, Null y) => Bool.Undefined;

        // Not
        public static Bool operator !(Null x) => Bool.True;

        // And
        public static Null operator &(Null x, Bool y) => Instance;

        public static Null operator &(Bool x, Null y) => Instance;

        public static Null operator &(Null x, Null y) => Instance;

        // Or
        public static Bool operator |(Null x, Bool y) => y;

        public static Bool operator |(Bool x, Null y) => x;

        public static Null operator |(Null x, Null y) => Instance;

        // Conversions
        public static explicit operator Bool(Null x) => Bool.False;

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
