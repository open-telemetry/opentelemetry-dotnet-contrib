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

using System;
using OpenTelemetry.Instrumentation.AWSLambda.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AWSLambda;

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
    public static TracerProviderBuilder AddAWSLambdaConfigurations(this TracerProviderBuilder builder) =>
        AddAWSLambdaConfigurations(builder, configure: null);

    /// <summary>
    /// Add AWS Lambda configurations.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="configure">AWS lambda instrumentation options.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddAWSLambdaConfigurations(
        this TracerProviderBuilder builder,
        Action<AWSLambdaInstrumentationOptions>? configure)
    {
        Guard.ThrowIfNull(builder);

        var options = new AWSLambdaInstrumentationOptions();
        configure?.Invoke(options);

        AWSLambdaWrapper.DisableAwsXRayContextExtraction = options.DisableAwsXRayContextExtraction;
        AWSMessagingUtils.SetParentFromMessageBatch = options.SetParentFromBatch;

        builder.AddSource(AWSLambdaWrapper.ActivitySourceName);
        builder.ConfigureResource(x => x.AddDetector(new AWSLambdaResourceDetector()));

        return builder;
    }
}
