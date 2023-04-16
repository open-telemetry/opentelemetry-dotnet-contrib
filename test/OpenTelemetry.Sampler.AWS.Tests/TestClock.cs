// <copyright file="TestClock.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTelemetry.Sampler.AWS.Tests;

internal class TestClock : Clock
{
    private DateTime nowTime;

    public TestClock()
    {
        this.nowTime = DateTime.Now;
    }

    public TestClock(DateTime time)
    {
        this.nowTime = time;
    }

    public override DateTime Now()
    {
        return this.nowTime;
    }

    public override long NowInSeconds()
    {
        throw new NotImplementedException();
    }

    public override DateTime ToDateTime(double seconds)
    {
        throw new NotImplementedException();
    }

    public override double ToDouble(DateTime dateTime)
    {
        throw new NotImplementedException();
    }
}
