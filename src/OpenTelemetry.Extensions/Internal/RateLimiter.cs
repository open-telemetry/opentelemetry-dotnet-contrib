// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics;

namespace OpenTelemetry.Extensions.Internal;

internal sealed class RateLimiter
{
    private readonly Func<long> getElapsedTicks;
    private readonly double creditsPerTick;
    private readonly long maxBalance; // max balance in ticks
    private long currentBalance; // last op ticks less remaining balance, using long directly with Interlocked for thread safety

    public RateLimiter(double creditsPerSecond, double maxBalance)
        : this(creditsPerSecond, maxBalance, Stopwatch.StartNew())
    {
    }

    internal RateLimiter(double creditsPerSecond, double maxBalance, Func<long> getElapsedTicks, long ticksPerSecond)
    {
        this.getElapsedTicks = getElapsedTicks;
        this.creditsPerTick = creditsPerSecond / ticksPerSecond;
        this.maxBalance = (long)(maxBalance / this.creditsPerTick);
        this.currentBalance = this.getElapsedTicks() - this.maxBalance;
    }

    private RateLimiter(double creditsPerSecond, double maxBalance, Stopwatch stopwatch)
        : this(creditsPerSecond, maxBalance, () => stopwatch.ElapsedTicks, Stopwatch.Frequency)
    {
    }

    public bool TrySpend(double itemCost)
    {
        var cost = (long)(itemCost / this.creditsPerTick);
        long currentTicks;
        long currentBalanceTicks;
        long availableBalanceAfterWithdrawal;
        do
        {
            currentBalanceTicks = Interlocked.Read(ref this.currentBalance);
            currentTicks = this.getElapsedTicks();
            var currentAvailableBalance = currentTicks - currentBalanceTicks;
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

        // CompareExchange will fail if currentBalance has changed since the last read, implying another thread has updated the balance
        while (Interlocked.CompareExchange(ref this.currentBalance, currentTicks - availableBalanceAfterWithdrawal, currentBalanceTicks) != currentBalanceTicks);
        return true;
    }
}
