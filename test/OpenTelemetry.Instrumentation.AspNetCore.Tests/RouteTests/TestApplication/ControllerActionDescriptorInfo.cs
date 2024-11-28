// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace RouteTests.TestApplication;

#pragma warning disable CA1515
public class ControllerActionDescriptorInfo
#pragma warning restore CA1515
{
    public ControllerActionDescriptorInfo()
    {
    }

    public ControllerActionDescriptorInfo(string controllerName, string actionName)
    {
        this.ControllerActionDescriptorControllerName = controllerName;
        this.ControllerActionDescriptorActionName = actionName;
    }

    [JsonPropertyName("ControllerName")]
    public string ControllerActionDescriptorControllerName { get; set; } = string.Empty;

    [JsonPropertyName("ActionName")]
    public string ControllerActionDescriptorActionName { get; set; } = string.Empty;
}
