// <copyright file="AWSMessageAttributeHelper.cs" company="OpenTelemetry Authors">
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

using System;
using System.Linq;
using Amazon.Runtime;
using Amazon.Runtime.Internal;

namespace OpenTelemetry.Contrib.Instrumentation.AWS.Implementation;

internal class AWSMessageAttributeHelper
{
    // SQS/SNS message attributes collection size limit according to
    // https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-message-metadata.html and
    // https://docs.aws.amazon.com/sns/latest/dg/sns-message-attributes.html
    private const int MaxMessageAttributes = 10;

    private readonly IAWSMessageAttributeFormatter attributeFormatter;

    internal AWSMessageAttributeHelper(IAWSMessageAttributeFormatter attributeFormatter)
    {
        this.attributeFormatter = attributeFormatter ?? throw new ArgumentNullException(nameof(attributeFormatter));
    }

    internal bool TryAddParameter(ParameterCollection parameters, string name, string value)
    {
        var index = this.GetNextAttributeIndex(parameters, name);
        if (!index.HasValue)
        {
            return false;
        }
        else if (index >= MaxMessageAttributes)
        {
            // TODO: Add logging (event source).
            return false;
        }

        var attributePrefix = this.attributeFormatter.AttributeNamePrefix + $".{index.Value}";
        parameters.Add(attributePrefix + ".Name", name.Trim());
        parameters.Add(attributePrefix + ".Value.DataType", "String");
        parameters.Add(attributePrefix + ".Value.StringValue", value.Trim());

        return true;
    }

    private int? GetNextAttributeIndex(ParameterCollection parameters, string name)
    {
        var names = parameters.Where(a => this.attributeFormatter.AttributeNameRegex.IsMatch(a.Key));
        if (!names.Any())
        {
            return 1;
        }

        int? index = 0;
        foreach (var nameAttribute in names)
        {
            if (nameAttribute.Value is StringParameterValue param && param.Value == name)
            {
                index = null;
                break;
            }

            var currentIndex = this.attributeFormatter.GetAttributeIndex(nameAttribute.Key);
            index = (currentIndex ?? 0) > index ? currentIndex : index;
        }

        return ++index;
    }
}
