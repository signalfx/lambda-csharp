# SignalFx .NET Lambda Wrapper

## Overview

You can use this document to add a SignalFx wrapper to your AWS Lambda for .NET.

The SignalFx C# Lambda Wrapper wraps around an AWS Lambda .NET function handler, which allows metrics and traces to be sent to SignalFx.

At a high-level, to add a SignalFx .NET Lambda wrapper, you can:
   * Package the code yourself; or
   * Use a Lambda layer containing the wrapper, and then attach the layer to a Lambda function.

To learn more about Lambda Layers, please visit the [AWS documentation site](https://docs.aws.amazon.com/lambda/latest/dg/configuration-layers.html).

## Step 1: Add the wrapper to the project

Add the [`signalfx-lambda-functions` NuGet package](https://www.nuget.org/packages/signalfx-lambda-functions/) to your project.

For advanced users who want to reduce the size of deployment packages, you can use the package as a developer dependency, but in production, you would add the wrapper to a layer in the Lambda environment. This option allows you to work with the wrapper in a local setting and reduce the size of deployment packages at the same time.

### Option 1: For AWS Serverless ASP.NET Core Lambda

In this option, perform the following steps:

1. Change the base class of your `LambdaEntryPoint` to the corresponding wrapper class:

| Original AWS Base Type | Wrapper Type |
| ----------------------- | ------------------------------------------------ |
| APIGatewayProxyFunction | APIGatewayProxyFunctionWrapper |
| APIGatewayHttpApiV2ProxyFunction | APIGatewayHttpApiV2ProxyFunctionWrapper |

2. Add the `TracingDecoratorFilter` to the filters of your ASP.NET Core application to enrich the span data. That is done by updating the `Startup.ConfigureServices` method, example:

```c#
        public void ConfigureServices(IServiceCollection services)
        {
            // Add the tracing decorator to enrich span data.
            services.AddControllers(config => config.Filters.Add(new TracingDecoratorFilter()));
        }
```

Check the sample projects below for working examples:

- [Sample ASP.NET Core AWS HttpApi V1](./src/SampleServerlessASPNETCore)
- [Sample ASP.NET Core AWS HttpApi V2](./src/SampleServerlessHttpApiV2ASPNETCore/)

### Option 2: AWS .NET Lambda function

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

Check the sample project for a working example:

- [Sample Lambda Functions](./src/SampleLambdaFunctions)

The are overloads of the `Invoke` method with different signatures to support the typical function signatures and specific ones to support API Gateway functions:

- `InvokeAPIGatewayProxyAsync`
- `InvokeAPIGatewayHttpApiV2ProxyAsync`

All of the methods above support enrichment of spans via optional parameters:

- `operationName` allows a custom and more friendly name for the span, if not specified defaults to the Lambda function name.
- `tags` lists extra key value pairs to be added to the span.

## Step 2: Locate your organization's realm and access token

Use the realm to configure the ingest endpoint. Follow these steps to locate your realm:

1. Open SignalFx and in the top, right corner, click your profile icon.
2. Click **My Profile**.
3. Next to **Organizations**, review the listed realm.

Use the access token to associate the data to your organization. Follow these steps to get an access token:

1. Open SignalFx and in the top, right corner, click your profile icon.
2. Click **My Profile**.
3. On the left panel click **Access Tokens**, select or create a new token as appropriate.

## Step 3: Set Lambda Function environment variables

Follow these steps to configure your access token and ingest endpoint:

1. Set `SIGNALFX_ACCESS_TOKEN` with your access token: 
    ```bash
     SIGNALFX_ACCESS_TOKEN=<access-token>
    ```

2. Set `SIGNALFX_ENDPOINT_URL` with your organization's realm:
    ```bash
     SIGNALFX_ENDPOINT_URL=https://ingest.<realm>.signalfx.com
    ```

If you're sending data to an OpenTelemetry Collector, you have to specify the full path like this: `http://<otel-collector-host>:9411/api/v2/spans`

3. (Optional) Globally enable/disable metrics and/or tracing by setting SIGNALFX_TRACING_ENABLED and/or SIGNALFX_METRICS_ENABLED.
    ```bash
    SIGNALFX_TRACING_ENABLED=true  [defaults to true]
    SIGNALFX_METRICS_ENABLED=false [defaults to false]
    ```

4. (Optional) Enable context propagation (currently only supports B3 propagation).
    ```bash
    SIGNALFX_CTX_PROPAGATION_ENABLED=true [defaults to true]
    ```

5. (Optional) Specify other environment variables to better configure the traces.
For a list of all the available configuration options available, see
[Configure the SignalFx Tracing Library for .NET](https://github.com/signalfx/signalfx-dotnet-tracing#configure-the-signalfx-tracing-library-for-net). 

## (Optional) Step 4: Reduce the size of deployment packages with AWS Lambda layers

Add a layer that includes the SignalFx Lambda wrapper to your Lambda function.
A layer is code and other content that you can run without including it in your deployment package.
SignalFx provides layers in all supported regions you can freely use.

Follow these steps to use the SignalFx .NET Lambda Wrapper layer.

1. Get the ARN layer according to the deployment region and .NET runtime of your Lambda:
    - [NetCoreApp 2.1](https://github.com/signalfx/lambda-layer-versions/blob/master/csharp/CSHARP-NetCoreApp21.md)
    - [NetCoreApp 3.1](https://github.com/signalfx/lambda-layer-versions/blob/master/csharp/CSHARP-NetCoreApp31.md)

2. Update the Lambda to use the layer:
    - Add the environment variable `DOTNET_SHARED_STORE` to `/opt/dotnetcore/store/` to Lambda configuration;
    - Explicitly set `framework` and `function-runtime` to ensure proper deployment;
    - Add the layer ARN from step 1 to the set of layers used by the Lambda;

All the updates above can be done using the Lambda configuration file
or the [AWS Extensions for .NET CLI](https://github.com/aws/aws-extensions-for-dotnet-cli#aws-extensions-for-net-cli).
The Examples below use `netcoreapp3.1` as the target framework but `netcoreapp2.1` is also supported.

- Using the `json` configuration file (by default named `aws-lambda-tools-defaults.json`):
    ```json
            "environment-variables" : "\"DOTNET_SHARED_STORE\"=\"/opt/dotnetcore/store/\"",
            "framework"             : "netcoreapp3.1",
            "function-runtime"      : "dotnetcore3.1",
            "function-layers"       : "<arn-from-step-1>",
    ```
- The `AWS Extensions for .NET CLI` CLI used to build and deploy your lambda by adding the parameter:
    ```terminal
            --environment-variables DOTNET_SHARED_STORE=/opt/dotnetcore/store/ 
            --framework netcoreapp3.1
            --function-runtime dotnetcore3.1
            --function-layers <arn-from-step-1>
    ```

If you want detailed information about using AWS Lambda Layers with .NET Core, please visit
[Layers for .NET Core Lambda Functions](https://github.com/aws/aws-extensions-for-dotnet-cli/blob/master/docs/Layers.md#layers-for-net-core-lambda-functions).

## Additional information and optional steps

### Metrics and dimensions sent by the wrapper

The Lambda wrapper sends the following metrics to SignalFx:

| Metric Name  | Type | Description |
| ------------- | ------------- | ---|
| function.invocations  | Counter  | Count number of Lambda invocations|
| function.cold_starts  | Counter  | Count number of cold starts|
| function.errors  | Counter  | Count number of errors from underlying Lambda handler|
| function.duration  | Gauge  | Milliseconds in execution time of underlying Lambda handler|

The Lambda wrapper adds the following dimensions to all data points sent to SignalFx:

| Dimension | Description |
| ------------- | ---|
| lambda_arn  | ARN of the Lambda function instance |
| aws_region  | AWS Region  |
| aws_account_id | AWS Account ID  |
| aws_function_name  | AWS Function Name |
| aws_function_version  | AWS Function Version |
| aws_function_qualifier  | AWS Function Version Qualifier (version or version alias if it is not an event source mapping Lambda invocation) |
| event_source_mappings  | AWS Function Name (if it is an event source mapping Lambda invocation) |
| aws_execution_env  | AWS execution environment (e.g. AWS_Lambda_dotnetcore3.1) |
| function_wrapper_version  | SignalFx function wrapper qualifier (e.g. signalfx_lambda_3.0.1.0) |
| metric_source | The literal value of 'lambda_wrapper' |

### Tags sent by the tracing wrapper 

The tracing wrapper creates a span for the wrapper handler. This span contains the following tags:

| Tag | Description |
|-|-|
| aws_request_id | AWS Request ID |
| lambda_arn | ARN of the Lambda function instance |
| aws_region | AWS region |
| aws_account_id | AWS account ID |
| aws_function_name | AWS function name |
| aws_function_version | AWS function version |
| aws_function_qualifier | AWS function version qualifier (version or version alias if it is not an event source mapping Lambda invocation) |
| event_source_mappings | AWS function name (if it is an event source mapping Lambda invocation) |
| aws_execution_env | AWS execution environment (e.g., AWS_Lambda_dotnetcore3.1) |
| function_wrapper_version | SignalFx function wrapper qualifier (e.g., ignalfx_lambda_3.0.1.0) |
| component | The literal value of 'dotnet-lambda-wrapper |

### Adding extra tags and enriching traces

There are several ways to add extra tags or enrich the traces of your service:

1. Where available use the optional parameters of the wrapper to pass extra tags via the `tags` parameter or update the span name via the `operationName` parameter.

2. For ASP.NET Core applications you can add custom action filters to add tags or various other operations available to manual instrumentation.
Use the [TracingDecoratorFilter](./src/SignalFx.LambdaWrapper/AspNetCoreServer/TracingDecoratorFilter.cs) as a starting point for your own implementation.

3. Use OpenTracing anywhere in your application to add tags, spans, or do context propagation as appropriate. Use the [SignalFx examples](https://github.com/signalfx/tracing-examples/tree/master/dotnet-manual-instrumentation) as a starting point.
Notice that no package needs to be added to the project since `signalfx-lambda-functions` already brings the OpenTracing library.

## Configuration settings for the legacy metrics wrapper

The configuration settings for the Metric Wrapper from version 2.0.1 are still supported. However, you should migrate to the latest version if possible.

| Legacy Environment Variable | Default Value / Comments |
| --------------------------- | ------------------------ |
| SIGNALFX_AUTH_TOKEN | No default, use SIGNALFX_ACCESS_TOKEN |
| SIGNALFX_API_HOSTNAME | Default is `ingest.us0.signalfx.com`. When you set `SIGNALFX_API_HOSTNAME`, you must reference your realm. |
| SIGNALFX_API_PORT | Default is `443` |
| SIGNALFX_API_SCHEME | Default is `https` |
| SIGNALFX_SEND_TIMEOUT | Default is `2000` |

3. Review optional parameters for ASP.Net Core Web API with Lambda:
    ```text
     CONNECTION_LEASE_TIMEOUT=milliseconds for connection lease timeout [5000]
     DNS_REFRESH_TIMEOUT=milliseconds for DNS refresh timeout [5000]
    ``` 

### Manually wrapp metrics with the legacy metrics wrapper

The legacy metrics wrapper offered the the following options. 

#### Option 1: Wrap the function manually

With this option, you will define a Lambda handler method and explicitly send metrics to SignalFx. To accomplish this, you will create a MetricWrapper with the context Wrap in your code with try-catch-finally and dispose of the wrapper finally.

Review the following example: 


```cs
using SignalFx.LambdaWrapper

...

public TOutput YourFunction(TInput input, ILambdaContext context)
{
    log.Info("C# HTTP trigger function processed a request.");
    MetricWrapper wrapper = new MetricWrapper(context);
    try { 
        ...
        // your code
        ...
        return ResponseObject
    } catch (Exception e) {
      wrapper.Error();
    } finally {
      wrapper.Dispose();
    }
}
```

#### Option 2: Use SignalFx function wrapper

With this option, you will extend `Amazon.Lambda.AspNetCoreServer.APIGatewayProxyFunction` and `SignalFx.LambdaWrapper.AspNetCoreServer.APIGatewayProxyFunctionWrapper` to send metrics to SignalFx. 

With this option, you will  **not** need to explicitly define the logic for sending telemetry.

Review the following example: 

```cs
public class LambdaEntryPoint : SignalFx.LambdaWrapper.AspNetCoreServer.APIGatewayProxyFunctionWrapper
{
    protected override void Init(IWebHostBuilder builder)
    {
      ...
    }
}
```

Please note that:
  * `SignalFx.LambdaWrapper.AspNetCoreServer.APIGatewayProxyFunctionWrapper` extends `Amazon.Lambda.AspNetCoreServer.APIGatewayProxyFunction`. 
  * The Lambda context object `Amazon.Lambda.Core.ILambdaContext` provides telemetry data that's not available in the Web API Controllers layer on down.

###  Option 3: Send custom metrics from the Lambda function  

1. This option is only available on versions prior to 3.0.0. Review the following example to understand how to send custom metrics from a defined Lambda handler when the Lambda context object is available:

    ```cs
    using com.signalfuse.metrics.protobuf;
    ...
    log.Info("C# HTTP trigger function processed a request.");
    MetricWrapper wrapper = new MetricWrapper(context);
    try { 
        ...
        // construct a data point
        DataPoint dp = new DataPoint();
    
        // use Datum to set the value
        Datum datum = new Datum();
        datum.intValue = 1;
    
        // set the name, value, and metric type on the datapoint
    
        dp.metric = "metric_name";
        dp.metricType = MetricType.GAUGE;
        dp.value = datum;
    
        // add custom dimension
        Dimension dim = new Dimension();
        dim.key = "applicationName";
        dim.value = "CoolApp";
        dp.dimensions.Add(dim);
    
        // send the metric
        // on version 3.0.1 and above use wrapper.AddDataPoint(dp);
        MetricSender.sendMetric(dp);
        ...
        return ResponseObject
    } catch (Exception e) {
      wrapper.Error();
    } finally {
      wrapper.Dispose();
    }
    ```

2. Review the following example to understand how to send custom metrics in the Web API Controller Layer on down for `ASP.Net Core Web API with Lambda` implementations when the Lambda context object is **not** available. In short, the SignalFx C# Lambda Wrapper provides a `SignalFx.LambdaWrapper.AspNetCoreServer.Extensions.AddMetricDataPoint()` extension method for  `Microsoft.AspNetCoreServer.Http.HttpResponse` type to export metric datapoints to SignalFx. 

    ```cs
    ...
    using com.signalfuse.metrics.protobuf;
    using SignalFx.LambdaWrapper.AspNetCoreServer.Extensions;
    ...
    [HttpGet]
    public IEnumerable<string> Get()
    {
        ...
        [HttpGet]
        public IEnumerable<string> Get()
        {
            ...
            DataPoint dataPoint = new DataPoint
            {
                metric = "mycontroller.get.invokes",
                metricType = MetricType.COUNTER,
                value = new Datum
                {
                    intValue = 1
                }
            };
            var response = Request.HttpContext.Response;
            response.AddMetricDataPoint(dataPoint);
            ...
        }
        ...
    ```

### Test locally
1. Review the following document from AWS: [Introducing AWS SAM Local, a CLI Tool to Test AWS Lambda Functions Locally](https://aws.amazon.com/about-aws/whats-new/2017/08/introducing-aws-sam-local-a-cli-tool-to-test-aws-lambda-functions-locally/).

2. Install the NuGet package (from Step 1 in the installation section), and then run as normal.  

## License

Apache Software License v2. Copyright © 2014-2020 Splunk
