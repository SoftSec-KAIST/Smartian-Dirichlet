﻿using System;
using System.Diagnostics;
using System.Numerics;

namespace Decompose.Numerics
{
    public struct UInt128 : IComparable<UInt128>, IEquatable<UInt128>
    {
        private uint r0;
        private uint r1;
        private uint r2;
        private uint r3;

        private static UInt128 maxValue = ~(UInt128)0;
        private static UInt128 zero = (UInt128)0;
        private static UInt128 one = (UInt128)1;

        public static UInt128 MaxValue
        {
            get { return maxValue; }
        }

        public static UInt128 Zero
        {
            get { return zero; }
        }

        public static UInt128 One
        {
            get { return one; }
        }

        public static UInt128 Parse(string value)
        {
            return (UInt128)BigInteger.Parse(value);
        }

        public UInt128(uint r0, uint r1, uint r2, uint r3)
        {
            this.r0 = r0;
            this.r1 = r1;
            this.r2 = r2;
            this.r3 = r3;
        }

        public uint LeastSignificantWord
        {
            get { return r0; }
        }

        public uint R0 { get { return r0; } }
        public uint R1 { get { return r1; } }
        public uint R2 { get { return r2; } }
        public uint R3 { get { return r3; } }

        public bool IsZero { get { return (r3 | r2 | r1 | r0) == 0; } }
        public bool IsOne { get { return ((r3 | r2 | r1) ^ r0) == 1; } }
        public bool IsPowerOfTwo { get { return (this & (this - 1)).IsZero; } }
        public bool IsEven { get { return (r0 & 1) == 0; } }

        public override string ToString()
        {
            return ((BigInteger)this).ToString();
        }
        
        public int GetBitLength()
        {
            if (r3 != 0)
                return r3.GetBitLength() + 96;
            if (r2 != 0)
                return r2.GetBitLength() + 64;
            if (r1 != 0)
                return r1.GetBitLength() + 32;
            return r0.GetBitLength();
        }
        
        public int GetBitCount()
        {
            return r0.GetBitCount() + r1.GetBitCount() + r2.GetBitCount() + r3.GetBitCount();
        }

        public static explicit operator UInt128(double a)
        {
            if (a < 0)
                throw new InvalidCastException();
            return (ulong)a;
        }

        public static explicit operator UInt128(int a)
        {
            if (a < 0)
                throw new InvalidCastException();
            var c = default(UInt128);
            c.r0 = (uint)a;
            return c;
        }

        public static implicit operator UInt128(uint a)
        {
            var c = default(UInt128);
            c.r0 = a;
            return c;
        }

        public static explicit operator UInt128(long a)
        {
            if (a < 0)
                throw new InvalidCastException();
            var c = default(UInt128);
            c.r0 = (uint)a;
            c.r1 = (uint)(a >> 32);
            return c;
        }

        public static implicit operator UInt128(ulong a)
        {
            var c = default(UInt128);
            c.r0 = (uint)a;
            c.r1 = (uint)(a >> 32);
            return c;
        }
        
        public static explicit operator UInt128(BigInteger a)
        {
            var a01 = (ulong)(a & ulong.MaxValue);
            var a23 = (ulong)(a >> 64);
            var c = default(UInt128);
            c.r0 = (uint)a01;
            c.r1 = (uint)(a01 >> 32);
            c.r2 = (uint)a23;
            c.r3 = (uint)(a23 >> 32);
            return c;
        }

        public static explicit operator double(UInt128 a)
        {
            if ((a.r3 | a.r2) == 0)
                return (ulong)a;
            var shift = a.GetBitLength() - 64;
            return (double)(ulong)(a >> shift) * ((long)1 << shift);
        }

        public static explicit operator int(UInt128 a)
        {
            return (int)a.r0;
        }

        public static explicit operator uint(UInt128 a)
        {
            return a.r0;
        }

        public static explicit operator long(UInt128 a)
        {
            return (long)((ulong)a.r1 << 32 | a.r0);
        }

        public static explicit operator ulong(UInt128 a)
        {
            return (ulong)a.r1 << 32 | a.r0;
        }
        
        public static implicit operator BigInteger(UInt128 a)
        {
            return (BigInteger)((ulong)a.r3 << 32 | a.r2) << 64 | (ulong)a.r1 << 32 | a.r0;
        }
        
        public static UInt128 operator <<(UInt128 a, int b)
        {
            UInt128 c;
            LeftShift(out c, ref a, b);
            return c;
        }

