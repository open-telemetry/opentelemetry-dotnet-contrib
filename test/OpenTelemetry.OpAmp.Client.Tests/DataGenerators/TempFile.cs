// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
        var fullPath = Path.Combine(tempDir, fileName);

        if (!string.IsNullOrEmpty(content))
        {
            File.WriteAllText(fullPath, content);
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
