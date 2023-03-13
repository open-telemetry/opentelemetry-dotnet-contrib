// <copyright file="AWSXRayRemoteSampler.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using System.Threading;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Contrib.Extensions.AWSXRay.Trace;

public class AWSXRayRemoteSampler : Sampler
{
    private TimeSpan pollingInterval;
    private string endpoint;
    private AWSXRaySamplerClient client;

    private Timer rulePollerTimer;
    private Timer targetPollerTimer;

    internal AWSXRayRemoteSampler(TimeSpan pollingInterval, string endpoint)
    {
        this.pollingInterval = pollingInterval;
        this.endpoint = endpoint;
        this.client = new AWSXRaySamplerClient(endpoint);

        // this.StartBackgroundThread();

        // execute the first update right away
        this.GetAndUpdateSampler(new Object());
        //this.rulePollerTimer = new Timer(this.GetAndUpdateSampler, null, 0, 10000000);
        //this.targetPollerTimer = new Timer(this.GetTargets, null, 1000, 0); // start with an initial delay of 10 seconds
    }

    public static AWSXRayRemoteSamplerBuilder Builder()
    {
        return new AWSXRayRemoteSamplerBuilder();
    }

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        throw new System.NotImplementedException();
    }

    private void GetAndUpdateSampler(Object state)
    {
        //Timer ruleTimer = new Timer(this.RefreshRules, null, 0, this.pollingInterval.Ticks / 10000); // 1 tick is 100 nanoseconds
        this.RefreshRules();
    }

    private void RefreshRules()
    {
        Console.WriteLine("Getting Sampling Rules...");
        Console.WriteLine("ThreadID: " + Thread.CurrentThread.ManagedThreadId);
        Thread.Sleep(500);

        List<SamplingRule> rules = this.client.GetSamplingRules().Result;
        Console.WriteLine("Got the rules::" + rules.Count);
    }

    private void GetTargets(Object state)
    {
        Console.WriteLine("Getting Sampling Targets...");
        Console.WriteLine("ThreadID: " + Thread.CurrentThread.ManagedThreadId);
        Thread.Sleep(500);
        Console.WriteLine("Got the targets");
        targetPollerTimer.Change(1000, 0);
    }

    private void StartBackgroundThread()
    {
        Thread t1 = new Thread(() =>
        {
            Console.WriteLine("Inside thread t1");
            for (int i = 0; i < 10; i++)
            {
                Console.WriteLine("thread is in process");
                Thread.Sleep(1000);
            }
        });
        t1.IsBackground = true;

        t1.Start();
    }
}
