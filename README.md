>ℹ️&nbsp;&nbsp;SignalFx was acquired by Splunk in October 2019. See [Splunk SignalFx](https://www.splunk.com/en_us/investor-relations/acquisitions/signalfx.html) for more information.

> # :warning: Deprecation Notice
> The SignalFx .NET Lambda Wrapper is deprecated. Only critical security fixes and bug fixes are provided.
>
> After October 9th, 2024 this repository will be archived (read-only). After October 9th, 2024, no updates will be provided and this repository will be unsupported.
> 
> Going forward, Lambda functions should use the Splunk OpenTelemetry Lambda Layer, which offers similar capabilities and fully supports the OpenTelemetry standard. To learn more about the Splunk OTel Lambda Layer, see https://docs.splunk.com/Observability/gdi/get-data-in/serverless/aws/otel-lambda-layer/instrument-lambda-functions.html#nav-Instrument-your-Lambda-function

# SignalFx .NET Lambda Wrapper

The SignalFx .NET Lambda Wrapper wraps around an AWS Lambda .NET or ASP.NET Core
function handler. This enables you to send metrics and traces to Splunk APM.

The Lambda wrapper manually instruments the Lambda function itself, and not
any libraries or frameworks. To get traces from libraries and frameworks in
the Lambda function, manually instrument them.

To see manual instrumentation in action, see the
[OpenTracing examples](https://github.com/signalfx/tracing-examples/tree/master/dotnet-manual-instrumentation).

There are two options to use the SignalFx Lambda wrapper:

- Manually deploy the Lambda wrapper to your code
- Use a Lambda layer that Splunk hosts

## Manually deploy the Lambda wrapper to your code

Follow these steps to add the SignalFx Lambda wrapper for .NET or ASP.NET Core.

### Step 1. Add the wrapper to your project

Add the [`signalfx-lambda-functions` NuGet package](https://www.nuget.org/packages/signalfx-lambda-functions/)
to your project.

After adding the NuGet package, what to do next depends on whether you're
instrumenting .NET or ASP.NET Core. See the following sections according to
what you're instrumenting.

#### Add the wrapper to your AWS Serverless ASP.NET Core project

Follow these steps to add the wrapper to your ASP.NET Core Lambda function
after adding the NuGet package.

1. Change the base class of your `LambdaEntryPoint` to the corresponding
   wrapper class:

   | Original AWS Base Type | Wrapper Type |
   | ----------------------- | ------------------------------------------------ |
   | APIGatewayProxyFunction | APIGatewayProxyFunctionWrapper |
   | APIGatewayHttpApiV2ProxyFunction | APIGatewayHttpApiV2ProxyFunctionWrapper |

2. Add `TracingDecoratorFilter` to the `Startup.ConfigureServices` method to
   enrich span data. Here's an example of what this looks like:

   ```c#
           public void ConfigureServices(IServiceCollection services)
           {
               // Add the tracing decorator to enrich span data.
               services.AddControllers(config => config.Filters.Add(new TracingDecoratorFilter()));
           }
   ```

   Check out these sample projects for working examples:

   - [Sample ASP.NET Core AWS HttpApi V1](./src/SampleServerlessASPNETCore)
   - [Sample ASP.NET Core AWS HttpApi V2](./src/SampleServerlessHttpApiV2ASPNETCore/)

#### Add the wrapper to your AWS .NET Lambda project

Manually add the wrapper to the code of the function type:

```c#
        // Static reference to the SignalFx function wrapper.
        private static FunctionWrapper s_functionWrapper = new FunctionWrapper();

        ...

        /// <summary>
        /// This is the modified handler to use the wrapper over a non-void synchronous function.
        /// </summary>
        public string FunctionWithReturn(string input, ILambdaContext context)
        {
            return s_functionWrapper.Invoke(OriginalFunctionWithReturn, input, context);
        }
```

Check out this sample project for a working example:

- [Sample Lambda Functions](./src/SampleLambdaFunctions)

The are overloads of the `Invoke` method with different signatures to support
the typical function signatures and specific ones to support API Gateway
functions:

- `InvokeAPIGatewayProxyAsync`
- `InvokeAPIGatewayHttpApiV2ProxyAsync`

All of the methods above support enrichment of spans with these optional
parameters:

| Parameter | Description |
| --------- | ----------- |
| `operationName` | Set a name for the span. If you don't specify a name, it defaults to the Lambda function name. |
| `tags` | Specify key-value pairs to add span tags to the span. |

You can also enrich spans with environment variables. For more information, see
[Configure the SignalFx Tracing Library for .NET](https://github.com/signalfx/signalfx-dotnet-tracing#configure-the-signalfx-tracing-library-for-net).

### Step 2. Get your organization's realm and access token

Use your realm to configure your ingest endpoint and an access token to
associate data to your organization.

To find which realm you're in, go to **Settings > My Profile**.

To find or create an access token for your organization, go to
**Settings > Organization Settings > Access Tokens**.

### Step 3: Set Lambda Function environment variables

Follow these steps to configure your access token and ingest endpoint:

1. Set `SIGNALFX_ACCESS_TOKEN` with your access token:

   ```bash
   SIGNALFX_ACCESS_TOKEN=<access-token>
   ```

2. Set `SIGNALFX_ENDPOINT_URL` with your organization's realm:

   ```bash
   SIGNALFX_ENDPOINT_URL=https://ingest.<realm>.signalfx.com
   ```

   If you're sending data to an OpenTelemetry Collector, you have to specify
   the full path like this: `http://<otel-collector-host>:9411/api/v2/spans`

3. Set `SIGNALFX_ENV` to specify the environment for the service in APM:

   ```bash
   SIGNALFX_ENV="yourEnvironment"
   ```

4. If you didn't already specify span tags with the `tags` parameter, add
   span tags to each span by setting `SIGNALFX_TRACE_GLOBAL_TAGS`:

   ```bash
   SIGNALFX_TRACE_GLOBAL_TAGS="key1:val1,key2:val2"
   ```

5. (Optional) Globally enable metrics by setting `SIGNALFX_METRICS_ENABLED`:

   ```bash
   SIGNALFX_METRICS_ENABLED=true
   ```

6. (Optional) Disable context propagation. The wrapper currently supports only
   B3 context propagation. By default, you should enable context propagation.
   The option to disable this is for security considerations.

   ```bash
   SIGNALFX_CTX_PROPAGATION_ENABLED=false
   ```

7. (Optional) Specify other environment variables to better configure the traces.

   For a list of all the available configuration options available, see
   [Configure the SignalFx Tracing Library for .NET](https://github.com/signalfx/signalfx-dotnet-tracing#configure-the-signalfx-tracing-library-for-net).

## Deploy the Lambda wrapper with a Lambda layer

Add a layer that includes the SignalFx Lambda wrapper to your Lambda function.
A layer is code and other content that you can run without including it in
your deployment package. SignalFx provides layers in all supported regions you
can freely use. If you want to reduce the size of your deployment, you can
add the package as a developer dependency, but in a production environment you
should add the wrapper as a layer to reduce the deployment size.

To learn more about AWS Lambda Layers, see
[AWS Lambda layers](https://docs.aws.amazon.com/lambda/latest/dg/configuration-layers.html)
on the AWS website.

If you want detailed information about using AWS Lambda Layers with .NET Core, see
[Layers for .NET Core Lambda Functions](https://github.com/aws/aws-extensions-for-dotnet-cli/blob/master/docs/Layers.md#layers-for-net-core-lambda-functions).

Follow these steps to use the SignalFx .NET Lambda Wrapper layer. You can also
use a Lambda configuration file or the
[AWS Extensions for .NET CLI](https://github.com/aws/aws-extensions-for-dotnet-cli#aws-extensions-for-net-cli).
See the following sections for examples. The examples use `netcoreapp3.1`
as the target framework, but `netcoreapp2.1` is also supported.

1. From the AWS console, add a layer to your Lambda function code.
2. Get the ARN layer according to the deployment region and .NET runtime of
   your Lambda:
    - [NetCoreApp 2.1](https://github.com/signalfx/lambda-layer-versions/blob/master/csharp/CSHARP-NetCoreApp21.md)
    - [NetCoreApp 3.1](https://github.com/signalfx/lambda-layer-versions/blob/master/csharp/CSHARP-NetCoreApp31.md)
3. Add the environment variable `DOTNET_SHARED_STORE` to `/opt/dotnetcore/store/`
   to Lambda configuration.
4. Explicitly set `framework` and `function-runtime` to ensure proper deployment.
5. Add the layer ARN to the set of layers the Lambda function uses.

### Deploy the Lambda layer with a configuration file

Use the `json` configuration file to build and deploy your Lambda function. By default, this is named `aws-lambda-tools-defaults.json`.

```json
       "environment-variables" : "\"DOTNET_SHARED_STORE\"=\"/opt/dotnetcore/store/\"",
       "framework"             : "netcoreapp3.1",
       "function-runtime"      : "dotnetcore3.1",
       "function-layers"       : "<arn-from-step-1>",
```

### Deploy the Lambda layer with the .NET CLI

Add these parameters with the `AWS Extensions for .NET CLI` CLI to build and
deploy your Lambda function:

```terminal
      --environment-variables DOTNET_SHARED_STORE=/opt/dotnetcore/store/ 
      --framework netcoreapp3.1
      --function-runtime dotnetcore3.1
      --function-layers <arn-from-step-1>
```

## Metrics and dimensions the Lambda wrapper sends

The Lambda wrapper sends these metrics to Splunk APM:

| Metric Name  | Type | Description |
| ------------ | ---- | ------------|
| `function.invocations`  | Counter  | The number of Lambda invocations. |
| `function.cold_starts`  | Counter  | The number of cold starts. |
| `function.errors`  | Counter  | The number of errors from the underlying Lambda handler. |
| `function.duration`  | Gauge  | The execution time of the underlying Lambda handler, in milliseconds. |

The Lambda wrapper adds these dimensions to all data points it sends to
Splunk APM:

| Dimension | Description |
| --------- | ------------|
| `lambda_arn`  | The ARN of the Lambda function instance. |
| `aws_region`  | The AWS region. |
| `aws_account_id` | The AWS Account ID. |
| `aws_function_name`  | The Lambda function name. |
| `aws_function_version`  | The Lambda function version. |
| `aws_function_qualifier`  | The Lambda function version qualifier. If it's not an event source mapping Lambda invocation, it's the version or version alias. |
| `event_source_mappings`  | The Lambda function name, if it's an event source mapping Lambda invocation. |
| `aws_execution_env`  | The AWS execution environment. For example, `AWS_Lambda_dotnetcore3.1`. |
| `function_wrapper_version`  | The SignalFx function wrapper qualifier, For example, `signalfx_lambda_3.0.1.0`. |
| `metric_source` | The literal value of `lambda_wrapper`. |

## Tags the Lambda wrapper sends

The tracing wrapper creates a span for the wrapper handler. That span contains
these tags:

| Tag | Description |
|---- | ----------- |
| `aws_request_id` | The AWS request ID. |
| `lambda_arn` | The ARN of the Lambda function instance. |
| `aws_region` | The AWS region. |
| `aws_account_id` | The AWS account ID. |
| `aws_function_name` | The Lambda function name. |
| `aws_function_version` | The Lambda function version. |
| `aws_function_qualifier` | The Lambda function version qualifier. If it's not an event source mapping Lambda invocation, it's the version or version alias. |
| `event_source_mappings` | The Lambda function name, if it's an event source mapping Lambda invocation. |
| `aws_execution_env` | The AWS execution environment. For example, `AWS_Lambda_dotnetcore3.1`. |
| `function_wrapper_version` | The SignalFx function wrapper qualifier, For example, `signalfx_lambda_3.0.1.0`. |
| `component` | The literal value of `dotnet-lambda-wrapper`. |

## Add extra tags and enrich traces

There are several ways to add extra tags or enrich the traces of your service:

- Pass span tags with the `tags` parameter.
- Update the span name with the `operationName` parameter.
- For ASP.NET Core applications, add custom action filters to add tags or
  various other operations available to manual instrumentation.
- Use the [TracingDecoratorFilter](./src/SignalFx.LambdaWrapper/AspNetCoreServer/TracingDecoratorFilter.cs)
  as a starting point for your own implementation.
- Use OpenTracing anywhere in your application to add span tags, spans, or
  context propagation. Use the [examples](https://github.com/signalfx/tracing-examples/tree/master/dotnet-manual-instrumentation)
  as a starting point. You don't need to add a package the project since
  `signalfx-lambda-functions` includes the OpenTracing library.

## License

Apache Software License v2. Copyright © 2014-2021 Splunk

>ℹ️&nbsp;&nbsp;SignalFx was acquired by Splunk in October 2019. See [Splunk SignalFx](https://www.splunk.com/en_us/investor-relations/acquisitions/signalfx.html) for more information.
