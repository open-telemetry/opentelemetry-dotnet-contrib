// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Sampler.BottomFloor.Tests;

public class SharedRandomTests
{
    [Fact]
    public void Instance_ProducesValuesInTheExpectedRanges()
    {
        var random = SharedRandom.Instance;

        for (var i = 0; i < 1000; i++)
        {
            var sample = random.NextDouble();
            Assert.True(sample >= 0.0 && sample < 1.0, $"NextDouble returned {sample}");

            Assert.InRange(random.Next(10), 0, 9);
            Assert.InRange(random.Next(5, 10), 5, 9);
            Assert.True(random.Next() >= 0);
        }

        var buffer = new byte[16];
        random.NextBytes(buffer);
        Assert.Contains(buffer, b => b != 0);
    }

    [Fact]
    public void Instance_GivesConcurrentThreadsDistinctSequences()
    {
        // On .NET Framework the parameterless Random constructor seeds from the
        // tick count, so threads that start within the same tick draw identical
        // sequences. That would silently correlate the per-arrival draws across
        // threads and bias the sample rather than fail outright, so the seeding
        // is asserted here. The threads are released together to make the
        // same-tick collision as likely as possible.
        const int threads = 8;
        const int draws = 20;

        var sequences = new double[threads][];
        var start = new ManualResetEventSlim(false);
        var workers = new Thread[threads];

        for (var i = 0; i < threads; i++)
        {
            var index = i;
            workers[i] = new Thread(() =>
            {
                start.Wait();
                var local = new double[draws];
                for (var d = 0; d < draws; d++)
                {
                    local[d] = SharedRandom.Instance.NextDouble();
                }

                sequences[index] = local;
            });

            workers[i].Start();
        }

        start.Set();
        foreach (var worker in workers)
        {
            Assert.True(worker.Join(TimeSpan.FromSeconds(30)), "a worker thread did not finish");
        }

        var distinct = sequences.Select(s => string.Join(",", s)).Distinct().Count();
        Assert.Equal(threads, distinct);
    }
}