        public static UInt128 operator >>(UInt128 a, int b)
        {
            UInt128 c;
            RightShift(out c, ref a, b);
            return c;
        }

        public static UInt128 operator &(UInt128 a, UInt128 b)
        {
            UInt128 c;
            And(out c, ref a, ref b);
            return c;
        }

        public static UInt128 operator |(UInt128 a, UInt128 b)
        {
            UInt128 c;
            Or(out c, ref a, ref b);
            return c;
        }

        public static UInt128 operator ^(UInt128 a, UInt128 b)
        {
            UInt128 c;
            ExclusiveOr(out c, ref a, ref b);
            return c;
        }

        public static UInt128 operator ~(UInt128 a)
        {
            UInt128 c;
            Not(out c, ref a);
            return c;
        }

        public static UInt128 operator +(UInt128 a, UInt128 b)
        {
            UInt128 c;
            Add(out c, ref a, ref b);
            return c;
        }

        public static UInt128 operator ++(UInt128 a)
        {
            UInt128 c;
            Add(out c, ref a, ref one);
            return c;
        }

        public static UInt128 operator -(UInt128 a, UInt128 b)
        {
            UInt128 c;
            Subtract(out c, ref a, ref b);
            return c;
        }

        public static UInt128 operator --(UInt128 a)
        {
            UInt128 c;
            Subtract(out c, ref a, ref one);
            return c;
        }

        public static UInt128 operator *(ulong a, UInt128 b)
        {
            UInt128 c;
            if ((b.r3 | b.r2) != 0)
            {
                UInt128 aa = a;
                Multiply(out c, ref aa, ref b);
            }
            else
                Multiply(out c, (uint)a, (uint)(a >> 32), b.r0, b.r1);
            Debug.Assert((BigInteger)c == (BigInteger)a * (BigInteger)b);
            return c;
        }
        
        public static UInt128 operator *(UInt128 a, ulong b)
        {
            return b * a;
        }
        
        public static UInt128 operator *(UInt128 a, UInt128 b)
        {
            UInt128 c;
            if ((a.r2 | a.r3 | b.r2 | b.r3) != 0)
            {
                Multiply(out c, ref a, ref b);
                Debug.Assert((BigInteger)c == (BigInteger)a * (BigInteger)b);
            }
            else
                Multiply(out c, a.r0, a.r1, b.r0, b.r1);
            Debug.Assert((BigInteger)c == (BigInteger)a * (BigInteger)b);
            return c;
        }
        
        public static UInt128 operator /(UInt128 a, ulong b)
        {
            UInt128 c;
            Divide(out c, ref a, b);
            return c;
        }
        
        public static UInt128 operator /(UInt128 a, UInt128 b)
        {
            UInt128 c;
            Divide(out c, ref a, (ulong)b);
            return c;
        }
        
        public static ulong operator %(UInt128 a, ulong b)
        {
            return Modulus(ref a, b);
        }
        
        public static UInt128 operator %(UInt128 a, UInt128 b)
        {
            return Modulus(ref a, (ulong)b.r1 << 32 | b.r0);
        }

        public static bool operator <(UInt128 a, UInt128 b)
        {
            return a.CompareTo(b) < 0;
        }

        public static bool operator <(UInt128 a, int b)
        {
            return a.CompareTo(b) < 0;
        }

        public static bool operator <(int a, UInt128 b)
        {
            return b.CompareTo(a) > 0;
        }

        public static bool operator <(UInt128 a, uint b)
        {
            return a.CompareTo(b) < 0;
        }

        public static bool operator <(uint a, UInt128 b)
        {
            return b.CompareTo(a) > 0;
        }

        public static bool operator <(UInt128 a, long b)
        {
            return a.CompareTo(b) < 0;
        }

        public static bool operator <(long a, UInt128 b)
        {
            return b.CompareTo(a) > 0;
        }

        public static bool operator <(UInt128 a, ulong b)
        {
            return a.CompareTo(b) < 0;
        }

        public static bool operator <(ulong a, UInt128 b)
        {
            return b.CompareTo(a) > 0;
        }

        public static bool operator <=(UInt128 a, UInt128 b)
        {
            return a.CompareTo(b) <= 0;
        }

        public static bool operator <=(UInt128 a, int b)
        {
            return a.CompareTo(b) <= 0;
        }

        public static bool operator <=(int a, UInt128 b)
        {
            return b.CompareTo(a) >= 0;
        }

        public static bool operator <=(UInt128 a, uint b)
        {
            return a.CompareTo(b) <= 0;
        }

