﻿using System;
using System.Diagnostics;
using System.Numerics;

namespace Decompose.Numerics
{
    public class MontgomeryReduction : IReductionAlgorithm
    {
        private class Reducer : IReducer
        {
            private class Residue : IResidue
            {
                private Reducer reducer;
                private Radix32Integer r;

                public Radix32Integer Rep { get { return r; } }

                public bool IsZero { get { return r.CompareTo(reducer.zeroRep) == 0; } }

                public bool IsOne { get { return r.CompareTo(reducer.oneRep) == 0; } }

                protected Residue(Reducer reducer)
                {
                    this.reducer = reducer;
                }

                public Residue(Reducer reducer, BigInteger x)
                    : this(reducer)
                {
                    r = reducer.CreateRep();
                    reducer.reg3.Set(x);
                    r.SetProduct(reducer.reg3, reducer.rSquaredModNRep);
                    reducer.Reduce(r);
                }

                public IResidue Set(IResidue x)
                {
                    r.Set(((Residue)x).r);
                    return this;
                }

                public IResidue Copy()
                {
                    var residue = new Residue(reducer);
                    residue.r = reducer.CreateRep();
                    residue.r.Set(r);
                    return residue;
                }

                public IResidue Multiply(IResidue x)
                {
                    reducer.reg3.Set(r);
                    if (this == x)
                        r.SetSquare(reducer.reg3);
                    else
                        r.SetProduct(reducer.reg3, ((Residue)x).r);
                    reducer.Reduce(r);
                    return this;
                }

                public IResidue Add(IResidue x)
                {
                    r.Add(((Residue)x).r);
                    if (r.CompareTo(reducer.nRep) >= 0)
                        r.Subtract(reducer.nRep);
                    return this;
                }

                public IResidue Subtract(IResidue x)
                {
                    r.Subtract(((Residue)x).r);
                    return this;
                }

                public bool Equals(IResidue other)
                {
                    return r.CompareTo(((Residue)other).r) == 0;
                }

                public int CompareTo(IResidue other)
                {
                    return r.CompareTo(((Residue)other).r);
                }

                public BigInteger ToBigInteger()
                {
                    reducer.reg3.Set(r);
                    reducer.Reduce(reducer.reg3);
                    return reducer.reg3.ToBigInteger();
                }

                public override string ToString()
                {
                    return ToBigInteger().ToString();
                }
            }

            private BigInteger n;
            private int rLength;
            private int length;
            private BigInteger k;
            private uint k0;
            private Radix32Store store;

            private Radix32Integer nRep;
            private Radix32Integer rSquaredModNRep;
            private Radix32Integer kRep;
            private Radix32Integer reg1;
            private Radix32Integer reg2;
            private Radix32Integer reg3;
            private Radix32Integer zeroRep;
            private Radix32Integer oneRep;

            public BigInteger Modulus
            {
                get { return n; }
            }

            public Reducer(BigInteger n)
            {
                this.n = n;
                if (n.IsEven)
                    throw new InvalidOperationException("not relatively prime");
                rLength = (n.GetBitLength() + 31) / 32 * 32;
                length = 2 * rLength / 32 + 1;
                var r = BigInteger.One << rLength;
                var rSquaredModN = r * r % n;
                BigInteger c;
                BigInteger d;
                BigIntegerUtils.ExtendedGreatestCommonDivisor(r, n, out c, out d);
                var rInverse = c;
                k = -d;
                if (rInverse.Sign == -1)
                    rInverse += n;
                if (k.Sign == -1)
                    k += r;
                Debug.Assert(r * rInverse == k * n + 1);
                var w = BigInteger.One << 32;
                k0 = (uint)(k % w);

                store = new Radix32Store(length);
                nRep = store.Create();
                rSquaredModNRep = store.Create();
                kRep = store.Create();
                reg1 = store.Create();
                reg2 = store.Create();
                reg3 = store.Create();
                zeroRep = store.Create();
                oneRep = store.Create();

                nRep.Set(n);
                rSquaredModNRep.Set(rSquaredModN);
                kRep.Set(k);
                zeroRep.Set(new Residue(this, BigInteger.Zero).Rep);
                oneRep.Set(new Residue(this, BigInteger.One).Rep);
            }

            public IResidue ToResidue(BigInteger x)
            {
                return new Residue(this, x);
            }

            private Radix32Integer CreateRep()
            {
                return new Radix32Integer(length);
            }

            private void Reduce(Radix32Integer t)
            {
                t.MontgomeryOperation(nRep, k0);
                if (t.CompareTo(nRep) >= 0)
                    t.Subtract(nRep);
                Debug.Assert(t.CompareTo(nRep) < 0);
            }

            private void BasicReduce(Radix32Integer t)
            {
                reg1.SetMasked(t, rLength);
                reg2.SetProductMasked(reg1, kRep, rLength);
                reg1.SetProduct(reg2, nRep);
                t.Add(reg1);
                t.RightShift(rLength);
                if (t.CompareTo(nRep) >= 0)
                    t.Subtract(nRep);
                Debug.Assert(t.CompareTo(nRep) < 0);
            }
        }

        public IReducer GetReducer(BigInteger n)
        {
            return new Reducer(n);
        }
    }
}
