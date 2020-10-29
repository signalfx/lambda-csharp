using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.AspNetCoreServer;
using Amazon.Lambda.Core;
using com.signalfuse.metrics.protobuf;
using Microsoft.AspNetCore.Http.Features;

namespace SignalFx.LambdaWrapper.AspNetCoreServer
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
        private static readonly FunctionWrapper s_functionWrapper = new FunctionWrapper();

        private readonly HttpClientWrapper _httpClientWrapper = new HttpClientWrapper();

        /// <inheritdoc />
        /// <summary>
        /// This overriding method is what the Lambda function handler points to.
        /// </summary>
        public override async Task<APIGatewayProxyResponse> FunctionHandlerAsync(APIGatewayProxyRequest request, ILambdaContext lambdaContext)
        {
            if (!TelemetryConfiguration.TracingEnabled && !TelemetryConfiguration.MetricsEnabled)
            {
                return await base.FunctionHandlerAsync(request, lambdaContext).ConfigureAwait(false);
            }

            return await s_functionWrapper.InvokeAPIGatewayProxyAsync(
                base.FunctionHandlerAsync,
                request,
                lambdaContext).ConfigureAwait(false);
        }

        /// <inheritdoc />
        /// <summary>
        /// This overriding method is called after the APIGatewayProxyFunction has marshaled IHttpResponseFeature that came
        /// back from making the request into ASP.NET Core into API Gateway's response object APIGatewayProxyResponse. It gets the
        /// user-defined custom metric datapoints from the response headers, posts them to SignalFx and removes the headers.
        /// </summary>
        protected override void PostMarshallResponseFeature(IHttpResponseFeature aspNetCoreResponseFeature, APIGatewayProxyResponse apiGatewayResponse, ILambdaContext lambdaContext)
        {
            if (!TelemetryConfiguration.MetricsEnabled)
            {
                return;
            }

            var dataPoints = aspNetCoreResponseFeature.ExtractCustomMetricDataPoints();
            if (dataPoints == null)
            {
                return;
            }

            foreach (var dataPoint in dataPoints)
            {
                dataPoint.AddDefaultDimensions(lambdaContext);
            }

            PostDataPoints(dataPoints);
        }

        // TODO: Still used via PostMarshallResponseFeature consolidate with the one used by MetricsWrapper.
        private void PostDataPoints(IEnumerable<DataPoint> dataPoints)
        {
            using (var httpResponseMessage = _httpClientWrapper.PostDataPointsAsync(dataPoints).Result)
            {
                if (httpResponseMessage.IsSuccessStatusCode) return;
                var content = httpResponseMessage.Content.ReadAsStringAsync().Result;
                LambdaLogger.Log($"[Error] {typeof(APIGatewayProxyFunctionWrapper).FullName}: posting metric datapoints. Http status code: {(int)httpResponseMessage.StatusCode}. Response content: {content}{Environment.NewLine}");
            }
        }
    }
}