        public static bool operator <=(uint a, UInt128 b)
        {
            return b.CompareTo(a) >= 0;
        }

        public static bool operator <=(UInt128 a, long b)
        {
            return a.CompareTo(b) <= 0;
        }

        public static bool operator <=(long a, UInt128 b)
        {
            return b.CompareTo(a) >= 0;
        }

        public static bool operator <=(UInt128 a, ulong b)
        {
            return a.CompareTo(b) <= 0;
        }

        public static bool operator <=(ulong a, UInt128 b)
        {
            return b.CompareTo(a) >= 0;
        }

        public static bool operator >(UInt128 a, UInt128 b)
        {
            return a.CompareTo(b) > 0;
        }

        public static bool operator >(UInt128 a, int b)
        {
            return a.CompareTo(b) > 0;
        }

        public static bool operator >(int a, UInt128 b)
        {
            return b.CompareTo(a) < 0;
        }

        public static bool operator >(UInt128 a, uint b)
        {
            return a.CompareTo(b) > 0;
        }

        public static bool operator >(uint a, UInt128 b)
        {
            return b.CompareTo(a) < 0;
        }

        public static bool operator >(UInt128 a, long b)
        {
            return a.CompareTo(b) > 0;
        }

        public static bool operator >(long a, UInt128 b)
        {
            return b.CompareTo(a) < 0;
        }

        public static bool operator >(UInt128 a, ulong b)
        {
            return a.CompareTo(b) > 0;
        }

        public static bool operator >(ulong a, UInt128 b)
        {
            return b.CompareTo(a) < 0;
        }

        public static bool operator >=(UInt128 a, UInt128 b)
        {
            return a.CompareTo(b) >= 0;
        }

        public static bool operator >=(UInt128 a, int b)
        {
            return a.CompareTo(b) >= 0;
        }

        public static bool operator >=(int a, UInt128 b)
        {
            return b.CompareTo(a) <= 0;
        }

        public static bool operator >=(UInt128 a, uint b)
        {
            return a.CompareTo(b) >= 0;
        }

        public static bool operator >=(uint a, UInt128 b)
        {
            return b.CompareTo(a) <= 0;
        }

        public static bool operator >=(UInt128 a, long b)
        {
            return a.CompareTo(b) >= 0;
        }

        public static bool operator >=(long a, UInt128 b)
        {
            return b.CompareTo(a) <= 0;
        }

        public static bool operator >=(UInt128 a, ulong b)
        {
            return a.CompareTo(b) >= 0;
        }

        public static bool operator >=(ulong a, UInt128 b)
        {
            return b.CompareTo(a) <= 0;
        }

        public static bool operator ==(UInt128 a, UInt128 b)
        {
            return a.Equals(b);
        }

        public static bool operator ==(UInt128 a, ulong b)
        {
            return a.Equals(b);
        }

        public static bool operator ==(ulong a, UInt128 b)
        {
            return b.Equals(a);
        }

        public static bool operator !=(UInt128 a, UInt128 b)
        {
            return !a.Equals(b);
        }

        public static bool operator !=(UInt128 a, ulong b)
        {
            return !a.Equals(b);
        }

        public static bool operator !=(ulong a, UInt128 b)
        {
            return !b.Equals(a);
        }

        public int CompareTo(UInt128 other)
        {
            if (r3 != other.r3)
                return r3.CompareTo(other.r3);
            if (r2 != other.r2)
                return r2.CompareTo(other.r2);
            if (r1 != other.r1)
                return r1.CompareTo(other.r1);
            return r0.CompareTo(other.r0);
        }

        public int CompareTo(int other)
        {
            if (r3 != 0 || r2 != 0 || other < 0)
                return 1;
            return ((uint)this).CompareTo((uint)other);
        }

        public int CompareTo(uint other)
        {
            if (r3 != 0 || r2 != 0)
                return 1;
            return ((uint)this).CompareTo(other);
        }

        public int CompareTo(long other)
        {
            if (r3 != 0 || r2 != 0 || other < 0)
                return 1;
            return ((ulong)this).CompareTo((ulong)other);
        }

        public int CompareTo(ulong other)
        {
            if (r3 != 0 || r2 != 0)
                return 1;
            return ((ulong)this).CompareTo(other);
        }

        public bool Equals(UInt128 other)
        {
            return r0 == other.r0 && r1 == other.r1 && r2 == other.r2 && r3 == other.r3;
        }

