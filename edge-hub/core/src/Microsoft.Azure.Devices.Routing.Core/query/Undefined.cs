// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Query
{
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;

    public struct Undefined
    {
        static readonly string UndefinedString = new string(new[] { 'u', 'n', 'd', 'e', 'f', 'i', 'n', 'e', 'd' });
        static readonly Bool UndefinedBool = Bool.Undefined;
        static readonly double UndefinedDouble = double.NaN;

        public static Undefined Instance { get; } = default(Undefined);

        // Equal
        public static Bool operator ==(Undefined x, Undefined y) => Bool.Undefined;

        public static Bool operator ==(Bool x, Undefined y) => Bool.Undefined;

        public static Bool operator ==(Undefined x, Bool y) => Bool.Undefined;

        public static Bool operator ==(double x, Undefined y) => Bool.Undefined;

        public static Bool operator ==(Undefined x, double y) => Bool.Undefined;

        public static Bool operator ==(string x, Undefined y) => Bool.Undefined;

        public static Bool operator ==(Undefined x, string y) => Bool.Undefined;

        public static Bool operator ==(Null x, Undefined y) => Bool.Undefined;

        public static Bool operator ==(Undefined x, Null y) => Bool.Undefined;

        // Not Equal
        public static Bool operator !=(Undefined x, Undefined y) => Bool.Undefined;

        public static Bool operator !=(Bool x, Undefined y) => Bool.Undefined;

        public static Bool operator !=(Undefined x, Bool y) => Bool.Undefined;

        public static Bool operator !=(double x, Undefined y) => Bool.Undefined;

        public static Bool operator !=(Undefined x, double y) => Bool.Undefined;

        public static Bool operator !=(string x, Undefined y) => Bool.Undefined;

        public static Bool operator !=(Undefined x, string y) => Bool.Undefined;

        public static Bool operator !=(Null x, Undefined y) => Bool.Undefined;

        public static Bool operator !=(Undefined x, Null y) => Bool.Undefined;

        // Add
        public static Undefined operator +(Undefined x, Undefined y) => Instance;

        public static Undefined operator +(Undefined x, double y) => Instance;

        public static Undefined operator +(double x, Undefined y) => Instance;

        public static Undefined operator +(Undefined x, Bool y) => Instance;

        public static Undefined operator +(Bool x, Undefined y) => Instance;

        public static Undefined operator +(Undefined x, string y) => Instance;

        public static Undefined operator +(string x, Undefined y) => Instance;

        public static Undefined operator +(Undefined x, Null y) => Instance;

        public static Undefined operator +(Null x, Undefined y) => Instance;

        // Subtract
        public static Undefined operator -(Undefined x, Undefined y) => Instance;

        public static Undefined operator -(Undefined x, double y) => Instance;

        public static Undefined operator -(double x, Undefined y) => Instance;

        public static Undefined operator -(Undefined x, bool y) => Instance;

        public static Undefined operator -(Bool x, Undefined y) => Instance;

        public static Undefined operator -(Undefined x, string y) => Instance;

        public static Undefined operator -(string x, Undefined y) => Instance;

        public static Undefined operator -(Undefined x, Null y) => Instance;

        public static Undefined operator -(Null x, Undefined y) => Instance;

        // Multiply
        public static Undefined operator *(Undefined x, Undefined y) => Instance;

        public static Undefined operator *(Undefined x, double y) => Instance;

        public static Undefined operator *(double x, Undefined y) => Instance;

        public static Undefined operator *(Undefined x, Bool y) => Instance;

        public static Undefined operator *(Bool x, Undefined y) => Instance;

        public static Undefined operator *(Undefined x, string y) => Instance;

        public static Undefined operator *(string x, Undefined y) => Instance;

        public static Undefined operator *(Undefined x, Null y) => Instance;

        public static Undefined operator *(Null x, Undefined y) => Instance;

        // Divide
        public static Undefined operator /(Undefined x, Undefined y) => Instance;

        public static Undefined operator /(Undefined x, double y) => Instance;

        public static Undefined operator /(double x, Undefined y) => Instance;

        public static Undefined operator /(Undefined x, Bool y) => Instance;

        public static Undefined operator /(Bool x, Undefined y) => Instance;

        public static Undefined operator /(Undefined x, string y) => Instance;

        public static Undefined operator /(string x, Undefined y) => Instance;

        public static Undefined operator /(Undefined x, Null y) => Instance;

        public static Undefined operator /(Null x, Undefined y) => Instance;

        // Modulo
        public static Undefined operator %(Undefined x, Undefined y) => Instance;

        public static Undefined operator %(Undefined x, double y) => Instance;

        public static Undefined operator %(double x, Undefined y) => Instance;

        public static Undefined operator %(Undefined x, Bool y) => Instance;

        public static Undefined operator %(Bool x, Undefined y) => Instance;

        public static Undefined operator %(Undefined x, string y) => Instance;

        public static Undefined operator %(string x, Undefined y) => Instance;

        public static Undefined operator %(Undefined x, Null y) => Instance;

        public static Undefined operator %(Null x, Undefined y) => Instance;

        // Unary Minus
        public static Undefined operator -(Undefined x) => Instance;

        // LessThan
        public static Bool operator <(Undefined x, Undefined y) => Bool.Undefined;

        public static Bool operator <(Undefined x, double y) => Bool.Undefined;

        public static Bool operator <(double x, Undefined y) => Bool.Undefined;

        public static Bool operator <(Undefined x, string y) => Bool.Undefined;

        public static Bool operator <(string x, Undefined y) => Bool.Undefined;

        public static Bool operator <(Undefined x, Bool y) => Bool.Undefined;

        public static Bool operator <(Bool x, Undefined y) => Bool.Undefined;

        public static Bool operator <(Undefined x, Null y) => Bool.Undefined;

        public static Bool operator <(Null x, Undefined y) => Bool.Undefined;

        // GreaterThan
        public static Bool operator >(Undefined x, Undefined y) => Bool.Undefined;

        public static Bool operator >(Undefined x, double y) => Bool.Undefined;

        public static Bool operator >(double x, Undefined y) => Bool.Undefined;

        public static Bool operator >(Undefined x, string y) => Bool.Undefined;

        public static Bool operator >(string x, Undefined y) => Bool.Undefined;

        public static Bool operator >(Undefined x, Bool y) => Bool.Undefined;

        public static Bool operator >(Bool x, Undefined y) => Bool.Undefined;

        public static Bool operator >(Undefined x, Null y) => Bool.Undefined;

        public static Bool operator >(Null x, Undefined y) => Bool.Undefined;

        // LessThanOrEqual
        public static Bool operator <=(Undefined x, Undefined y) => Bool.Undefined;

        public static Bool operator <=(Undefined x, double y) => Bool.Undefined;

        public static Bool operator <=(double x, Undefined y) => Bool.Undefined;

        public static Bool operator <=(Undefined x, string y) => Bool.Undefined;

        public static Bool operator <=(string x, Undefined y) => Bool.Undefined;

        public static Bool operator <=(Undefined x, Bool y) => Bool.Undefined;

        public static Bool operator <=(Bool x, Undefined y) => Bool.Undefined;

        public static Bool operator <=(Undefined x, Null y) => Bool.Undefined;

        public static Bool operator <=(Null x, Undefined y) => Bool.Undefined;

        // GreaterThanOrEqual
        public static Bool operator >=(Undefined x, Undefined y) => Bool.Undefined;

        public static Bool operator >=(Undefined x, double y) => Bool.Undefined;

        public static Bool operator >=(double x, Undefined y) => Bool.Undefined;

        public static Bool operator >=(Undefined x, string y) => Bool.Undefined;

        public static Bool operator >=(string x, Undefined y) => Bool.Undefined;

        public static Bool operator >=(Undefined x, Bool y) => Bool.Undefined;

        public static Bool operator >=(Bool x, Undefined y) => Bool.Undefined;

        public static Bool operator >=(Undefined x, Null y) => Bool.Undefined;

        public static Bool operator >=(Null x, Undefined y) => Bool.Undefined;

        // Not
        public static Bool operator !(Undefined x) => Bool.Undefined;

        // And
        public static Bool operator &(Undefined x, Undefined y) => Bool.Undefined;

        public static Bool operator &(Undefined x, Bool y) => y.Equals(Bool.False) ? Bool.False : Bool.Undefined;

        public static Bool operator &(Bool x, Undefined y) => x.Equals(Bool.False) ? Bool.False : Bool.Undefined;

        public static Bool operator &(Undefined x, double y) => Bool.Undefined;

        public static Bool operator &(double x, Undefined y) => Bool.Undefined;

        public static Bool operator &(Undefined x, string y) => Bool.Undefined;

        public static Bool operator &(string x, Undefined y) => Bool.Undefined;

        // Or
        public static Bool operator |(Undefined x, Undefined y) => Bool.Undefined;

        public static Bool operator |(Undefined x, Bool y) => y.Equals(Bool.True) ? Bool.True : Bool.Undefined;

        public static Bool operator |(Bool x, Undefined y) => x.Equals(Bool.True) ? Bool.True : Bool.Undefined;

        public static Bool operator |(double x, Undefined y) => Bool.Undefined;

        public static Bool operator |(Undefined x, double y) => Bool.Undefined;

        public static Bool operator |(string x, Undefined y) => Bool.Undefined;

        public static Bool operator |(Undefined x, string y) => Bool.Undefined;

        // Conversions
        public static implicit operator double(Undefined x) => UndefinedDouble;

        public static implicit operator Bool(Undefined x) => UndefinedBool;

        public static implicit operator string(Undefined x) => UndefinedString;

        public static Bool IsDefined(string input) => (Bool)!ReferenceEquals(input, UndefinedString);

        public static Bool IsDefined(double input) => (Bool)!double.IsNaN(input);

        public static Bool IsDefined(Bool input) => (Bool)!input.Equals(UndefinedBool);

        public static Bool IsDefined(QueryValue input) => (Bool)(input.ValueType != QueryValueType.None);

        public bool Equals(Undefined other) => true;

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            return obj is Undefined && this.Equals((Undefined)obj);
        }

        public override int GetHashCode() => 0;

        public override string ToString() => "undefined";
    }

    public static class UndefinedExtensions
    {
        public static Bool IsDefined(this string input) => Undefined.IsDefined(input);

        public static Bool IsDefined(this double input) => Undefined.IsDefined(input);

        public static Bool IsDefined(this Bool input) => Undefined.IsDefined(input);

        public static bool IsNullOrUndefined(this string input)
        {
            return input == null || !input.IsDefined();
        }
    }
}
