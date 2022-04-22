// <copyright file="TracerProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Contrib.Instrumentation.AWSLambda.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension class for TracerProviderBuilder.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Add AWS Lambda configurations.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddAWSLambdaConfigurations(this TracerProviderBuilder builder)
    {
        Guard.ThrowIfNull(builder);

        builder.AddSource(AWSLambdaUtils.ActivitySourceName);
        builder.SetResourceBuilder(ResourceBuilder
            .CreateEmpty()
            .AddService(AWSLambdaUtils.GetFunctionName(), null, null, false)
            .AddTelemetrySdk()
            .AddAttributes(AWSLambdaResourceDetector.Detect()));

        return builder;
    }
}
