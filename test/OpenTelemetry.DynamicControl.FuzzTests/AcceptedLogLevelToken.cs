// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.DynamicControl.FuzzTests;

/// <summary>
/// One of the diagnostic log level parser's accepted tokens, with its casing randomly mutated
/// per character.
/// </summary>
public readonly record struct AcceptedLogLevelToken(string Token);