        public bool Equals(ulong other)
        {
            return r0 == (uint)other && r1 == (uint)(other >> 32) && r2 == 0 && r3 == 0;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is UInt128))
                return false;
            return Equals((UInt128)obj);
        }

        public override int GetHashCode()
        {
            return r0.GetHashCode() ^ r1.GetHashCode() ^ r2.GetHashCode() ^ r3.GetHashCode();
        }

        public static ulong MultiplyHigh(ulong a, ulong b)
        {
#if false
            UInt128 c;
            Multiply(out c, (uint)a, (uint)(a >> 32), (uint)b, (uint)(b >> 32));
            return (ulong)c.r3 << 32 | c.r2;
#endif
#if true
            var u0 = (uint)a;
            var u1 = (uint)(a >> 32);
            var v0 = (uint)b;
            var v1 = (uint)(b >> 32);
            var carry = (((ulong)u0 * v0) >> 32) + (ulong)u0 * v1;
            return (((uint)carry + (ulong)u1 * v0) >> 32) + (carry >> 32) + (ulong)u1 * v1;
#endif
        }

        public static ulong MultiplyHighApprox(ulong a, ulong b)
        {
            var u0 = (uint)a;
            var u1 = (uint)(a >> 32);
            var v0 = (uint)b;
            var v1 = (uint)(b >> 32);
            var carry = (ulong)u0 * v1;
            return (((uint)carry + (ulong)u1 * v0) >> 32) + (carry >> 32) + (ulong)u1 * v1;
        }

        public static UInt128 Multiply(ulong a, ulong b)
        {
            UInt128 c;
            Multiply(out c, (uint)a, (uint)(a >> 32), (uint)b, (uint)(b >> 32));
            return c;
        }

        public static UInt128 Square(ulong a)
        {
            UInt128 c;
            Square(out c, (uint)a, (uint)(a >> 32));
            return c;
        }

        public static ulong ModularSum(ulong a, ulong b, ulong modulus)
        {
            var a0 = (uint)a;
            var a1 = (uint)(a >> 32);
            var b0 = (uint)b;
            var b1 = (uint)(b >> 32);
            var n0 = (uint)modulus;
            var n1 = (uint)(modulus >> 32);
            var carry = (ulong)a0 + b0;
            var c0 = (uint)carry;
            carry = (carry >> 32) + a1 + b1;
            var c1 = (uint)carry;
            if (carry > n1 || carry == n1 && c0 >= n0)
            {
                var borrow = (ulong)c0 - n0;
                c0 = (uint)borrow;
                borrow = (ulong)((long)borrow >> 32) + carry - n1;
                c1 = (uint)borrow;
            }
            var c = (ulong)c1 << 32 | c0;
            Debug.Assert(((BigInteger)a + b) % modulus == c);
            return c;
        }
        
        public static ulong ModularDifference(ulong a, ulong b, ulong modulus)
        {
            var a0 = (uint)a;
            var a1 = (uint)(a >> 32);
            var b0 = (uint)b;
            var b1 = (uint)(b >> 32);
            var n0 = (uint)modulus;
            var n1 = (uint)(modulus >> 32);
            var borrow = (ulong)a0 - b0;
            var c0 = (uint)borrow;
            borrow = (ulong)((long)borrow >> 32) + a1 - b1;
            var c1 = (uint)borrow;
            if (borrow >> 32 != 0)
            {
                var carry = (ulong)c0 + n0;
                c0 = (uint)carry;
                carry = (carry >> 32) + borrow + n1;
                c1 = (uint)carry;
            }
            var c = (ulong)c1 << 32 | c0;
            Debug.Assert(((BigInteger)a - b + modulus) % modulus == c);
            return c;
        }
        
        public static ulong ModularProduct(ulong a, ulong b, ulong modulus)
        {
            UInt128 product;
            Multiply(out product, (uint)a, (uint)(a >> 32), (uint)b, (uint)(b >> 32));
            var c = Modulus(ref product, modulus);
            Debug.Assert((BigInteger)a * b % modulus == c);
            return c;
        }
        
        public static ulong ModularPower(ulong value, ulong exponent, ulong modulus)
        {
            var result = (ulong)1;
            while (exponent != 0)
            {
                if ((exponent & 1) != 0)
                    result = ModularProduct(result, value, modulus);
                if (exponent != 1)
                    value = ModularProduct(value, value, modulus);
                exponent >>= 1;
            }
            return result;
        }

        public static ulong Montgomery(ulong u, ulong v, ulong n, uint k0)
        {
            var u0 = (uint)u;
            var u1 = (uint)(u >> 32);
            var v0 = (uint)v;
            var v1 = (uint)(v >> 32);
            var n0 = (uint)n;
            var n1 = (uint)(n >> 32);

            if (n1 == 0)
                return Montgomery(u0, v0, n0, k0);

            var carry = (ulong)u0 * v0;
            var t0 = (uint)carry;
            carry = (carry >> 32) + (ulong)u1 * v0;
            var t1 = (uint)carry;
            var t2 = (uint)(carry >> 32);

            var m = t0 * k0;
            carry = t0 + (ulong)m * n0;
            carry = (carry >> 32) + t1 + (ulong)m * n1;
            t0 = (uint)carry;
            carry = (carry >> 32) + t2;
            t1 = (uint)carry;
            t2 = (uint)(carry >> 32);

            carry = t0 + (ulong)u0 * v1;
            t0 = (uint)carry;
            carry = (carry >> 32) + t1 + (ulong)u1 * v1;
            t1 = (uint)carry;
            carry = (carry >> 32) + t2;
            t2 = (uint)carry;
            var t3 = (uint)(carry >> 32);

            m = t0 * k0;
            carry = t0 + (ulong)m * n0;
            carry = (carry >> 32) + t1 + (ulong)m * n1;
            t0 = (uint)carry;
            carry = (carry >> 32) + t2;
            t1 = (uint)carry;
            t2 = t3 + (uint)(carry >> 32);

            var t = (ulong)t1 << 32 | t0;
            if (t2 != 0 || t >= n)
                t -= n;
            return t;
        }

        public static uint Montgomery(uint u0, uint v0, uint n0, uint k0)
        {
#if false
            var carry = (ulong)u0 * v0;
            var t0 = (uint)carry;
            var t1 = (uint)(carry >> 32);

            var m = (ulong)(t0 * k0);
            carry = t0 + m * n0;
            var t = (carry >> 32) + t1;

            if (t >= n0)
                t -= n0;
            return (uint)t;
#endif
#if false
            var uv = (ulong)u0 * v0;
            var mn = (ulong)((uint)uv * k0) * n0;
            var t = (uv >> 32) + (mn >> 32);
            if ((ulong)(uint)uv + (uint)mn >> 32 != 0)
                ++t;
            if (t >= n0)
                t -= n0;
            return (uint)t;
#endif
#if false
            // Only works if n0 <= int.MaxValue.
            var uv = (ulong)u0 * v0;
            var mn = (ulong)((uint)uv * k0) * n0;
            var t = (uv + mn) >> 32;
            if (t >= n0)
                t -= n0;
            return (uint)t;
#endif
#if true
            var uv = (ulong)u0 * v0;
            var mn = (ulong)(0 - (uint)uv * k0) * n0;
            if (uv < mn)
                return (uint)(n0 - ((mn - uv) >> 32));
            return (uint)((uv - mn) >> 32);
#endif
        }

        private static void Add(out UInt128 w, ref UInt128 u, ref UInt128 v)
        {
            var carry = (ulong)u.r0 + v.r0;
            w.r0 = (uint)carry;
            carry = (carry >> 32) + u.r1 + v.r1;
            w.r1 = (uint)carry;
            carry = (carry >> 32) + u.r2 + v.r2;
            w.r2 = (uint)carry;
            carry = (carry >> 32) + u.r3 + v.r3;
            w.r3 = (uint)carry;
        }

        private static void Subtract(out UInt128 w, ref UInt128 u, ref UInt128 v)
        {
            var borrow = (ulong)u.r0 - v.r0;
            w.r0 = (uint)borrow;
            borrow = (ulong)((long)borrow >> 32) + u.r1 - v.r1;
            w.r1 = (uint)borrow;
            borrow = (ulong)((long)borrow >> 32) + u.r2 - v.r2;
            w.r2 = (uint)borrow;
            borrow = (ulong)((long)borrow >> 32) + u.r3 - v.r3;
            w.r3 = (uint)borrow;
        }

        private static void Square(out UInt128 w, uint u0, uint u1)
        {
            var carry = (ulong)u0 * u0;
            w.r0 = (uint)carry;
            var u0u1 = (ulong)u0 * u1;
            carry = (carry >> 32) + u0u1;
            w.r1 = (uint)carry;
            w.r2 = (uint)(carry >> 32);
            carry = w.r1 + u0u1;
            w.r1 = (uint)carry;
            carry = (carry >> 32) + w.r2 + (ulong)u1 * u1;
            w.r2 = (uint)carry;
            w.r3 = (uint)(carry >> 32);
        }

        private static void Multiply(out UInt128 w, uint u0, uint u1, uint v0, uint v1)
        {
            var carry = (ulong)u0 * v0;
            w.r0 = (uint)carry;
            carry = (carry >> 32) + (ulong)u0 * v1;
            w.r1 = (uint)carry;
            w.r2 = (uint)(carry >> 32);
            carry = w.r1 + (ulong)u1 * v0;
            w.r1 = (uint)carry;
            carry = (carry >> 32) + w.r2 + (ulong)u1 * v1;
            w.r2 = (uint)carry;
            w.r3 = (uint)(carry >> 32);
        }

        private static void Multiply(out UInt128 w, ref UInt128 u, ref UInt128 v)
        {
            var u0 = u.r0;
            var u1 = u.r1;
            var u2 = u.r2;
            var v0 = v.r0;
            var v1 = v.r1;
            var v2 = v.r2;
            var carry = (ulong)u0 * v0;
            w.r0 = (uint)carry;
            carry = (carry >> 32) + (ulong)u0 * v1;
            w.r1 = (uint)carry;
            w.r2 = (uint)(carry >> 32);
            carry = w.r1 + (ulong)u1 * v0;
            w.r1 = (uint)carry;
            carry = (carry >> 32) + w.r2 + (ulong)u1 * v1 +
                (ulong)u2 * v0 + (ulong)u0 * v2 +
                (((ulong)u1 * v2 + (ulong)u2 * v1) << 32);
            w.r2 = (uint)carry;
            w.r3 = (uint)(carry >> 32);
        }

        private static void Divide(out UInt128 w, ref UInt128 u, ulong v)
        {
            var v0 = (uint)v;
            if (v == v0)
            {
                if (u.r3 == 0)
                {
                    if (u.r2 == 0)
                        Division64(out w, ref u, v);
                    else
                        Division96(out w, ref u, v0);
                }
                else
                    Division128(out w, ref u, v0);
            }
            else if (u.r3 == 0)
            {
                if (u.r2 == 0)
                    Division64(out w, ref u, v);
                else
                    Division96(out w, ref u, v);
            }
            else
                Division128(out w, ref u, v);
        }

        private static ulong Modulus(ref UInt128 u, ulong v)
        {
            var v0 = (uint)v;
            if (v == v0)
            {
                if (u.r3 == 0)
                {
                    if (u.r2 == 0)
                        return (ulong)u % v;
                    return Modulus96(ref u, v0);
                }
                return Modulus128(ref u, v0);
            }
            if (u.r3 == 0)
            {
                if (u.r2 == 0)
                    return (ulong)u % v;
                return Modulus96(ref u, v);
            }
            return Modulus128(ref u, v);
        }

        private static void Division64(out UInt128 w, ref UInt128 u, ulong v)
        {
            var q = (ulong)u / v;
            w.r3 = 0;
            w.r2 = 0;
            w.r1 = (uint)(q >> 32);
            w.r0 = (uint)q;
        }

        private static void Division96(out UInt128 w, ref UInt128 u, uint v)
        {
            w.r3 = 0;
            w.r2 = u.r2 / v;
            var u0 = (ulong)(u.r2 - w.r2 * v);
            var u0u1 = u0 << 32 | u.r1;
            w.r1 = (uint)(u0u1 / v);
            u0 = u0u1 - w.r1 * v;
            u0u1 = u0 << 32 | u.r0;
            w.r0 = (uint)(u0u1 / v);
        }
        
        private static void Division128(out UInt128 w, ref UInt128 u, uint v)
        {
            w.r3 = u.r3 / v;
            var u0 = (ulong)(u.r3 - w.r3 * v);
            var u0u1 = u0 << 32 | u.r2;
            w.r2 = (uint)(u0u1 / v);
            u0 = u0u1 - w.r2 * v;
            u0u1 = u0 << 32 | u.r1;
            w.r1 = (uint)(u0u1 / v);
            u0 = u0u1 - w.r1 * v;
            u0u1 = u0 << 32 | u.r0;
            w.r0 = (uint)(u0u1 / v);
        }
        
        private static void Division96(out UInt128 w, ref UInt128 u, ulong v)
        {
            var dneg = ((uint)(v >> 32)).GetBitLength();
            var d = 32 - dneg;
            var vPrime = v << d;
            var v1 = (uint)(vPrime >> 32);
            var v2 = (uint)vPrime;
            var r0 = u.r0;
            var r1 = u.r1;
            var r2 = u.r2;
            var r3 = (uint)0;
            if (d != 0)
            {
                r3 = r2 >> dneg;
                r2 = r2 << d | r1 >> dneg;
                r1 = r1 << d | r0 >> dneg;
                r0 = r0 << d;
            }
            w.r3 = 0;
            w.r2 = 0;
            w.r1 = ModDiv(r3, ref r2, ref r1, v1, v2);
            w.r0 = ModDiv(r2, ref r1, ref r0, v1, v2);
        }
        
        private static void Division128(out UInt128 w, ref UInt128 u, ulong v)
        {
            var dneg = ((uint)(v >> 32)).GetBitLength();
            var d = 32 - dneg;
            var vPrime = v << d;
            var v1 = (uint)(vPrime >> 32);
            var v2 = (uint)vPrime;
            var r0 = u.r0;
            var r1 = u.r1;
            var r2 = u.r2;
            var r3 = u.r3;
            var r4 = (uint)0;
            if (d != 0)
            {
                r4 = r3 >> dneg;
                r3 = r3 << d | r2 >> dneg;
                r2 = r2 << d | r1 >> dneg;
                r1 = r1 << d | r0 >> dneg;
                r0 = r0 << d;
            }
            w.r3 = 0;
            w.r2 = ModDiv(r4, ref r3, ref r2, v1, v2);
            w.r1 = ModDiv(r3, ref r2, ref r1, v1, v2);
            w.r0 = ModDiv(r2, ref r1, ref r0, v1, v2);
        }
        
        private static ulong Modulus96(ref UInt128 u, uint v)
        {
            var u0 = (ulong)(u.r2 % v);
            var u0u1 = u0 << 32 | u.r1;
            u0 = u0u1 % v;
            u0u1 = u0 << 32 | u.r0;
            return u0u1 % v;
        }
        
        private static ulong Modulus128(ref UInt128 u, uint v)
        {
            var u0 = (ulong)(u.r3 % v);
            var u0u1 = u0 << 32 | u.r2;
            u0 = u0u1 % v;
            u0u1 = u0 << 32 | u.r1;
            u0 = u0u1 % v;
            u0u1 = u0 << 32 | u.r0;
            return u0u1 % v;
        }
        
        private static ulong Modulus96(ref UInt128 u, ulong v)
        {
            var dneg = ((uint)(v >> 32)).GetBitLength();
            var d = 32 - dneg;
            var vPrime = v << d;
            var v1 = (uint)(vPrime >> 32);
            var v2 = (uint)vPrime;
            var r0 = u.r0;
            var r1 = u.r1;
            var r2 = u.r2;
            var r3 = (uint)0;
            if (d != 0)
            {
                r3 = r2 >> dneg;
                r2 = r2 << d | r1 >> dneg;
                r1 = r1 << d | r0 >> dneg;
                r0 = r0 << d;
            }
            ModDiv(r3, ref r2, ref r1, v1, v2);
            ModDiv(r2, ref r1, ref r0, v1, v2);
            return ((ulong)r1 << 32 | r0) >> d;
        }
        
        private static ulong Modulus128(ref UInt128 u, ulong v)
        {
            var dneg = ((uint)(v >> 32)).GetBitLength();
            var d = 32 - dneg;
            var vPrime = v << d;
            var v1 = (uint)(vPrime >> 32);
            var v2 = (uint)vPrime;
            var r0 = u.r0;
            var r1 = u.r1;
            var r2 = u.r2;
            var r3 = u.r3;
            var r4 = (uint)0;
            if (d != 0)
            {
                r4 = r3 >> dneg;
                r3 = r3 << d | r2 >> dneg;
                r2 = r2 << d | r1 >> dneg;
                r1 = r1 << d | r0 >> dneg;
                r0 = r0 << d;
            }
            ModDiv(r4, ref r3, ref r2, v1, v2);
            ModDiv(r3, ref r2, ref r1, v1, v2);
            ModDiv(r2, ref r1, ref r0, v1, v2);
            return ((ulong)r1 << 32 | r0) >> d;
        }
        
        private static uint ModDiv(uint u0, ref uint u1, ref uint u2, uint v1, uint v2)
        {
            var u0u1 = (ulong)u0 << 32 | u1;
            var qhat = u0 == v1 ? uint.MaxValue : u0u1 / v1;
            var r = u0u1 - qhat * v1;
            if (r == (uint)r && v2 * qhat > (r << 32 | u2))
            {
                --qhat;
                r += v1;
                if (r == (uint)r && v2 * qhat > (r << 32 | u2))
                {
                    --qhat;
                    r += v1;
                }
            }
            var carry = qhat * v2;
            var borrow = (long)u2 - (uint)carry;
            carry >>= 32;
            u2 = (uint)borrow;
            borrow >>= 32;
            carry += qhat * v1;
            borrow += (long)u1 - (uint)carry;
            carry >>= 32;
            u1 = (uint)borrow;
            borrow >>= 32;
            borrow += (long)u0 - (uint)carry;
            if (borrow != 0)
            {
                --qhat;
                carry = (ulong)u2 + v2;
                u2 = (uint)carry;
                carry >>= 32;
                carry += (ulong)u1 + v1;
                u1 = (uint)carry;
            }
            return (uint)qhat;
        }
        
        private static void LeftShift(out UInt128 w, ref UInt128 u, int d)
        {
            var dneg = 32 - d;
            if (d < 32)
            {
                if (d == 0)
                {
                    w = u;
                    return;
                }
                w.r0 = u.r0 << d;
                w.r1 = u.r1 << d | u.r0 >> dneg;
                w.r2 = u.r2 << d | u.r1 >> dneg;
                w.r3 = u.r3 << d | u.r2 >> dneg;
                return;
            }
            if (d < 64)
            {
                if (d == 32)
                {
                    w.r0 = 0;
                    w.r1 = u.r0;
                    w.r2 = u.r1;
                    w.r3 = u.r2;
                    return;
                }
                w.r0 = 0;
                w.r1 = u.r0 << d;
                w.r2 = u.r1 << d | u.r0 >> dneg;
                w.r3 = u.r2 << d | u.r1 >> dneg;
                return;
            }
            if (d < 96)
            {
                if (d == 64)
                {
                    w.r0 = 0;
                    w.r1 = 0;
                    w.r2 = u.r0;
                    w.r3 = u.r1;
                    return;
                }
                w.r0 = 0;
                w.r1 = 0;
                w.r2 = u.r0 << d;
                w.r3 = u.r1 << d | u.r0 >> dneg;
                return;
            }
            if (d == 96)
            {
                w.r0 = 0;
                w.r1 = 0;
                w.r2 = 0;
                w.r3 = u.r0;
                return;
            }
            w.r0 = 0;
            w.r1 = 0;
            w.r2 = 0;
            w.r3 = u.r0 << d;
        }

        private static void RightShift(out UInt128 w, ref UInt128 u, int d)
        {
            var dneg = 32 - d;
            if (d < 32)
            {
                if (d == 0)
                {
                    w = u;
                    return;
                }
                w.r0 = u.r0 >> d | u.r1 << dneg;
                w.r1 = u.r1 >> d | u.r2 << dneg;
                w.r2 = u.r2 >> d | u.r3 << dneg;
                w.r3 = u.r3 >> d;
                return;
            }
            if (d < 64)
            {
                if (d == 32)
                {
                    w.r0 = u.r1;
                    w.r1 = u.r2;
                    w.r2 = u.r3;
                    w.r3 = 0;
                    return;
                }
                w.r0 = u.r1 >> d | u.r2 << dneg;
                w.r1 = u.r2 >> d | u.r3 << dneg;
                w.r2 = u.r3 >> d;
                w.r3 = 0;
                return;
            }
            if (d == 64)
            {
                w.r0 = u.r2;
                w.r1 = u.r3;
                w.r2 = 0;
                w.r3 = 0;
                return;
            }
            throw new NotImplementedException();
        }

        private static void And(out UInt128 w, ref UInt128 u, ref UInt128 v)
        {
            w.r0 = u.r0 & v.r0;
            w.r1 = u.r1 & v.r1;
            w.r2 = u.r2 & v.r2;
            w.r3 = u.r3 & v.r3;
        }

        private static void Or(out UInt128 w, ref UInt128 u, ref UInt128 v)
        {
            w.r0 = u.r0 | v.r0;
            w.r1 = u.r1 | v.r1;
            w.r2 = u.r2 | v.r2;
            w.r3 = u.r3 | v.r3;
        }

        private static void ExclusiveOr(out UInt128 w, ref UInt128 u, ref UInt128 v)
        {
            w.r0 = u.r0 ^ v.r0;
            w.r1 = u.r1 ^ v.r1;
            w.r2 = u.r2 ^ v.r2;
            w.r3 = u.r3 ^ v.r3;
        }

        private static void Not(out UInt128 w, ref UInt128 u)
        {
            w.r0 = ~u.r0;
            w.r1 = ~u.r1;
            w.r2 = ~u.r2;
            w.r3 = ~u.r3;
        }
    }
}
