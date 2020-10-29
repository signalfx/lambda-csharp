# SignalFx .NET Lambda Wrapper

## Overview

You can use this document to add a SignalFx wrapper to your AWS Lambda for C#.

The SignalFx C# Lambda Wrapper wraps around an AWS Lambda C# function handler, which allows metrics
and trces to be sent to SignalFx.

## Step 1: Install via NuGet

1. Add the following package reference to `.csproj` or `function.proj`:
```xml
  <PackageReference Include="signalfx-lambda-functions" Version="2.0.2"/>
```

The `signalfx-lambda-functions` has the following package dependencies; your package manager should add these package dependencies automatically to your project:

  * `Amazon.Lambda` 
  * `Amazon.Lambda.AspNetCoreServer` 
  * `protobuf-net` 
    
2. Verify that `protobut-net` has been added. If `protobut-net` is missing, then reference the `protobut-net` package as a dependency in your project, similar to Step 1. 

## Step 2: Locate the ingest endpoint

TODO: Review this after implementation.

By default, this function wrapper will send data to the us0 realm. As a result, if you are not in the us0 realm and you want to use the ingest endpoint directly, then you must explicitly set your realm. 

To locate your realm:

1. Open SignalFx and in the top, right corner, click your profile icon.
2. Click **My Profile**.
3. Next to **Organizations**, review the listed realm.

To set your realm, use a subdomain, such as ingest.us1.signalfx.com or ingest.eu0.signalfx.com. You will use the realm subdomain to set `SIGNALFX_API_HOSTNAME` variable in the next step.

## Step 3: Set Lambda function environment variables

1. Set authentication token:
    ```text
     SIGNALFX_AUTH_TOKEN=signalfx token
    ```
2. Review optional parameters: 
    ```text
     SIGNALFX_API_HOSTNAME=[ingest.us0.signalfx.com]
     SIGNALFX_API_PORT=[443]
     SIGNALFX_API_SCHEME=[https]
     SIGNALFX_SEND_TIMEOUT=milliseconds for signalfx client timeout [2000]
    ```
    (When you set `SIGNALFX_API_HOSTNAME`, you must reference your correct realm from Step 2.)

3. Review optional parameters for ASP.Net Core Web API with Lambda:
    ```text
     CONNECTION_LEASE_TIMEOUT=milliseconds for connection lease timeout [5000]
     DNS_REFRESH_TIMEOUT=milliseconds for DNS refresh timeout [5000]
    ``` 

## Step 4: Wrap the function

To wrap the function, review the following options. 

### Option 1: Wrap the function manually
TODO: Option 1 vs 2 depends more on the user's code: if using API Gateway or function handler.

With this option, you will define a Lambda handler method and explicitly send metrics to SignalFx. To accomplish this, you will create a MetricWrapper with the ExecutionContext Wrap in your code with try-catch-finally and dispose of the wrapper finally.

Review the following example: 


```cs
using SignalFx.LambdaWrapper

...

// TODO: The code below is actually an Az Function not an AWS Lambda. Although the code itself is pretty similar.
[FunctionName("HttpTrigger")]
public static IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, TraceWriter log, ExecutionContext context)
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

### Option 2: Use SignalFx FunctionWrapper

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
  * The Lambda context object `Amazon.Lambda.Core.ILambdaContext` provides telemetry data that is not available in the Web API Controllers layer on down.

## (Optional) Step 5: Send custom metrics from the Lambda function 
TODO: Add reference to manual tracing.

1. Review the following example to understand how to send custom metrics from a defined Lambda handler when the Lambda context object is available:

    ```cs
    using com.signalfuse.metrics.protobuf;
    
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
    MetricSender.sendMetric(dp);
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
## (Optional) Step 6: Reduce the size of deployment packages with AWS Lambda Layers
TODO: Give shorter instructions directly here and use links below as further info.

1. For advanced users who want to reduce the size of deployment packages, please visit the AWS documentation site and see [AWS Lambda Layers](https://docs.aws.amazon.com/lambda/latest/dg/configuration-layers.html).

2. SignalFx hosts layers containing this wrapper in most AWS regions. To review the latest available version for your region, see [the list of versions](https://github.com/signalfx/lambda-layer-versions/blob/master/csharp/CSHARP.md).

3. After you locate the appropriate layer version, please visit the AWS documentation site and follow [AWS instructions to deploy .NET Core Lambda with the layer](https://aws.amazon.com/blogs/developer/aws-lambda-layers-with-net-core/).

## Additional information and optional steps

### Metrics and dimensions sent by the wrapper
TODO: Double-check the name of metrics.

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
| aws_execution_env  | AWS execution environment (e.g. AWS_Lambda_java8) |
| function_wrapper_version  | SignalFx function wrapper qualifier (e.g. signalfx-lambda-0.0.5) |
| metric_source | The literal value of 'lambda_wrapper' |


### Test locally
1. Review the following document from AWS: [Introducing AWS SAM Local, a CLI Tool to Test AWS Lambda Functions Locally](https://aws.amazon.com/about-aws/whats-new/2017/08/introducing-aws-sam-local-a-cli-tool-to-test-aws-lambda-functions-locally/).

2. Install the NuGet package (from Step 1 in the installation section), and then run as normal.  

## License

Apache Software License v2. Copyright © 2014-2020 Splunk
