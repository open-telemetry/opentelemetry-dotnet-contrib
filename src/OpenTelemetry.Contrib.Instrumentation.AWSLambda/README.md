# AWS OTel .NET SDK for Lambda

This repo contains SDK to instrument Lambda handler to create incoming span.

## Installation

```shell
dotnet add package OpenTelemetry.Contrib.Instrumentation.AWSLambda
```

## Configuration

Add `AddLambdaConfigurations()` to `TracerProvider`.

```csharp
TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
        // add other instrumentations
        .AddLambdaConfigurations()
        .Build();
```

## Instrumentation

### Lambda Function

1. Create a function with the same signature as the original Lambda function. Call `AWSLambdaWrapper.Trace()` API
and pass `TracerProvider`, original Lambda function and its inputs as parameters.

2. Set the Lambda handler field point to the new function.

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

Override the `FunctionHandlerAsync` function in `LambdaEntryPoint.cs` file. Call `AWSLambdaWrapper.Trace()` API
and pass `TracerProvider`, original Lambda function and its inputs as parameters.
Below is an example if using `APIGatewayProxyFunction` as base class.

```csharp
public override async Task<APIGatewayProxyResponse> FunctionHandlerAsync(APIGatewayProxyRequest request, ILambdaContext lambdaContext)
=> await AWSLambdaWrapper.Trace(tracerProvider, base.FunctionHandlerAsync, request, lambdaContext);
```

## Reference

* [OpenTelemetry Project](https://opentelemetry.io/)
* [OpenTelemetry Lambda](https://github.com/open-telemetry/opentelemetry-lambda)
* [AWS OpenTelemetry Lambda](https://github.com/aws-observability/aws-otel-lambda)
