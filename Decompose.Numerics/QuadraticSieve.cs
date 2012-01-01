﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Decompose.Numerics
{
    public class QuadraticSieve : IFactorizationAlgorithm<BigInteger>
    {
        private const int windowSize = 2000;

        private class Candidate
        {
            public BigInteger X { get; set; }
            public int[] Exponents { get; set; }
        }

        private struct Range
        {
            public BigInteger Min { get; set; }
            public BigInteger Max { get; set; }
            public override string ToString()
            {
                return string.Format("[{0}, {1})", Min, Max);
            }
        }

        protected int threads;

        public QuadraticSieve(int threads)
        {
            this.threads = threads;
        }

        public IEnumerable<BigInteger> Factor(BigInteger n)
        {
            var factors = new List<BigInteger>();
            FactorCore(n, factors);
            return factors;
        }

        private void FactorCore(BigInteger n, List<BigInteger> factors)
        {
            if (n == 1)
                return;
            if (BigIntegerUtils.IsPrime(n))
            {
                factors.Add(n);
                return;
            }
            var divisor = GetDivisor(n);
            if (!divisor.IsZero)
            {
                FactorCore(divisor, factors);
                FactorCore(n / divisor, factors);
            }
        }

        private BigInteger n;
        private BigInteger sqrtn;
        private int[] factorBase;

        private BigInteger GetDivisor(BigInteger n)
        {
            if (n.IsEven)
                return BigIntegerUtils.Two;
            this.n = n;
            sqrtn = BigIntegerUtils.Sqrt(n);
            var digits = BigInteger.Log(n) / Math.Log(10);
            int factorBaseSize = (int)Math.Ceiling((digits - 5) * 5 + digits) + 1;
            factorBase = new SieveOfErostothones()
                .Where(p => BigIntegerUtils.JacobiSymbol(n, p) == 1)
                .Take(factorBaseSize)
                .ToArray();
            int desired = factorBase.Length + 1 + (int)Math.Ceiling(digits);
            var candidates = Sieve(desired);
            var matrix = new List<BitArray>();
            for (int i = 0; i <= factorBaseSize; i++)
                matrix.Add(new BitArray(candidates.Count));
            for (int j = 0; j < candidates.Count; j++)
            {
                var exponents = candidates[j].Exponents;
                for (int i = 0; i < exponents.Length; i++)
                    matrix[i][j] = exponents[i] % 2 != 0;
            }
            foreach (var v in Solve(matrix))
            {
#if false
                Console.WriteLine("v = {0}", string.Join(" ", v.ToArray()));
#endif
                var vbool = v.Cast<bool>();
                var xSet = candidates
                    .Zip(vbool, (candidate, selected) => new { X = candidate.X, Selected = selected })
                    .Where(pair => pair.Selected)
                    .Select(pair => pair.X)
                    .ToArray();
                var xPrime = xSet.Aggregate((sofar, current) => sofar * current) % n;
                var yPrime = BigIntegerUtils.Sqrt(xSet
                    .Aggregate(BigInteger.One, (sofar, current) => sofar * (current * current - n))) % n;
                var factor = BigInteger.GreatestCommonDivisor(xPrime + yPrime, n);
                if (!factor.IsOne && factor != n)
                    return factor;
            }
            return BigInteger.Zero;
        }

        private List<Candidate> Sieve(int desired)
        {
            var candidates = new List<Candidate>();
            if (threads == 1)
            {
                foreach (var range in Ranges)
                {
                    var left = desired - candidates.Count;
                    candidates.AddRange(SieveTrialDivision(range.Min, range.Max).Take(left));
                    if (candidates.Count == desired)
                        break;
                }
            }
            else
            {
                var collection = new BlockingCollection<Candidate>();
                var cancellationTokenSource = new CancellationTokenSource();
                var tasks = new Task[threads + 1];
                var ranges = Ranges.GetEnumerator();
                for (int i = 0; i < threads; i++)
                {
                    ranges.MoveNext();
                    var range = ranges.Current;
                    tasks[i] = Task.Factory.StartNew(() => SieveParallel(range, collection, cancellationTokenSource.Token));
                }
                tasks[threads] = Task.Factory.StartNew(() => ReadQueue(candidates, collection, desired));
                while (true)
                {
                    var index = Task.WaitAny(tasks);
                    if (index == threads)
                    {
                        cancellationTokenSource.Cancel();
                        break;
                    }
                    ranges.MoveNext();
                    var range = ranges.Current;
                    tasks[index] = Task.Factory.StartNew(() => SieveParallel(range, collection, cancellationTokenSource.Token));
                }
            }
            return candidates;
        }

        private void ReadQueue(List<Candidate> list, BlockingCollection<Candidate> queue, int desired)
        {
            while (list.Count < desired)
            {
                var candidate = null as Candidate;
                queue.TryTake(out candidate, Timeout.Infinite);
                list.Add(candidate);
            }
        }

        private void SieveParallel(Range range, BlockingCollection<Candidate> candidates, CancellationToken cancellationToken)
        {
            foreach (var candidate in SieveTrialDivision(range.Min, range.Max))
            {
                candidates.Add(candidate);
                if (cancellationToken.IsCancellationRequested)
                    return;
            }
        }

        private IEnumerable<Range> Ranges
        {
            get
            {
                var k = BigInteger.Zero;
                var window = BigIntegerUtils.Min(sqrtn, windowSize);
                while (true)
                {
                    yield return new Range { Min = k, Max = k + window };
                    yield return new Range { Min = -k - window, Max = -k };
                    k += window;
                }
            }
        }

        private IEnumerable<Candidate> SieveTrialDivision(BigInteger kmin, BigInteger kmax)
        {
            int factorBaseSize = factorBase.Length;
            var exponents = new int[factorBaseSize + 1];
            for (var k = kmin; k < kmax; k++)
            {
                for (int i = 0; i <= factorBaseSize; i++)
                    exponents[i] = 0;
                var x = sqrtn + k;
                var y = x * x - n;
                if (y < 0)
                {
                    exponents[0] = 1;
                    y = -y;
                }
                for (int i = 0; i < factorBaseSize; i++)
                {
                    var p = factorBase[i];
                    while ((y % p).IsZero)
                    {
                        ++exponents[i + 1];
                        y /= p;
                    }
                }
                if (y.IsOne)
                {
                    yield return new Candidate
                    {
                        X = x,
                        Exponents = (int[])exponents.Clone(),
                    };
                }
            }
        }

        private IEnumerable<BitArray> Solve(List<BitArray> matrix)
        {
#if false
            PrintMatrix("initial:", matrix);
#endif
            int rows = Math.Min(matrix.Count, matrix[0].Count);
            int cols = matrix[0].Count;
            var c = new List<int>();
            for (int i = 0; i < cols; i++)
                c.Add(-1);
            for (int k = 0; k < cols; k++)
            {
                int j = -1;
                for (int i = 0; i < rows; i++)
                {
                    if (matrix[i][k] && c[i] < 0)
                    {
                        j = i;
                        break;
                    }
                }
                if (j != -1)
                {
                    for (int i = 0; i < rows; i++)
                    {
                        if (i == j || !matrix[i][k])
                            continue;
                        matrix[i].Xor(matrix[j]);
                    }
                    c[j] = k;
                }
                else
                {
                    var v = new BitArray(c.Count);
                    for (int jj = 0; jj < c.Count; jj++)
                    {
                        int js = -1;
                        for (int s = 0; s < c.Count; s++)
                        {
                            if (c[s] == jj)
                            {
                                js = s;
                                break;
                            }
                        }
                        if (js != -1)
                            v[jj] = matrix[js][k];
                        else if (jj == k)
                            v[jj] = true;
                        else
                            v[jj] = false;
                    }
                    if (VerifySolution(matrix, v))
                        yield return v;
                }
#if false
                PrintMatrix(string.Format("k = {0}", k), matrix);
#endif
            }
        }

        private bool VerifySolution(List<BitArray> matrix, BitArray solution)
        {
            int rows = matrix.Count;
            int cols = matrix[0].Count;
            for (int i = 0; i < rows; i++)
            {
                bool row = false;
                for (int j = 0; j < cols; j++)
                {
                    row ^= solution[j] & matrix[i][j];
                }
                if (row)
                {
                    return false;
                }
            }
            return true;
        }

        private void PrintMatrix(string label, List<List<int>> matrix)
        {
            Console.WriteLine(label);
            for (int i = 0; i < matrix.Count; i++)
                Console.WriteLine(string.Join(" ", matrix[i].ToArray()));
        }
    }
}
