// <copyright file="RateLimiter.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Threading;

namespace OpenTelemetry.Sampler.AWS;

internal sealed class RateLimiter
{
    private readonly Clock clock;
    private readonly double creditsPerMillisecond;
    private readonly long maxBalance;
    private long currentBalance;

    internal RateLimiter(double creditsPerSecond, double maxBalance, Clock clock)
    {
        this.clock = clock;
        this.creditsPerMillisecond = creditsPerSecond / 1.0e3;
        this.maxBalance = (long)(maxBalance / this.creditsPerMillisecond);
        this.currentBalance = this.clock.NowInMilliSeconds() - this.maxBalance;
    }

    public bool TrySpend(double itemCost)
    {
        long cost = (long)(itemCost / this.creditsPerMillisecond);
        long currentMillis;
        long currentBalanceMillis;
        long availableBalanceAfterWithdrawal;

        do
        {
            currentBalanceMillis = Interlocked.Read(ref this.currentBalance);
            currentMillis = this.clock.NowInMilliSeconds();
            long currentAvailableBalance = currentMillis - currentBalanceMillis;
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
        while (Interlocked.CompareExchange(ref this.currentBalance, currentMillis - availableBalanceAfterWithdrawal, currentBalanceMillis) != currentBalanceMillis);

        return true;
    }
}
