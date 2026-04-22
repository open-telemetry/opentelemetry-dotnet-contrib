// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;

namespace OpenTelemetry.Sampler.AWS.Tests;

internal sealed class MockServerRequestHandler
{
    private readonly Dictionary<string, string> responses = [];

    public MockServerRequestHandler()
    {
    }

    public MockServerRequestHandler(string path, string responseBody)
    {
        this.responses[path] = responseBody;
    }

    public void SetResponse(string path, string responseBody)
        => this.responses[path] = responseBody;

    public void Handle(HttpListenerContext context)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath;
            if (path != null && this.responses.TryGetValue(path, out var responseBody))
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";

#if NET
                using var writer = new StreamWriter(context.Response.OutputStream, leaveOpen: true);
#else
                using var writer = new StreamWriter(context.Response.OutputStream, new System.Text.UTF8Encoding(false), 4096, leaveOpen: true);
#endif
                writer.Write(responseBody);
            }
            else
            {
                context.Response.StatusCode = 404;
            }
        }
        finally
        {
            context.Response.Close();
        }
    }
}
