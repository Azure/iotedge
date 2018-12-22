// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Query
{
    using System.Diagnostics;

    public struct Bool
    {
        readonly int value;

        public static readonly Bool Undefined = new Bool(0);
        public static readonly Bool False = new Bool(-1);
        public static readonly Bool True = new Bool(1);

        Bool(int value)
        {
            Debug.Assert(value >= -1 && value <= 1);
            this.value = value;
        }

        public static explicit operator Bool(bool x) => x ? True : False;

        public static implicit operator bool(Bool x) => x.value > 0;

        public static Bool operator ==(Bool x, Bool y)
        {
            if (x.value == 0 || y.value == 0)
            {
                return Undefined;
            }
            return x.value == y.value ? True : False;
        }

        public static Bool operator !=(Bool x, Bool y)
        {
            if (x.value == 0 || y.value == 0)
            {
                return Undefined;
            }
            return x.value != y.value ? True : False;
        }

        public static Bool operator !(Bool x)
        {
            return x.value == 0 ? Undefined : x.value == -1 ? True : False;
        }

        public static Bool operator &(Bool x, Bool y)
        {
            if (x.value == 0)
            {
                return y.value == -1 ? False : Undefined;
            }
            else if (y.value == 0)
            {
                return x.value == -1 ? False : Undefined;
            }
            return x.value == -1 || y.value == -1 ? False : True;
        }

        public static Bool operator |(Bool x, Bool y)
        {
            if (x.value == 0)
            {
                return y.value == 1 ? True : Undefined;
            }
            else if (y.value == 0)
            {
                return x.value == 1 ? True : Undefined;
            }
            return x.value == 1 || y.value == 1 ? True : False;
        }

        public static bool operator true(Bool x) => x.value > 0;

        public static bool operator false(Bool x) => x.value <= 0;

        // Conversions
        public static implicit operator Bool(double x) => Undefined;

        public static implicit operator Bool(string x) => Undefined;

        public int CompareTo(Bool other) => this.value - other.value;

        public bool Equals(Bool other) => this.value == other.value;

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            return obj is Bool && this.Equals((Bool)obj);
        }

        public override int GetHashCode() => this.value;

        public override string ToString()
        {
            switch (this.value)
            {
                case -1:
                    return "Bool.False";
                case 0:
                    return "Bool.Undefined";
                case 1:
                    return "Bool.True";
                default:
                    return "Bool.Unknown";
            }
        }
    }
}
