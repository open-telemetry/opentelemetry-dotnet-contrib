// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Geneva.Tld;

/// <summary>
/// Like ASCIIEncoding but instead of checking for non-ASCII characters, it just
/// ignores the top bits of the character. This is significantly faster than
/// ASCIIEncoding and can be used to improve performance in cases where you know a
/// string only contains ASCII characters.
/// </summary>

#pragma warning disable SA1124
internal sealed class UncheckedASCIIEncoding : Encoding
{
    public static Encoding SharedInstance = new UncheckedASCIIEncoding();

    public UncheckedASCIIEncoding()
        : base(20127)
    {
    }

    #region Optional property overrides (performance/functionality improvement)

    public override bool IsSingleByte => true;

    #endregion

    #region Required implementation of Encoding abstract methods

    public override int GetMaxByteCount(int charCount) => charCount;

    public override int GetMaxCharCount(int byteCount) => byteCount;

    public override int GetByteCount(char[] chars, int index, int count) => count;

    public override int GetCharCount(byte[] bytes, int index, int count) => count;

    public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
    {
        ValidateArgs(chars, charIndex, charCount, bytes, byteIndex, "char", "byte");
        return GetBytesCore(chars.AsSpan(charIndex, charCount), bytes.AsSpan(byteIndex));
    }

    public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
    {
        ValidateArgs(bytes, byteIndex, byteCount, chars, charIndex, "byte", "char");
        return GetCharsCore(bytes.AsSpan(byteIndex, byteCount), chars.AsSpan(charIndex));
    }

    #endregion

    #region Optional overrides (performance/functionality improvement)

    public override int GetByteCount(string s)
    {
        Guard.ThrowIfNull(s);
        return s.Length;
    }

    public override int GetBytes(string s, int charIndex, int charCount, byte[] bytes, int byteIndex)
    {
        if (s == null || bytes == null)
        {
            throw new ArgumentNullException(s == null ? nameof(s) : nameof(bytes));
        }
        else if (charIndex < 0 || charCount < 0)
        {
            throw new ArgumentOutOfRangeException(charIndex < 0 ? nameof(charIndex) : nameof(charCount));
        }
        else if (s.Length - charIndex < charCount)
        {
            throw new ArgumentOutOfRangeException(nameof(s));
        }
        else if (byteIndex < 0 || byteIndex > bytes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(byteIndex));
        }

        return GetBytesCore(s.AsSpan(charIndex, charCount), bytes.AsSpan(byteIndex));
    }

    #endregion

    private static int GetBytesCore(ReadOnlySpan<char> chars, Span<byte> bytes)
    {
        if (bytes.Length < chars.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes));
        }

        for (var i = 0; i < chars.Length; i += 1)
        {
            bytes[i] = unchecked((byte)(chars[i] & 0x7F));
        }

        return chars.Length;
    }

    private static int GetCharsCore(ReadOnlySpan<byte> bytes, Span<char> chars)
    {
        if (chars.Length < bytes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(chars));
        }

        for (var i = 0; i < bytes.Length; i += 1)
        {
            chars[i] = (char)bytes[i];
        }

        return bytes.Length;
    }

    private static void ValidateArgs<TA, TB>(TA[] a, int aIndex, int aCount, TB[] b, int bIndex, string aName, string bName)
    {
        if (a == null || b == null)
        {
            throw new ArgumentNullException(a == null ? aName + "s" : bName + "s");
        }
        else if (aIndex < 0 || aCount < 0)
        {
            throw new ArgumentOutOfRangeException(aIndex < 0 ? aName + "Index" : aName + "Count");
        }
        else if (a.Length - aIndex < aCount)
        {
            throw new ArgumentOutOfRangeException(aName + "s");
        }
        else if (bIndex < 0 || bIndex > b.Length)
        {
            throw new ArgumentOutOfRangeException(bName + "Index");
        }
    }
}
