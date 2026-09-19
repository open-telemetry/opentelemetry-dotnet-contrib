// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.DynamicControl.FuzzTests;

/// <summary>
/// A string whose characters are drawn from a small pool of characters meaningful to the
/// numeric parsers under test (digits, signs, exponent markers, whitespace, an unpaired
/// surrogate, etc.), so that a much larger share of generated inputs exercise their
/// numeric-looking code paths than plain arbitrary strings would.
/// </summary>
public readonly record struct NumericLookingText(string Text);
