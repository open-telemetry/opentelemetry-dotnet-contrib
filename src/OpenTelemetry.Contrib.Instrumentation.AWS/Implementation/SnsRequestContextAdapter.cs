// <copyright file="SnsRequestContextAdapter.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.SimpleNotificationService.Model;

namespace OpenTelemetry.Contrib.Instrumentation.AWS.Implementation;
internal class SnsRequestContextAdapter : IRequestContextAdapter
{
    private readonly ParameterCollection parameters;
    private readonly PublishRequest originalRequest;

    public SnsRequestContextAdapter(IRequestContext context)
    {
        this.parameters = context.Request?.ParameterCollection;
        this.originalRequest = context.OriginalRequest as PublishRequest;
    }

    public bool CanInject => this.originalRequest != null;

    public int AttributesCount =>
        this.originalRequest?.MessageAttributes.Count ?? 0;

    public void AddAttribute(string name, string value, int nextAttributeIndex)
    {
        if (this.parameters == null)
        {
            return;
        }

        var prefix = "MessageAttributes.entry." + nextAttributeIndex;
        this.parameters.Add(prefix + ".Name", name);
        this.parameters.Add(prefix + ".Value.DataType", "String");
        this.parameters.Add(prefix + ".Value.StringValue", value);

        // Add injected attributes to the original request as well.
        // This dictionary must be in sync with parameters collection to pass through the MD5 hash matching check.
        this.originalRequest?.MessageAttributes.Add(name, new MessageAttributeValue { DataType = "String", StringValue = value });
    }

    public bool ContainsAttribute(string name)
        => this.originalRequest?.MessageAttributes.ContainsKey(name) ?? false;
}
