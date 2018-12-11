using System;
using System.Collections.Generic;
using com.signalfuse.metrics.protobuf;
using Microsoft.AspNetCore.Http;
using System.Linq;
using Amazon.Lambda.Core;
using Microsoft.AspNetCore.Http.Features;
using Newtonsoft.Json;
using Amazon.Lambda.APIGatewayEvents;

namespace SignalFx.LambdaWrapper.Extensions
{
    public static class WrapperExtensions
    {
        private static readonly string WrapperVersion = "0.1.0";
        private static readonly string CustomMetricPrefix = "sfx_metric_datapoint-";

        /// <summary>
        /// This HttpResponse extension method is for sending custom metric datapoints from within controller methods. This method adds a custom metric datapoint to the response headers.
        /// </summary>
        /// <param name="httpResponse"></param>
        /// <param name="dataPoint"></param>
        public static void AddMetricDataPoint(this HttpResponse httpResponse, DataPoint dataPoint)
        {
            if (dataPoint == null)
            {
                LambdaLogger.Log($"[Error] adding metric to response. Argument {nameof(dataPoint)} of method {nameof(WrapperExtensions.AddMetricDataPoint)} cannot be null.{Environment.NewLine}");
                return;
            }
            if (string.IsNullOrWhiteSpace(dataPoint.metric))
            {
                LambdaLogger.Log($"[Error] adding metric to response. Property {nameof(dataPoint.metric)} of argument {nameof(dataPoint)} of method {nameof(WrapperExtensions.AddMetricDataPoint)} cannot be null or whitespace.{Environment.NewLine}");
                return;
            }
            // Unique header key per serialized datapoint.
            string headerKey = CustomMetricPrefix + Guid.NewGuid();
            httpResponse.Headers.Append(headerKey, JsonConvert.SerializeObject(dataPoint));
        }

        // This IHttpResponseFeature extension method gets custom metric datapoints from the headers of a IHttpResponseFeature object.
        internal static List<DataPoint> GetCustomMetricDataPoints(this IHttpResponseFeature responseFeature)
        {
            var dataPoints = from metricDataPointHeader in
                                (from header in responseFeature.Headers
                                 where header.Key.StartsWith(CustomMetricPrefix, StringComparison.Ordinal)
                                 select header)
                             select JsonConvert.DeserializeObject<DataPoint>(metricDataPointHeader.Value);
            return dataPoints.ToList();
        }

        // This IHttpResponseFeature extension method removes custom metric datapoints headers from a IHttpResponseFeature object.
        internal static void RemoveCustomMetricDataPointHeaders(this IHttpResponseFeature responseFeature)
        {
            var metricDataPointHeaders = from header in responseFeature.Headers
                                where header.Key.StartsWith(CustomMetricPrefix, StringComparison.Ordinal)
                                select header;
            foreach (var metricDataPointHeader in metricDataPointHeaders.ToList())
            {
                responseFeature.Headers.Remove(metricDataPointHeader.Key);
            }
        }

        // AddDefaultDimensions adds metric dimensions derived from AWS Lambda ARN to datapoint. Formats and examples of AWS Lambda ARNs are in the
        // AWS Lambda (Lambda) section at https://docs.aws.amazon.com/general/latest/gr/aws-arns-and-namespaces.html#arn-syntax-lambda
        internal static void AddDefaultDimensions(this DataPoint dataPoint, ILambdaContext lambdaContext)
        {
            string[] arnSubstrings = lambdaContext.InvokedFunctionArn.Split(':');
            dataPoint.dimensions.Add(new Dimension { key = "aws_function_name", value = lambdaContext.FunctionName });
            dataPoint.dimensions.Add(new Dimension { key = "aws_function_version", value = lambdaContext.FunctionVersion });
            dataPoint.dimensions.Add(new Dimension { key = "metric_source", value = "lambda_wrapper" });
          
            if (arnSubstrings.Length > 3)
            {
                dataPoint.dimensions.Add(new Dimension { key = "aws_region", value = arnSubstrings[3] });
            }
            if (arnSubstrings.Length > 4)
            {
                dataPoint.dimensions.Add(new Dimension { key = "aws_account_id", value = arnSubstrings[4] });
            }
            if (arnSubstrings.Length > 5 && "function".Equals(arnSubstrings[5]))
            {
                if (arnSubstrings.Length == 8)
                {
                    dataPoint.dimensions.Add(new Dimension { key = "aws_function_qualifier", value = arnSubstrings[7] });
                }
                string[] updatedArn = new string[8];
                Array.Copy(arnSubstrings, 0, updatedArn, 0, arnSubstrings.Length);
                updatedArn[7] = lambdaContext.FunctionVersion;
                dataPoint.dimensions.Add(new Dimension { key = "lambda_arn", value = String.Join(":", updatedArn) });
            }
            else if (arnSubstrings.Length > 6 && "event-source-mappings".Equals(arnSubstrings[5]))
            {
                dataPoint.dimensions.Add(new Dimension { key = "event_source_mappings", value = arnSubstrings[6] });
                dataPoint.dimensions.Add(new Dimension { key = "lambda_arn", value = lambdaContext.InvokedFunctionArn });
            }

            string awsExecutionEnvironment = Environment.GetEnvironmentVariable("AWS_EXECUTION_ENVIRONMENT");
            if (!string.IsNullOrEmpty(awsExecutionEnvironment))
            {
                dataPoint.dimensions.Add(new Dimension { key = "aws_execution_env", value = awsExecutionEnvironment });
            }
            dataPoint.dimensions.Add(new Dimension { key = "function_wrapper_version", value = WrapperVersion });
        }

        internal static bool IsSuccessStatusCode(this APIGatewayProxyResponse response)
        {
            return (response.StatusCode >= 200 && response.StatusCode <= 299);
        }

    }
}
