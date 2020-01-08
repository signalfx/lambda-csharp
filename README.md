# SignalFx .NET Lambda Wrapper

SignalFx .NET Lambda Wrapper.

## Overview

The SignalFx .NET Lambda Wrapper is a wrapper around an Lambda Function, used to instrument execution of the function and send metrics to SignalFx.

### Step 1: Install via NuGet
Add the following package reference to your `.csproj` or `function.proj`
```xml
  <PackageReference Include="signalfx-lambda-functions" Version="2.0.1"/>
```
NOTE: The `signalfx-lambda-functions` package depends on packages `Amazon.Lambda`, `Amazon.Lambda.AspNetCoreServer` and `protobuf-net`. Your package manager should add these transitive dependencies automatically to your project. This negates the need to add them explicitly. However, `protobut-net` has be reported missing on occasion by users. If this happens, reference the `protobut-net` package as a dependency in your project using a statement similar to the one above. See this project's `.csproj` file for details about the version of `protobuf-net` required.

### Step 2: Locate ingest endpoint

By default, this function wrapper will send data to the us0 realm. As a result, if you are not in us0 realm and you want to use the ingest endpoint directly, then you must explicitly set your realm. To set your realm, use a subdomain, such as ingest.us1.signalfx.com or ingest.eu0.signalfx.com.

To locate your realm:

1. Open SignalFx and in the top, right corner, click your profile icon.
2. Click **My Profile**.
3. Next to **Organizations**, review the listed realm.

### Step 3: Set environment variables

Set the Lambda Function environment variables as follows:

1 ) Set authentication token:
```text
 SIGNALFX_AUTH_TOKEN=signalfx token
```
2 ) Optional parameters available:
```text
 SIGNALFX_API_HOSTNAME=[pops.signalfx.com]
 SIGNALFX_API_PORT=[443]
 SIGNALFX_API_SCHEME=[https]
 SIGNALFX_SEND_TIMEOUT=milliseconds for signalfx client timeout [2000]
```
When setting SIGNALFX_API_HOSTNAME, remember to account for your realm, as explained in Step 2.

3 ) Additional optional parameters for ASP.Net Core Web API with Lambda:
```text
 CONNECTION_LEASE_TIMEOUT=milliseconds for connection lease timeout [5000]
 DNS_REFRESH_TIMEOUT=milliseconds for DNS refresh timeout [5000]
``` 

### Step 4: Wrap the function

#### Option 1) Wrap the function manually
In the common Lambda implementation where you are required to define a Lambda handler method directly, you explicitly send metrics to SignalFx by creating a MetricWrapper with the ExecutionContext Wrap your code in try-catch-finally, disposing of the wrapper finally.
```cs
using SignalFx.LambdaWrapper

...

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

#### Option 2) Use SignalFx FunctionWrapper
 In the `ASP.Net Core Web API with Lambda` implementation where you do not define the Lambda handler method directly but rather extend `Amazon.Lambda.AspNetCoreServer.APIGatewayProxyFunction`, you simply extend `SignalFx.LambdaWrapper.AspNetCoreServer.APIGatewayProxyFunctionWrapper` instead in order to send metrics to SignalFx. No need to explicitly define logic for sending metrics.
```cs
public class LambdaEntryPoint : SignalFx.LambdaWrapper.AspNetCoreServer.APIGatewayProxyFunctionWrapper
{
    protected override void Init(IWebHostBuilder builder)
    {
      ...
    }
}
```
Note that `SignalFx.LambdaWrapper.AspNetCoreServer.APIGatewayProxyFunctionWrapper` extends `Amazon.Lambda.AspNetCoreServer.APIGatewayProxyFunction`. Also, the lambda context object `Amazon.Lambda.Core.ILambdaContext` from which default metric dimensions are derived is not available in the Web API Controllers layer on down.

### Step 5: Send custom metric from the Lambda function (optional)

Below is an example of sending user-defined metrics (i.e. custom metrics) from withing a directly defined Lambda handler method. The lambda context object is available.

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

Sending custom metric in the Web API Controller layer on down for `ASP.Net Core Web API with Lambda` implementations is curtailed by the fact that the lambda context object is not available. However, the SignalFx C# Lambda Wrapper provides extension method `SignalFx.LambdaWrapper.AspNetCoreServer.Extensions.AddMetricDataPoint()` for type `Microsoft.AspNetCoreServer.Http.HttpResponse` for exporting metric datapoints to SignalFx. Below is an example of sending metrics from within a Web API Controller method.

```cs
...
using com.signalfuse.metrics.protobuf;
using SignalFx.LambdaWrapper.AspNetCoreServer.Extensions;
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
### Step 6: For advanced users - reducing size of the deployment package with AWS Lambda Layers (optional) 
You can reduce size of your deployment package by taking advantage of AWS Lambda Layers feature.
To learn more about Lambda Layers, please visit the AWS documentation site and see [AWS Lambda Layers](https://docs.aws.amazon.com/lambda/latest/dg/configuration-layers.html).

SignalFx hosts layers containing this wrapper in most of the AWS regions.
To check what is the latest version available in the region of your interest, see [the list of versions](https://github.com/signalfx/lambda-layer-versions/blob/master/csharp/CSHARP.md).

After you located appropriate layer version, follow [AWS instructions to deploy .NET Core Lambda with the layer](https://aws.amazon.com/blogs/developer/aws-lambda-layers-with-net-core/).

## Additional information

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
| aws_execution_env  | AWS execution environment (e.g. AWS_Lambda_java8) |
| function_wrapper_version  | SignalFx function wrapper qualifier (e.g. signalfx-lambda-0.0.5) |
| metric_source | The literal value of 'lambda_wrapper' |


### Testing locally
1) Follow the Lambda instructions to run functions locally https://aws.amazon.com/about-aws/whats-new/2017/08/introducing-aws-sam-local-a-cli-tool-to-test-aws-lambda-functions-locally/

2) Install the NuGet package above, run as normal.

## License

Apache Software License v2. Copyright © 2014-2020 SignalFx
