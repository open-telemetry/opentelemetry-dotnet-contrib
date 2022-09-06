// <copyright file="CommonExtensionsTests.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using OpenTelemetry.Instrumentation.AWSLambda.Implementation;
using Xunit;

namespace OpenTelemetry.Instrumentation.AWSLambda.Tests.Implementation
{
    public class CommonExtensionsTests
    {
        [Theory]
        [InlineData("x-forwarded-test", false, null)]
        [InlineData("x-forwarded-proto", true, "https")]
        [InlineData("X-Forwarded-Proto", true, "https")]
        [InlineData("X-forwarded-proTo", true, "https")]
        public void TryGetValueIgnoringCase_Key_CorrectResult(string key, bool expectedSuccess, string expectedValue)
        {
            var dict = new Dictionary<string, string>
            {
                { "X-Forwarded-Proto", "https" },
            };

            var success = CommonExtensions.TryGetValueIgnoringCase(dict, key, out var value);

            Assert.Equal(expectedSuccess, success);
            Assert.Equal(expectedValue, value);
        }
    }
}
