// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Web;
using System.Web.Hosting;

namespace OpenTelemetry.Instrumentation.AspNet.Tests;

internal class HttpContextHelper
{
    public static HttpContext GetFakeHttpContext(string page = "/page", string query = "", IDictionary<string, string>? headers = null)
    {
        Thread.GetDomain().SetData(".appPath", string.Empty);
        Thread.GetDomain().SetData(".appVPath", string.Empty);

        var workerRequest = new SimpleWorkerRequestWithHeaders(page, query, new StringWriter(CultureInfo.InvariantCulture), headers);
        var context = new HttpContext(workerRequest);
        HttpContext.Current = context;
        return context;
    }

    public static HttpContextBase GetFakeHttpContextBase(string page = "/page", string query = "", IDictionary<string, string>? headers = null)
    {
        var context = GetFakeHttpContext(page, query, headers);
        return new HttpContextWrapper(context);
    }

    private class SimpleWorkerRequestWithHeaders : SimpleWorkerRequest
    {
        private readonly IDictionary<string, string> headers;

        public SimpleWorkerRequestWithHeaders(string page, string query, TextWriter output, IDictionary<string, string>? headers)
            : base(page, query, output)
        {
            if (headers != null)
            {
                this.headers = headers;
            }
            else
            {
                this.headers = new Dictionary<string, string>();
            }
        }

        public override string[][] GetUnknownRequestHeaders()
        {
            List<string[]> result = new List<string[]>();

            foreach (var header in this.headers)
            {
                result.Add(new string[] { header.Key, header.Value });
            }

            var baseResult = base.GetUnknownRequestHeaders();
            if (baseResult != null)
            {
                result.AddRange(baseResult);
            }

            return result.ToArray();
        }

        public override string GetUnknownRequestHeader(string name)
        {
            if (this.headers.TryGetValue(name, out var value))
            {
                return value;
            }

            return base.GetUnknownRequestHeader(name);
        }

        public override string GetKnownRequestHeader(int index)
        {
            var name = GetKnownRequestHeaderName(index);

            if (this.headers.TryGetValue(name, out var value))
            {
                return value;
            }

            return base.GetKnownRequestHeader(index);
        }
    }
}
