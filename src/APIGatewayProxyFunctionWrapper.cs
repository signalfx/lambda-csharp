using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.Json;
using Microsoft.AspNetCore.Http.Features;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Lambda.AspNetCoreServer;
using com.signalfuse.metrics.protobuf;
using SignalFx.LambdaWrapper.Extensions;
using System.Diagnostics;

namespace SignalFx.LambdaWrapper
{
    /// <inheritdoc />
    /// <summary>
    /// APIGatewayProxyFunctionWrapper extends ApiGatewayProxyFunction to provide monitoring functionality. Users should
    /// extend APIGatewayProxyFunctionWrapper and implement the Init method similar to Main function in the ASP.NET Core.
    /// The function handler for the Lambda function will point to the APIGatewayProxyFunctionWrapper class FunctionHandlerAsync
    /// method.
    /// </summary>
    public abstract class APIGatewayProxyFunctionWrapper : APIGatewayProxyFunction
    {
        private readonly HttpClientWrapper _httpClientWrapper = new HttpClientWrapper();
        private bool _isColdStart = true;

        /// <inheritdoc />
        /// <summary>
        /// This overriding method is what the Lambda function handler points to. This method posts defaults metric datapoints to SignalFx
        /// and delegates to the overriden base method.
        /// </summary>
        [LambdaSerializer(typeof(JsonSerializer))]
        public override async Task<APIGatewayProxyResponse> FunctionHandlerAsync(APIGatewayProxyRequest request, ILambdaContext lambdaContext)
        {
            APIGatewayProxyResponse apiGatewayProxyResponse = null;
            var dataPoints = new List<DataPoint>();
            try
            {
                dataPoints.Add(NewDefaultCounterDataPoint("function.invocations", lambdaContext));
                if (_isColdStart)
                {
                    dataPoints.Add(NewDefaultCounterDataPoint("function.cold_starts", lambdaContext));
                    _isColdStart = false;
                }
                var watch = Stopwatch.StartNew();
                apiGatewayProxyResponse = await base.FunctionHandlerAsync(request, lambdaContext);
                watch.Stop();
                dataPoints.Add(NewDefaultGaugeDataPoint("function.duration", watch.Elapsed.TotalSeconds, lambdaContext));
                if (!apiGatewayProxyResponse.IsSuccessStatusCode())
                {
                    dataPoints.Add(NewDefaultCounterDataPoint("function.errors", lambdaContext));
                    LambdaLogger.Log($"[Error] invoking lambda function. Http status code: {apiGatewayProxyResponse.StatusCode}. Response body: {apiGatewayProxyResponse.Body}{Environment.NewLine}");
                }
            }
            catch (Exception exception)
            {
                dataPoints.Add(NewDefaultCounterDataPoint("function.errors", lambdaContext));
                LambdaLogger.Log($"[Error] invoking lambda function.{Environment.NewLine}{exception}{Environment.NewLine}");
            }
            finally
            {
                PostDataPoints(dataPoints);
            }
            return apiGatewayProxyResponse;
        }

        /// <inheritdoc />
        /// <summary>
        /// This overriding method is called after the APIGatewayProxyFunction has marshaled IHttpResponseFeature that came
        /// back from making the request into ASP.NET Core into API Gateway's response object APIGatewayProxyResponse. It gets the
        /// user-defined custom metric datapoints from the response headers, posts them to SignalFx and removes the headers.
        /// </summary>
        protected override void PostMarshallResponseFeature(IHttpResponseFeature aspNetCoreResponseFeature, APIGatewayProxyResponse apiGatewayResponse, ILambdaContext lambdaContext)
        {
            var dataPoints = aspNetCoreResponseFeature.GetCustomMetricDataPoints();
            foreach (var dataPoint in dataPoints)
            {
                dataPoint.AddDefaultDimensions(lambdaContext);
            }
            PostDataPoints(dataPoints);
            aspNetCoreResponseFeature.RemoveCustomMetricDataPointHeaders();
        }

        private static DataPoint NewDefaultCounterDataPoint(string metricName, ILambdaContext lambdaContext) {
            var dataPoint = new DataPoint
            {
                metric = metricName,
                value = new Datum { intValue = 1 },
                metricType = MetricType.COUNTER
            };
            dataPoint.AddDefaultDimensions(lambdaContext);
            return dataPoint;
        }

        private static DataPoint NewDefaultGaugeDataPoint(string metricName, double value, ILambdaContext lambdaContext)
        {
            var dataPoint = new DataPoint
            {
                metric = metricName,
                value = new Datum { doubleValue = value },
                metricType = MetricType.GAUGE
            };
            dataPoint.AddDefaultDimensions(lambdaContext);
            return dataPoint;
        }

        private void PostDataPoints(IEnumerable<DataPoint> dataPoints)
        {
            using (var httpResponseMessage = _httpClientWrapper.PostDataPointsAsync(dataPoints).Result)
            {
                if (!httpResponseMessage.IsSuccessStatusCode)
                {
                    var content = httpResponseMessage.Content.ReadAsStringAsync().Result;
                    LambdaLogger.Log($"[Error] posting metric datapoints. Http status code: {(int)httpResponseMessage.StatusCode}. Response content: {content}{Environment.NewLine}");
                }
                else
                {
                    LambdaLogger.Log("[Information] success posting metric datapoints.");
                }
            }
        }

    }

}
