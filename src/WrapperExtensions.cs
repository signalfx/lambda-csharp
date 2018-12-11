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
        private static readonly string CustomMetricPrefix = "sfx_metric-";

        public static void AddMetric(this HttpResponse httpResponse, DataPoint dataPoint)
        {
            if (dataPoint == null)
            {
                LambdaLogger.Log($"[Error] adding metric to response. Argument {nameof(dataPoint)} of method {nameof(WrapperExtensions.AddMetric)} cannot be null.\n");
                return;
            }
            if (string.IsNullOrWhiteSpace(dataPoint.metric))
            {
                LambdaLogger.Log($"[Error] adding metric to response. Property {nameof(dataPoint.metric)} of argument {nameof(dataPoint)} of method {nameof(WrapperExtensions.AddMetric)} cannot be null or whitespace.\n");
                return;
            }
            string headerKey = CustomMetricPrefix + Guid.NewGuid();
            //string prefixedIntValue    = dataPoint.value.intValueSpecified    ? "1" + dataPoint.value.intValue    : "0";
            //string prefixedDoubleValue = dataPoint.value.doubleValueSpecified ? "1" + dataPoint.value.doubleValue : "0";
            //string prefixedStrValue    = dataPoint.value.strValueSpecified    ? "1" + dataPoint.value.strValue    : "0";
            //string prefixedMetricType  = dataPoint.metricTypeSpecified        ? "1" + dataPoint.metricType        : "0";
            //string prefixedSource      = dataPoint.sourceSpecified            ? "1" + dataPoint.source            : "0";
            //string prefixedTimestamp   = dataPoint.timestampSpecified         ? "1" + dataPoint.timestamp         : "0";

            //// memory_usage-150-166.67-1high-1Counter-1host-1666666
            //string headerValue = Regex.Replace(dataPoint.metric,    "-", @"\-") + "-" + 
            //                     Regex.Replace(prefixedIntValue,    "-", @"\-") + "-" + 
            //                     Regex.Replace(prefixedDoubleValue, "-", @"\-") + "-" + 
            //                     Regex.Replace(prefixedStrValue,    "-", @"\-") + "-" + 
            //                     Regex.Replace(prefixedMetricType,  "-", @"\-") + "-" + 
            //                     Regex.Replace(prefixedSource,      "-", @"\-") + "-" + 
            //                     Regex.Replace(prefixedTimestamp,   "-", @"\-");
            //headerValue = Regex.Replace(headerValue, ",", @"\,");
            //httpResponse.Headers.Append(headerKey, JsonConvert.SerializeObject(dataPoint));
            //if (dataPoint.dimensions.Count != 0)
            //{
            //    httpResponse.Headers.AppendCommaSeparatedValues(headerKey, dataPoint.dimensions.Select(dimension => dimension.key + "=" + dimension.value).ToArray());
            //}
            httpResponse.Headers.Append(headerKey, JsonConvert.SerializeObject(dataPoint));
        }

        internal static IEnumerable<DataPoint> GetCustomMetricDataPoints(this IHttpResponseFeature responseFeature, ILambdaContext lambdaContext)
        {
            var dataPoints = from metricDataPointHeader in
                                (from header in responseFeature.Headers
                                 where header.Key.StartsWith(CustomMetricPrefix, StringComparison.Ordinal)
                                 select header)
                             select JsonConvert.DeserializeObject<DataPoint>(metricDataPointHeader.Value);
            //select NewCustomMetricDataPoint(lambdaContext, metricHeader.Key, responseFeature.Headers.GetCommaSeparatedValues(metricHeader.Key));
            foreach (var dataPoint in dataPoints)
            {
                dataPoint.AddDefaultDimensions(lambdaContext);
            }
            return dataPoints;
        }

        internal static void RemoveCustomMetricDataPointHeaders(this IHttpResponseFeature responseFeature)
        {
            var metricDataPointHeaders = from header in responseFeature.Headers
                                where header.Key.StartsWith(CustomMetricPrefix, StringComparison.Ordinal)
                                select header;
            foreach (var metricDataPointHeader in metricDataPointHeaders)
            {
                responseFeature.Headers.Remove(metricDataPointHeader.Key);
            }
        }

        //private static DataPoint NewCustomMetricDataPoint(ILambdaContext lambdaContext, String metricHeaderKey, string[] dimensionStrings)
        //{
        //    // 
        //    var splits = Regex.Split(metricHeaderKey.Substring(CustomMetricPrefix.Length), @"(?<!%)-");
        //    DataPoint dataPoint = new DataPoint
        //    {
        //        metric = splits[0],
        //        value = NewDatum(splits[1]),
        //        metricType = (MetricType)Enum.Parse(typeof(MetricType), splits[2])
        //    };
        //    dataPoint.AddDefaultDimensions(lambdaContext);
        //    foreach (var dimensionString in dimensionStrings)
        //    {
        //        splits = Regex.Split(dimensionString, @"(?<!%)=");
        //        dataPoint.dimensions.Add(new Dimension
        //        {
        //            key = splits[0],
        //            value = splits[1]
        //        });
        //    }
        //    return dataPoint;
        //}

        //private static Datum NewDatum(String value)
        //{
        //    Datum datum = new Datum();
        //    if (int.TryParse(value, out int intValue))
        //    {
        //        datum.intValue = intValue;

        //    }
        //    else if (double.TryParse(value, out double doubleValue))
        //    {
        //        datum.doubleValue = doubleValue;
        //    }
        //    else
        //    {
        //        datum.strValue = value;
        //    }
        //    return datum;
        //}

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
