﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Decompose.Numerics
{
    public class QuadraticSieve : IFactorizationAlgorithm<BigInteger>
    {
        private class Candidate
        {
            public BigInteger X { get; set; }
            public int[] Exponents { get; set; }
            public override string ToString()
            {
                return string.Format("X = {0}", X);
            }
        }

        private class Interval
        {
            public BigInteger X { get; set; }
            public int Size { get; set; }
            public int[] Exponents { get; set; }
            public ushort[] Counts { get; set; }
            public int[] Offsets { get; set; }
            public Word32Integer XRep { get; set; }
            public Word32Integer Reg1 { get; set; }
            public Word32Integer Reg2 { get; set; }
            public Word32Integer Reg3 { get; set; }
            public override string ToString()
            {
                return string.Format("X = {0}, Size = {1}", X, Size);
            }
        }

        private const int intervalSize = 200000;
        private const int lowerBoundPercentDefault = 85;
        private const int surplusCandidates = 10;
        private readonly BigInteger smallFactorCutoff = (BigInteger)int.MaxValue;

        private int threadsOverride;
        private int factorBaseSizeOverride;
        private int lowerBoundPercentOverride;
        private IFactorizationAlgorithm<int> smallFactorizationAlgorithm;
        private IEnumerable<int> primes;
        private INullSpaceAlgorithm<IBitArray, IBitMatrix> nullSpaceAlgorithm;

        private Tuple<int, int>[] sizePairs =
        {
            Tuple.Create(1, 2),
            Tuple.Create(6, 5),
            Tuple.Create(10, 30),
            Tuple.Create(20, 60),
            Tuple.Create(30, 500),
            Tuple.Create(40, 1200),
            Tuple.Create(50, 5000),
            Tuple.Create(60, 12000),
            Tuple.Create(90, 60000), // http://www.mersenneforum.org/showthread.php?t=4013
        };

        public QuadraticSieve(int threads, int factorBaseSize, int lowerBoundPercent)
        {
            threadsOverride = threads;
            factorBaseSizeOverride = factorBaseSize;
            lowerBoundPercentOverride = lowerBoundPercent;
            smallFactorizationAlgorithm = new TrialDivision();
            primes = new SieveOfErostothones();
            nullSpaceAlgorithm = new GaussianElimination<Word32BitArray>(threads);
        }

        public IEnumerable<BigInteger> Factor(BigInteger n)
        {
            if (n <= smallFactorCutoff)
                return smallFactorizationAlgorithm.Factor((int)n).Select(factor => (BigInteger)factor);
            var factors = new List<BigInteger>();
            FactorCore(n, factors);
            return factors;
        }

        private void FactorCore(BigInteger n, List<BigInteger> factors)
        {
            if (n == 1)
                return;
            if (IntegerMath.IsPrime(n))
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
        private BigInteger sqrtN;
        private int factorBaseSize;
        private int[] factorBase;
        private ushort[] logFactorBase;
        private Tuple<int, int>[] roots;
        private Word32Integer nRep;

        private int threads;
        private CancellationToken token;
        private BlockingCollection<Candidate> candidateBuffer;
        private BlockingCollection<Interval> newIntervalBuffer;
        private BlockingCollection<Interval> oldIntervalBuffer;
        private List<Candidate> candidates;
        private IBitMatrix matrix;
        private int matrixColumn;

        private void CalculateNumberOfThreads()
        {
            threads = threadsOverride != 0 ? threadsOverride : 1;
            if (n <= BigInteger.Pow(10, 10))
                threads = 1;
        }

        private int CalculateFactorBaseSize(BigInteger n)
        {
            if (factorBaseSizeOverride != 0)
                return factorBaseSizeOverride;
            int digits = (int)Math.Ceiling(BigInteger.Log(n) / Math.Log(10));
            for (int i = 0; i < sizePairs.Length - 1; i++)
            {
                var pair = sizePairs[i];
                if (digits >= sizePairs[i].Item1 && digits <= sizePairs[i + 1].Item1)
                {
                    // Interpolate.
                    double x0 = sizePairs[i].Item1;
                    double x1 = sizePairs[i + 1].Item1;
                    double y0 = sizePairs[i].Item2;
                    double y1 = sizePairs[i + 1].Item2;
                    double x = y0 + (digits - x0) * (y1 - y0) / (x1 - x0);
                    return (int)Math.Ceiling(x);
                }
            }
            return (digits - 5) * 5 + digits + 1;
        }

        private ushort CalculateLowerBound(BigInteger y)
        {
            int percent = lowerBoundPercentOverride != 0 ? lowerBoundPercentOverride : lowerBoundPercentDefault;
            return (ushort)(LogScale(y) * percent / 100);
        }

        private BigInteger GetDivisor(BigInteger n)
        {
            if (n.IsEven)
                return BigIntegers.Two;
            this.n = n;
            nRep = new Word32Integer(n.GetBitLength() / Word32Integer.WordLength * 2 + 1);
            nRep.Set(n);
            sqrtN = IntegerMath.Sqrt(n);
            factorBaseSize = CalculateFactorBaseSize(n);
            factorBase = primes
                .Where(p => IntegerMath.JacobiSymbol(n, p) == 1)
                .Take(factorBaseSize)
                .ToArray();
            logFactorBase = factorBase
                .Select(factor => LogScale(factor))
                .ToArray();
            roots = factorBase
                .Select(factor =>
                {
                    var root = (int)IntegerMath.ModularSquareRoot(n, factor);
                    return Tuple.Create(root, factor - root);
                })
                .ToArray();
            int desired = factorBase.Length + surplusCandidates;
#if false
            Sieve(desired, SieveTrialDivision);
#else
            Sieve(desired, SieveQuadraticResidue);
#endif
            foreach (var v in nullSpaceAlgorithm.Solve(matrix))
            {
                var factor = ComputeFactor(candidates, v);
                if (!factor.IsZero)
                    return factor;
            }
            return BigInteger.Zero;
        }

        private BigInteger ComputeFactor(List<Candidate> candidates, IBitArray v)
        {
            var indices = v
                .Select((selected, index) => new { Index = index, Selected = selected })
                .Where(pair => pair.Selected)
                .Select(pair => pair.Index)
                .ToArray();
#if false
            Console.WriteLine("v = {0}", string.Join(", ", indices));
#endif
            var xSet = indices.Select(index => candidates[index].X).ToArray();
            var xPrime = xSet
                .Aggregate((sofar, current) => sofar * current) % n;
            var yPrimeSquared = xSet
                .Aggregate(BigInteger.One, (sofar, current) => sofar * (current * current - n));
            var yPrime = IntegerMath.Sqrt(yPrimeSquared) % n;
            var factor = BigInteger.GreatestCommonDivisor(xPrime + yPrime, n);
            if (!factor.IsOne && factor != n)
                return factor;
            return BigInteger.Zero;
        }

        private static ushort LogScale(BigInteger n)
        {
            return (ushort)Math.Ceiling(10 * BigInteger.Log(BigInteger.Abs(n)));
        }

        private void Sieve(int desired, Action<Interval, Action<Candidate>> sieveCore)
        {
            CalculateNumberOfThreads();
            candidateBuffer = new BlockingCollection<Candidate>();
            newIntervalBuffer = new BlockingCollection<Interval>();
            oldIntervalBuffer = new BlockingCollection<Interval>();
            candidates = new List<Candidate>();
            matrix = CreateMatrix(desired);
            SetupIntervals();

            if (threads == 1)
            {
                token = CancellationToken.None;
                while (candidates.Count < desired)
                {
                    var left = desired - candidates.Count;
                    var interval = newIntervalBuffer.Take();
                    sieveCore(interval, candidate => candidates.Add(candidate));
                    oldIntervalBuffer.Add(interval);
                }
            }
            else
            {
                var tokenSource = new CancellationTokenSource();
                for (int i = 0; i <= threads; i++)
                    oldIntervalBuffer.Add(new Interval());
                token = tokenSource.Token;
                Task.Factory.StartNew(ProduceIntervals);
                for (int i = 0; i < threads; i++)
                    Task.Factory.StartNew(() => SieveParallel(sieveCore));
                while (candidates.Count < desired)
                {
                    var candidate = candidateBuffer.Take();
                    candidates.Add(candidate);
                }
                tokenSource.Cancel();
            }
            ProcessCandidates();
        }

        private void ProduceIntervals()
        {
            while (!token.IsCancellationRequested)
            {
                var interval = oldIntervalBuffer.Take();
                newIntervalBuffer.Add(GetInterval(interval));
            }
        }

        private IBitMatrix CreateMatrix(int columns)
        {
            var matrix = new Word64BitMatrix(factorBaseSize + 1, columns);
            matrixColumn = 0;
            return matrix;
        }

        private void ProcessCandidates()
        {
            Debug.Assert(candidates.GroupBy(candidate => candidate.X).Count() == candidates.Count);
#if false
            for (int i = 0; i < candidates.Count; i++)
                ProcessCandidate(candidates[i]);
#else
            int rows = matrix.Rows;
            int cols = matrix.Cols;
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                    matrix[i, j] = candidates[j].Exponents[i] % 2 != 0;
            }
#endif
        }

        private void ProcessCandidate(Candidate candidate)
        {
            // Doing this in parallel with sieving interferes with the cache.
            int j = matrixColumn++;
            var exponents = candidate.Exponents;
            for (int i = 0; i < exponents.Length; i++)
                matrix[i, j] = exponents[i] % 2 != 0;
        }

        private void SieveParallel(Action<Interval, Action<Candidate>> sieveCore)
        {
            var callback = new Action<Candidate>(ParallelCallback);
            while (!token.IsCancellationRequested)
            {
                var interval = newIntervalBuffer.Take();
                sieveCore(interval, callback);
                oldIntervalBuffer.Add(interval);
            }
        }

        private void ParallelCallback(Candidate candidate)
        {
            candidateBuffer.Add(candidate);
        }

        private object intervalSync = new object();
        private BigInteger xPos;
        private BigInteger xNeg;
        private int[] offsetsPos;
        private int[] offsetsNeg;
        private int nextInterval;
        private int size;

        private void SetupIntervals()
        {
            xPos = sqrtN;
            offsetsPos = new int[factorBaseSize];
            for (int i = 0; i < factorBaseSize; i++)
            {
                var p = factorBase[i];
                var offset = ((int)((roots[i].Item1 - xPos) % p) + p) % p;
                offsetsPos[i] = offset;
            }
            nextInterval = -1;
            size = (int)IntegerMath.Min((sqrtN + threads - 1) / threads, intervalSize);
            xNeg = xPos - size;
            offsetsNeg = (int[])offsetsPos.Clone();
            ShiftOffsets(offsetsNeg, -size);
        }

        private Interval GetInterval(Interval interval)
        {
            if (nextInterval == 1)
            {
                SetupInterval(interval, xPos, size, offsetsPos);
                ShiftOffsets(offsetsPos, size);
                xPos += size;
                nextInterval = -nextInterval;
                return interval;
            }
            else
            {
                SetupInterval(interval, xNeg, size, offsetsNeg);
                ShiftOffsets(offsetsNeg, -size);
                xNeg -= size;
                nextInterval = -nextInterval;
                return interval;
            }
        }

        private Interval SetupInterval(Interval interval, BigInteger x, int size, int[] offsets)
        {
            interval.X = x;
            interval.Size = size;
            if (interval.Offsets == null)
                interval.Offsets = new int[factorBaseSize];
            offsets.CopyTo(interval.Offsets, 0);
            if (interval.XRep == null)
            {
                interval.XRep = nRep.Copy();
                interval.Reg1 = nRep.Copy();
                interval.Reg2 = nRep.Copy();
                interval.Reg3 = nRep.Copy();
            }
            interval.XRep.Clear();
            return interval;
        }

        private void ShiftOffsets(int[] offsets, int window)
        {
            for (int i = 0; i < factorBaseSize; i++)
            {
                var p = factorBase[i];
                offsets[i] = ((offsets[i] - window) % p + p) % p;
            }
        }

        private void SieveTrialDivision(Interval interval, Action<Candidate> candidateCallback)
        {
            if (interval.Exponents == null)
                interval.Exponents = new int[factorBaseSize + 1];
            var exponents = interval.Exponents;
            for (int i = 0; i < interval.Size; i++)
            {
                if (ValueIsSmooth(i, interval))
                {
                    var candidate = new Candidate
                    {
                        X = interval.X + i,
                        Exponents = (int[])exponents.Clone(),
                    };
                    candidateCallback(candidate);
                    if (token.IsCancellationRequested)
                        break;
                }
            }
        }

        private void SieveQuadraticResidue(Interval interval, Action<Candidate> candidateCallback)
        {
            var x0 = interval.X;
            var y0 = x0 * x0 - n;
            int size = interval.Size;
            var offsets = interval.Offsets;
            if (interval.Exponents == null)
                interval.Exponents = new int[factorBaseSize + 1];
            var exponents = interval.Exponents;
            if (interval.Counts == null)
                interval.Counts = new ushort[size];
            var counts = interval.Counts;
            for (int i = 0; i < factorBaseSize; i++)
            {
                var p = factorBase[i];
                var logP = logFactorBase[i];
                var k0 = offsets[i];
                for (int root = 0; root < 2; root++)
                {
                    if (root == 1 && p == 2)
                        continue;
                    for (int k = k0; k < size; k += p)
                    {
                        Debug.Assert((BigInteger.Pow(x0 + k, 2) - n) % p == 0);
                        counts[k] += logP;
                    }
                    k0 += roots[i].Item2 - roots[i].Item1;
                }
                if (token.IsCancellationRequested)
                    return;
            }
            if (token.IsCancellationRequested)
                return;
            ushort limit = CalculateLowerBound(y0);
            for (int k = 0; k < size; k++)
            {
                if (counts[k] >= limit)
                {
                    var x = x0 + k;
                    if (ValueIsSmooth(k, interval))
                    {
                        var candidate = new Candidate
                        {
                            X = x,
                            Exponents = (int[])exponents.Clone(),
                        };
                        candidateCallback(candidate);
                        if (token.IsCancellationRequested)
                            return;
                    }
                }
                counts[k] = 0;
            }
        }

        private bool ValueIsSmooth(int k, Interval interval)
        {
            var exponents = interval.Exponents;
            for (int i = 0; i <= factorBaseSize; i++)
                exponents[i] = 0;
            if (interval.XRep.IsZero)
                interval.XRep.Set(interval.X);
            var xRep = interval.Reg1.SetSum(interval.XRep, (uint)k);
            var yRep = interval.Reg2.SetSquare(xRep);
            var zRep = interval.Reg3;
            if (yRep < nRep)
            {
                yRep.SetDifference(nRep, yRep);
                exponents[0] = 1;
            }
            else
                yRep.Subtract(nRep);
            for (int i = 0; i < factorBaseSize; i++)
            {
                var p = factorBase[i];
                while (zRep.SetRemainder(yRep, (uint)p).IsZero)
                {
                    ++exponents[i + 1];
                    yRep.Divide((uint)p, zRep);
                }
            }
            return yRep.IsOne;
        }

        private bool ValueIsSmoothBigInteger(int k, Interval interval)
        {
            var exponents = interval.Exponents;
            var x = interval.X + k;
            var y = x * x - n;
            for (int i = 0; i <= factorBaseSize; i++)
                exponents[i] = 0;
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
            return y.IsOne;
        }
    }
}