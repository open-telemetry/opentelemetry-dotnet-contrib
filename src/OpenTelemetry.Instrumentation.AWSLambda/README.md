# AWS OTel .NET SDK for Lambda

This repo contains SDK to instrument Lambda handler to create incoming span.

## Installation

```shell
dotnet add package OpenTelemetry.Instrumentation.AWSLambda
```

## Configuration

Add `AddAWSLambdaConfigurations()` to `TracerProvider`.

```csharp
TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
        // add other instrumentations
        .AddAWSLambdaConfigurations()
        .Build();
```

## Instrumentation

### Lambda Function

1. Create a wrapper function with the same signature as the original Lambda function.
Call `AWSLambdaWrapper.Trace()` API and pass `TracerProvider`, original Lambda function
and its inputs as parameters.

2. Set the wrapper function as the Lambda handler input.

```csharp
// new Lambda function handler passed in
public string TracingFunctionHandler(JObject input, ILambdaContext context)
=> AWSLambdaWrapper.Trace(tracerProvider, OriginalFunctionHandler, input, context);

public string OriginalFunctionHandler(JObject input, ILambdaContext context)
{
    var S3Client = new AmazonS3Client();
    var httpClient = new HttpClient();
    _ = S3Client.ListBucketsAsync().Result;
    _ = httpClient.GetAsync("https://aws.amazon.com").Result;
    return input?.ToString();
}
```

### Lambda Function - Asp.Net Core

For using base classes from package [Amazon.Lambda.AspNetCoreServer](https://github.com/aws/aws-lambda-dotnet/tree/master/Libraries/src/Amazon.Lambda.AspNetCoreServer#amazonlambdaaspnetcoreserver),
override the `FunctionHandlerAsync` function in `LambdaEntryPoint.cs` file. Call
`AWSLambdaWrapper.Trace()` API and pass `TracerProvider`, original Lambda function
and its inputs as parameters. Below is an example if using `APIGatewayProxyFunction`
as base class.

```csharp
public override async Task<APIGatewayProxyResponse> FunctionHandlerAsync(
    APIGatewayProxyRequest request, ILambdaContext lambdaContext)
=> await AWSLambdaWrapper.Trace(tracerProvider, base.FunctionHandlerAsync,
    request, lambdaContext);
```

## Example

A more detailed Lambda function example that takes a JObject and ILambdaContext
as inputs.

```csharp
public class Function
{
    public static TracerProvider tracerProvider;

    static Function()
    {
        tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddHttpClientInstrumentation()
                .AddAWSInstrumentation()
                .AddOtlpExporter()
                .AddAWSLambdaConfigurations()
                .Build();
    }

    public string TracingFunctionHandler(JObject input, ILambdaContext context)
    => AWSLambdaWrapper.Trace(tracerProvider, FunctionHandler, input, context);

    /// <summary>
    /// A simple function that takes a JObject input and sends downstream http
    /// request to aws.amazon.com and aws request to S3.
    /// </summary>
    public string FunctionHandler(JObject input, ILambdaContext context)
    {
        var S3Client = new AmazonS3Client();
        var httpClient = new HttpClient();
        _ = S3Client.ListBucketsAsync().Result;
        _ = httpClient.GetAsync("https://aws.amazon.com").Result;
        return input?.ToString();
    }
}
```

## Reference

* [OpenTelemetry Project](https://opentelemetry.io/)
* [OpenTelemetry Lambda](https://github.com/open-telemetry/opentelemetry-lambda)
* [AWS OpenTelemetry Lambda](https://github.com/aws-observability/aws-otel-lambda)
