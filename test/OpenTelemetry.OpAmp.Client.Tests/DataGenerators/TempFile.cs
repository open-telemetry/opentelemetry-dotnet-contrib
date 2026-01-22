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
        var tempFile = Path.GetTempFileName();
        var fileName = Path.GetFileName(tempFile);

        if (!string.IsNullOrEmpty(content))
        {
            File.WriteAllText(tempFile, content);
        }

        return new TempFile(tempFile, fileName);
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
