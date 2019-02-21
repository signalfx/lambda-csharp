# SignalFx C# Lambda Wrapper

SignalFx C# Lambda Wrapper.

## Usage

The SignalFx C# Lambda Wrapper is a wrapper around an Lambda Function, used to instrument execution of the function and send metrics to SignalFx.

### Install via NuGet
Add the following package reference to your `.csproj` or `function.proj`
```xml
  <PackageReference Include="signalfx-lambda-functions" Version="2.0.1"/>
```
NOTE: The `signalfx-lambda-functions` package depends on packages `Amazon.Lambda`, `Amazon.Lambda.AspNetCoreServer` and `protobuf-net`. Your package manager should add these transitive dependencies automatically to your project. This negates the need to add them explicitly. However, `protobut-net` has be reported missing on occasion by users. If this happens, reference the `protobut-net` package as a dependency in your project using a statement similar to the one above. See this project's `.csproj` file for details about the version of `protobuf-net` required.

### Using the Wrapper

#### Configuring the ingest endpoint

By default, this function wrapper will send to the `us0` realm. If you are
not in this realm you will need to set the `SIGNALFX_API_HOSTNAME` environment
variable to the correct realm ingest endpoint (https://ingest.{REALM}.signalfx.com).
To determine what realm you are in, check your profile page in the SignalFx
web application (click the avatar in the upper right and click My Profile).

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

### Environment Variable
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
3 ) Additional optional parameters for ASP.Net Core Web API with Lambda:
```text
 CONNECTION_LEASE_TIMEOUT=milliseconds for connection lease timeout [5000]
 DNS_REFRESH_TIMEOUT=milliseconds for DNS refresh timeout [5000]
``` 

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

### Sending custom metric

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

### Testing locally.
1) Follow the Lambda instructions to run functions locally https://aws.amazon.com/about-aws/whats-new/2017/08/introducing-aws-sam-local-a-cli-tool-to-test-aws-lambda-functions-locally/

2) Install the NuGet package above, run as normal.


## License

Apache Software License v2. Copyright © 2014-2017 SignalFx
