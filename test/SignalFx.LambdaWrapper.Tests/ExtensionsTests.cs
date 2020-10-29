using System.Collections.Generic;
using System.Linq;

using com.signalfuse.metrics.protobuf;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Moq;
using Newtonsoft.Json;
using TestHelpers;
using Xunit;

namespace SignalFx.LambdaWrapper.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void TestAddMetricDataPoint()
        {
            var httpResponseMock = new Mock<HttpResponse>();
            var headerDictionary = new HeaderDictionary();
            httpResponseMock.SetupGet(m => m.Headers).Returns(headerDictionary);

            Extensions.AddMetricDataPoint(httpResponseMock.Object, null);
            Assert.Empty(headerDictionary);

            Extensions.AddMetricDataPoint(httpResponseMock.Object, new DataPoint());
            Assert.Empty(headerDictionary);

            var dp = BuildDataPoint();
            httpResponseMock.Object.AddMetricDataPoint(dp);
            Assert.Single(headerDictionary);

            var header = headerDictionary.First();
            Assert.StartsWith("sfx_metric_datapoint-", header.Key);
            Assert.Equal(JsonConvert.SerializeObject(dp), header.Value);
        }

        [Fact]
        public void TestExtractCustomMetricDataPoints()
        {
            var responseFeatureMock = new Mock<IHttpResponseFeature>();
            var headerDictionary = new HeaderDictionary();
            responseFeatureMock.SetupGet(m => m.Headers).Returns(headerDictionary);

            List<DataPoint> actualDataPoints = responseFeatureMock.Object.ExtractCustomMetricDataPoints();
            Assert.Null(actualDataPoints);
            Assert.Empty(headerDictionary);

            var httpResponseMock = new Mock<HttpResponse>();
            httpResponseMock.SetupGet(m => m.Headers).Returns(headerDictionary);
            httpResponseMock.Object.AddMetricDataPoint(BuildDataPoint());
            Assert.Single(headerDictionary);

            actualDataPoints = responseFeatureMock.Object.ExtractCustomMetricDataPoints();
            Assert.Single(actualDataPoints);

            Assert.Equal(BuildDataPoint().metric, actualDataPoints[0].metric);
            Assert.Empty(headerDictionary);
        }

        [Theory]
        [InlineData("./SampleContexts/lambda-context.json")]
        [InlineData("./SampleContexts/serverless-context.json")]
        public void TestAddDefaultDimensions(string jsonContextFile)
        {
            var context = ContextUtils.FromJsonFile(jsonContextFile);

            var dp = BuildDataPoint();
            dp.AddDefaultDimensions(context);

            var expectedDimensions = new Dictionary<string, string>
            {
                { "aws_execution_env", "" }, // Expected to be empty since the env var is not defined.
                { "aws_function_name", context.FunctionName },
                { "aws_function_version", context.FunctionVersion },
                { "aws_region", "us-west-2" },
                { "aws_account_id", "123456789012" },
                { "lambda_arn", $"{context.InvokedFunctionArn}:{context.FunctionVersion}" },
                { "function_wrapper_version", "signalfx_lambda_2.0.2.0" },
                { "metric_source", "lambda_wrapper" },
                { "test", "extensions" },
            };

            foreach (var dim in dp.dimensions)
            {
                Assert.Contains(dim.key, expectedDimensions.Keys);
                Assert.Equal(expectedDimensions[dim.key], dim.value);
            }
        }

        [Theory]
        [InlineData("./SampleContexts/lambda-context.json")]
        [InlineData("./SampleContexts/serverless-context.json")]

        public void CompareLambdaContextExtraction(string jsonContextFile)
        {
            var context = ContextUtils.FromJsonFile(jsonContextFile);

            var dpServerless = BuildDataPoint();
            var dpLambda = BuildDataPoint();

            dpServerless.AddDefaultDimensions(context);
            dpLambda.dimensions.AddRange(MetricWrapper.GetDefaultDimensions(context.ExtractCommonTags()));

            // Sort dimensions to make easy to compare.
            dpServerless.dimensions.Sort((d0, d1) => string.Compare(d0.key, d1.key));
            dpLambda.dimensions.Sort((d0, d1) => string.Compare(d0.key, d1.key));

            Assert.Equal(dpServerless.dimensions.Count, dpLambda.dimensions.Count);
            for (int i = 0; i < dpLambda.dimensions.Count; i++)
            {
                Assert.Equal(dpLambda.dimensions[i].key, dpServerless.dimensions[i].key);
                Assert.Equal(dpLambda.dimensions[i].value, dpServerless.dimensions[i].value);
            }
        }

        private DataPoint BuildDataPoint()
        {
            var dp = new DataPoint
            {
                metric = "test-extensios",
                value = new Datum { intValue = 1 },
                metricType = MetricType.COUNTER,
            };

            dp.dimensions.Add(new Dimension { key = "test", value = "extensions" });
            return dp;
        }
    }
}
