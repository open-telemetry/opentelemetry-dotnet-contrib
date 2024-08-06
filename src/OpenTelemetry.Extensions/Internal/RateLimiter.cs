// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics;

namespace OpenTelemetry.Extensions.Internal;

internal sealed class RateLimiter
{
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    private readonly double creditsPerNanosecond;
    private readonly long maxBalance; // max balance in ticks
    private long currentBalance; // last op ticks less remaining balance, using long directly with Interlocked for thread safety

    public RateLimiter(double creditsPerSecond, double maxBalance)
    {
        this.creditsPerNanosecond = creditsPerSecond / Stopwatch.Frequency;
        this.maxBalance = (long)(maxBalance / this.creditsPerNanosecond);
        this.currentBalance = this.stopwatch.ElapsedTicks - this.maxBalance;
    }

    public bool TrySpend(double itemCost)
    {
        long cost = (long)(itemCost / this.creditsPerNanosecond);
        long currentNanos;
        long currentBalanceNanos;
        long availableBalanceAfterWithdrawal;
        do
        {
            currentBalanceNanos = Interlocked.Read(ref this.currentBalance);
            currentNanos = this.stopwatch.ElapsedTicks;
            long currentAvailableBalance = currentNanos - currentBalanceNanos;
            if (currentAvailableBalance > this.maxBalance)
            {
                currentAvailableBalance = this.maxBalance;
            }

            availableBalanceAfterWithdrawal = currentAvailableBalance - cost;
            if (availableBalanceAfterWithdrawal < 0)
            {
                return false;
            }
        }
        while (Interlocked.CompareExchange(ref this.currentBalance, currentNanos - availableBalanceAfterWithdrawal, currentBalanceNanos) != currentBalanceNanos);
        return true;
    }
}
