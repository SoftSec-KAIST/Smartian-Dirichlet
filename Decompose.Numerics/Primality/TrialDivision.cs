﻿namespace Decompose.Numerics
{
    public class TrialDivisionPrimality : IPrimalityAlgorithm<int>
    {
        public bool IsPrime(int n)
        {
            if (n < 2)
                return false;
            if (n <= 3)
                return true;
            if ((n & 1) == 0)
                return false;
            if (n % 3 == 0)
                return false;
            int p = 5;
            int i = 2;
            while (p * p <= n)
            {
                if (n % p == 0)
                    return false;
                p += i;
                i = 6 - i;
            }
            return true;
        }
    }
}
