// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;
using Testcontainers.Kafka;
using Xunit;

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

public sealed class KafkaFixture : IAsyncLifetime
{
    private static readonly string KafkaImage = GetKafkaImage();

    public KafkaContainer Container { get; } = CreateKafka();

    public async Task InitializeAsync()
    {
        if (DockerHelper.IsAvailable(DockerPlatform.Linux))
        {
            await this.Container.StartAsync();
        }
    }

    public Task DisposeAsync() =>
        this.Container.DisposeAsync().AsTask();

    private static KafkaContainer CreateKafka() =>
        new KafkaBuilder(KafkaImage).Build();

    private static string GetKafkaImage()
    {
        var assembly = typeof(KafkaFixture).Assembly;

        using var stream = assembly.GetManifestResourceStream("kafka.Dockerfile");

#if NET
        using var reader = new StreamReader(stream!);
#else
        using var reader = new StreamReader(stream);
#endif

        var raw = reader.ReadToEnd();

        // Exclude FROM
        return raw.Substring(4).Trim();
    }
}
