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
    public abstract class APIGatewayProxyFunctionWrapper : APIGatewayProxyFunction
    {
        protected HttpClientWrapper HttpClientWrapper { get; set; } = new HttpClientWrapper();
        private bool isColdStart = true;

        [LambdaSerializer(typeof(JsonSerializer))]
        public override async Task<APIGatewayProxyResponse> FunctionHandlerAsync(APIGatewayProxyRequest request, ILambdaContext lambdaContext)
        {
            APIGatewayProxyResponse apiGatewayProxyResponse = null;
            var dataPoints = new List<DataPoint>();
            try
            {
                dataPoints.Add(NewCounterDatapoint("function.invocations", lambdaContext));
                if (isColdStart)
                {
                    dataPoints.Add(NewCounterDatapoint("function.cold_starts", lambdaContext));
                    isColdStart = false;
                }
                var watch = Stopwatch.StartNew();
                apiGatewayProxyResponse = await base.FunctionHandlerAsync(request, lambdaContext);
                watch.Stop();
                dataPoints.Add(NewGaugeDatapoint("function.duration", watch.Elapsed.TotalSeconds, lambdaContext));
                if (!apiGatewayProxyResponse.IsSuccessStatusCode())
                {
                    dataPoints.Add(NewCounterDatapoint("function.errors", lambdaContext));
                    LambdaLogger.Log($"[Error] posting metric datapoints. Http status code: {apiGatewayProxyResponse.StatusCode}. Response body: {apiGatewayProxyResponse.Body}{Environment.NewLine}");
                }
            }
            catch (Exception exception)
            {
                dataPoints.Add(NewCounterDatapoint("function.errors", lambdaContext));
                LambdaLogger.Log($"[Error] invoking lambda function.{Environment.NewLine}{exception.ToString()}{Environment.NewLine}");
            }
            finally
            {
                PostDataPoints(dataPoints);
            }
            return apiGatewayProxyResponse;
        }

        protected override void PostMarshallResponseFeature(IHttpResponseFeature aspNetCoreResponseFeature, APIGatewayProxyResponse apiGatewayResponse, ILambdaContext lambdaContext)
        {
            var dataPoints = aspNetCoreResponseFeature.GetCustomMetricDataPoints(lambdaContext);
            foreach (var dataPoint in dataPoints)
            {
                dataPoint.AddDefaultDimensions(lambdaContext);
            }
            PostDataPoints(dataPoints);
            aspNetCoreResponseFeature.RemoveCustomMetricDataPointHeaders();

        }

        private DataPoint NewCounterDatapoint(string metricName, ILambdaContext lambdaContext) {
            var dataPoint = new DataPoint
            {
                metric = metricName,
                value = new Datum { intValue = 1 },
                metricType = MetricType.COUNTER
            };
            dataPoint.AddDefaultDimensions(lambdaContext);
            return dataPoint;
        }

        private DataPoint NewGaugeDatapoint(string metricName, double value, ILambdaContext lambdaContext)
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
            using (var httpResponseMessage = HttpClientWrapper.PostDataPointsAsync(dataPoints).Result)
            {
                if (!httpResponseMessage.IsSuccessStatusCode)
                {
                    var content = httpResponseMessage.Content.ReadAsStringAsync().Result;
                    LambdaLogger.Log($"[Error] posting metric datapoints. Http status code: {(int)httpResponseMessage.StatusCode}. Response content: {content}{Environment.NewLine}");
                }
            }
        }
    }
}
