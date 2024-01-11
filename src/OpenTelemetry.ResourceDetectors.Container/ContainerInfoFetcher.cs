// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Text.RegularExpressions;

namespace OpenTelemetry.ResourceDetectors.Container;

internal abstract class ContainerInfoFetcher
{
    private static readonly Regex HexStringRegex = new("(^[a-fA-F0-9]+$)", RegexOptions.Compiled);

    private readonly ApiConnector? apiConnector;

    protected ContainerInfoFetcher(ApiConnector apiConnector)
    {
        this.apiConnector = apiConnector;
    }

    public string ExtractContainerId()
    {
        // executing request only once. Do we need to retry again if data not available?
        string response = this.ExecuteApiRequest();
        if (response == null)
        {
            return string.Empty;
        }

        return this.ParseResponse(response);
    }

    protected static bool CheckAndInitProp(string envPropName, string? sysPropName, out string? result, bool canContinue = false)
    {
        // preference to Env Var and then Sys Prop
        // propName will be either Env Var or Sys Prop key, whichever is found
        string? value = Environment.GetEnvironmentVariable(envPropName);

        if (value == null && sysPropName != null)
        {
            // if Env Var not found, then check for Sys Prop
            value = Environment.GetEnvironmentVariable(sysPropName);
        }

        if (value == null || string.IsNullOrEmpty(value))
        {
            result = null;

            return false;
        }

        // whichever prop is found
        result = value;
        return true;
    }

    protected static bool CheckFileAndInitProp(string dirName, string fileName, bool isCertFile, out string result)
    {
        result = string.Empty;
        string filePath = Path.Combine(dirName, fileName);
        try
        {
            FileInfo fileInfo = new(filePath);

            if (!IsFilePresent(fileInfo))
            {
                return false;
            }

            // if this is certificate file, we don't have to read it yet but only check for existence and readability
            // ca.cert will be directly consumed as input stream when building SSL context
            if (isCertFile)
            {
                result = Path.GetFullPath(filePath);
            }
            else
            {
                string data = File.ReadAllText(filePath).Trim();

                // file only has whitespaces
                if (string.IsNullOrEmpty(data))
                {
                    return false;
                }

                result = data;
            }
        }
        catch (Exception e)
        {
            ContainerExtensionsEventSource.Log.ExtractResourceAttributesException("Cannot Read " + Path.GetFullPath(filePath) + " : " + e.Message, e);
            return false;
        }

        return true;
    }

    protected static string FormatContainerId(string unFormattedId)
    {
        // "containerID"="docker://18e1f4b72f6861b5e591e11ea6db0640377de6ed5dc9bffbae4d9ab284d53044"
        // Assuming kube api return container id always in this format prefixed with 'docker://'. (Big assumption?)
        string formattedId = unFormattedId.Substring(unFormattedId.LastIndexOf("/", StringComparison.InvariantCulture) + 1);
        Console.WriteLine($"{formattedId}");
        // should be valid hex string
        if (!HexStringRegex.Match(formattedId).Success)
        {
            return string.Empty;
        }

        return formattedId;
    }

    protected abstract string ParseResponse(string response);

    private static bool IsFilePresent(FileInfo fileInfo)
    {
        return fileInfo.Exists && (fileInfo.Length != 0);
    }

    private string ExecuteApiRequest()
    {
        return this.apiConnector!.ExecuteRequest();
    }
}
