// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Confluent.Kafka;

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

/// <summary>
/// An asynchronous action to process the <see cref="ConsumeResult{TKey,TValue}"/>.
/// </summary>
/// <param name="consumeResult">The <see cref="ConsumeResult{TKey,TValue}"/>.</param>
/// <param name="cancellationToken">An optional <see cref="CancellationToken"/>.</param>
/// <typeparam name="TKey">The type of key of the <see cref="ConsumeResult{TKey,TValue}"/>.</typeparam>
/// <typeparam name="TValue">The type of value of the <see cref="ConsumeResult{TKey,TValue}"/>.</typeparam>
/// <returns>A <see cref="ValueTask"/>.</returns>
public delegate ValueTask ConsumeAndProcessMessageHandler<TKey, TValue>(
    ConsumeResult<TKey, TValue> consumeResult,
    CancellationToken cancellationToken = default);
