// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace OpenTelemetry.OpAmp.Client.Tests.DataGenerators;

internal class TempFile : IDisposable
{
    private bool isDisposed;

    private TempFile(string path, string fileName)
    {
        this.FilePath = path;
        this.FileName = fileName;
    }

    public string FilePath { get; }

    public string FileName { get; set; }

    public static TempFile Create(string content)
    {
        var tempDir = Path.GetTempPath();
        var fileName = Path.GetRandomFileName();

        // Manual separator handling (cross-platform)
        if (!tempDir.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            tempDir += Path.DirectorySeparatorChar;
        }

        var fullPath = tempDir + fileName;

        using var stream = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None);

        if (!string.IsNullOrEmpty(content))
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
            stream.Position = 0;
        }

        return new TempFile(fullPath, fileName);
    }

    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.isDisposed = true;

        try
        {
            if (!File.Exists(this.FilePath))
            {
                File.Delete(this.FilePath);
            }
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}
