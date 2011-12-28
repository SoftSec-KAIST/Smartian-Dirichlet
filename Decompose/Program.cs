﻿using System;
using System.Linq;
using System.Numerics;
using Decompose.Numerics;
using System.Diagnostics;

namespace Decompose
{
    class Program
    {
        static void Main(string[] args)
        {
            //FindPrimeTest1();
            //BarrettReductionTest1();
            //BarrettReductionTest2();
            //Radix32Test1();
            //FactorTest1();
            FactorTest2();
        }

        static void FindPrimeTest1()
        {
            var random = new MersenneTwister32(0);
            var limit = BigInteger.One << (32 * 4);
            var x = random.Next(limit);
            while (!BigIntegerUtils.IsPrime(x))
                ++x;
            Console.WriteLine("x = {0}", x);
        }

        static void BarrettReductionTest1()
        {
            var p = BigInteger.Parse("10023859281455311421");
            var random = new MersenneTwister32(0);
            var x = random.Next(p);
            var y = random.Next(p);
            var z = x * y;
            var expected = z % p;
            var bLength = 32;
            var b = BigInteger.One << bLength;
            var k = (p.GetBitLength() - 1) / bLength + 1;
            var mu = BigInteger.Pow(b, 2 * k) / p;
            var bToTheKPlusOne = BigInteger.Pow(b, k + 1);

            var qhat = (z >> (bLength * (k - 1))) * mu >> (bLength * (k + 1));
            var r = z % bToTheKPlusOne - qhat * p % bToTheKPlusOne;
            if (r.Sign == -1)
                r += bToTheKPlusOne;
            while (r >= p)
                r -= p;
            if (r != expected)
                throw new InvalidOperationException();

            var reducer = new BarrettReduction().GetReducer(p);
            var actual = reducer.ToResidue(z).ToBigInteger();
            if (actual != expected)
                throw new InvalidOperationException();
        }

        static void BarrettReductionTest2()
        {
            var n = BigInteger.Parse("10023859281455311421");
            var random1 = new MersenneTwister32(0);
            var random2 = new MersenneTwister32(0);
            var timer1 = new Stopwatch();
            var timer2 = new Stopwatch();
            var iterations1 = 1000;
            var iterations2 = 1000;
            var reducer = new BarrettReduction().GetReducer(n);

            timer1.Start();
            for (int i = 0; i < iterations1; i++)
            {
                var a = reducer.ToResidue(random1.Next(n));
                var b = reducer.ToResidue(random1.Next(n));
                var c = reducer.ToResidue(0);

                for (int j = 0; j < iterations2; j++)
                    c.Set(a).Multiply(b);
            }
            var elapsed1 = timer1.ElapsedMilliseconds;

            timer2.Start();
            for (int i = 0; i < iterations1; i++)
            {
                var a = random2.Next(n);
                var b = random2.Next(n);
                var c = BigInteger.Zero;

                for (int j = 0; j < iterations2; j++)
                {
                    c = a * b;
                    c %= n;
                }
            }
            var elapsed2 = timer1.ElapsedMilliseconds;

            Console.WriteLine("elapsed1 = {0}, elapsed2 = {1}", elapsed1, elapsed2);
        }

        static void Radix32Test1()
        {
            for (int i = 0; i < 2; i++)
            {
                Radix32Test1("sum:     ", (c, a, b) => c.SetSum(a, b), (a, b) => a + b);
                Radix32Test1("product: ", (c, a, b) => c.SetProduct(a, b), (a, b) => a * b);
            }
        }

        static void Radix32Test1(string label,
            Action<Radix32Integer, Radix32Integer, Radix32Integer> operation1,
            Func<BigInteger, BigInteger, BigInteger> operation2)
        {
            var n = BigInteger.Parse("10023859281455311421");
            var length = (n.GetBitLength() * 2 + 31) / 32;
            var random1 = new MersenneTwister32(0);
            var random2 = new MersenneTwister32(0);
            var timer1 = new Stopwatch();
            var timer2 = new Stopwatch();
            var iterations1 = 1000;
            var iterations2 = 1000;

            timer1.Start();
            for (int i = 0; i < iterations1; i++)
            {
                var store = new Radix32Store(length);
                var a = store.Create();
                var b = store.Create();
                var c = store.Create();
                a.Set(random1.Next(n));
                b.Set(random1.Next(n));

                for (int j = 0; j < iterations2; j++)
                    operation1(c, a, b);
            }
            var elapsed1 = timer1.ElapsedMilliseconds;

            timer2.Start();
            for (int i = 0; i < iterations1; i++)
            {
                var a = random2.Next(n);
                var b = random2.Next(n);
                var c = BigInteger.Zero;

                for (int j = 0; j < iterations2; j++)
                    c = operation2(a, b);
            }
            var elapsed2 = timer1.ElapsedMilliseconds;

            Console.WriteLine("{0}: elapsed1 = {1}, elapsed2 = {2}", label, elapsed1, elapsed2);
        }

        static void FactorTest1()
        {
            var n = BigInteger.Parse("10023859281455311421");

            //FactorTest(25, n, new PollardRhoBrent(4));
            FactorTest(25, n, new PollardRhoReduction(4, new BigIntegerReduction()));
            FactorTest(25, n, new PollardRhoReduction(4, new Radix32IntegerReduction()));
            //FactorTest(25, n, new PollardRhoReduction(4, new BarrettReduction()));
            //FactorTest(25, n, new PollardRhoReduction(4, new MontgomeryReduction()));

            //FactorTest(10, n, new PollardRhoBrent(1));
            //FactorTest(10, n, new PollardRhoReduction(1, new BarrettReduction()));
            //FactorTest(10, n, new PollardRhoReduction(1, new MontgomeryReduction()));

            //FactorTest(500, n, new PollardRhoReduction(4, new MontgomeryReduction()));
        }

        static void FactorTest2()
        {
            var p = BigInteger.Parse("287288745765902964785862069919080712937");
            var q = BigInteger.Parse("7660450463");
            var n = p * q;
            //FactorTest(25, n, new PollardRhoBrent(4));
            //FactorTest(25, n, new PollardRhoReduction(4, new Radix32IntegerReduction()));
            //FactorTest(25, n, new PollardRhoReduction(4, new BarrettReduction()));
            FactorTest(25, n, new PollardRhoReduction(1, new MontgomeryReduction()));
        }

        static void FactorTest(int iterations, BigInteger n, IFactorizationAlgorithm<BigInteger> algorithm)
        {
            var elapsed = new double[iterations];
            for (int i = 0; i < iterations; i++)
            {
                GC.Collect();
                var timer = new Stopwatch();
                timer.Start();
                var factors = algorithm.Factor(n).OrderBy(factor => factor).ToArray();
                var product = factors.Aggregate((sofar, current) => sofar * current);
                if (factors.Length != 2)
                    throw new InvalidOperationException();
                if (n != product)
                    throw new InvalidOperationException();
                elapsed[i] = timer.ElapsedMilliseconds;
                Console.WriteLine("elapsed = {0}", elapsed[i]);
            }
            var total = elapsed.Aggregate((sofar, current) => sofar + current);
            Console.WriteLine("{0} iterations in {1} msec, {2} msec/iteration", iterations, total, total / iterations);
        }
    }
}
