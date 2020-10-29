using System;
using System.Collections.Generic;
using System.Linq;

using com.signalfuse.metrics.protobuf;
using Moq;
using TestHelpers;
using Xunit;

namespace SignalFx.LambdaWrapper.Tests
{
    public class MetricWrapperTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RecommendedUsage(bool throwException)
        {
            var context = ContextUtils.FromJsonFile("./SampleContexts/lambda-context.json");

            var mockSender = new Mock<ISignalFxReporter>();
            DataPointUploadMessage actualDataPointMsg = null;
            mockSender.Setup(m => m.Send(It.IsNotNull<DataPointUploadMessage>()))
                .Callback<DataPointUploadMessage>(msg => actualDataPointMsg = msg);

            using (var wrapper = new MetricWrapper(context, mockSender.Object))
            {
                try
                {
                    if (throwException)
                    {
                        throw new ApplicationException("test");
                    }
                }
                catch (Exception)
                {
                    wrapper.Error();
                }
            }

            Assert.NotNull(actualDataPointMsg);

            var expectedDimensions = new Dictionary<string, string>
            {
                { "aws_execution_env", "" }, // Expected to be empty since the env var is not defined.
                { "aws_function_name", "sample-lambda-functions" },
                { "aws_function_version", "$LATEST" },
                { "aws_region", "us-west-2" },
                { "aws_account_id", "123456789012" },
                { "lambda_arn", "arn:aws:lambda:us-west-2:123456789012:function:sample-lambda-functions:$LATEST" }, //
                { "function_wrapper_version", "signalfx_lambda_2.0.2.0" },
                { "metric_source", "lambda_wrapper" },
            };

            // It is hard to control for cold starts, it is exists check for the dimensions and remove it.
            var coldStartMetric = actualDataPointMsg.datapoints.FirstOrDefault(dp => dp.metric == "function.cold_starts");
            if (coldStartMetric != null)
            {
                AssertDimensions(coldStartMetric, expectedDimensions);
                actualDataPointMsg.datapoints.Remove(coldStartMetric);
            }

            var expectedMetrics = new List<dynamic>
            {
                new { Name = "function.invocations", MetricType = MetricType.COUNTER },
                new { Name = "function.duration", MetricType = MetricType.GAUGE },
            };

            if (throwException)
            {
                expectedMetrics.Add(new { Name = "function.errors", MetricType = MetricType.COUNTER });
            }

            Assert.Equal(actualDataPointMsg.datapoints.Count, expectedMetrics.Count);
            foreach (var expectedMetric in expectedMetrics)
            {
                var actualMetric = actualDataPointMsg.datapoints.FirstOrDefault(dp => dp.metric == expectedMetric.Name);
                Assert.True(actualMetric != null, $"Expected metric {expectedMetric.Name} was not found.");
                Assert.Equal(expectedMetric.MetricType, actualMetric.metricType);

                AssertDimensions(actualMetric, expectedDimensions);
            }

            void AssertDimensions(DataPoint actualMetric, dynamic expectedDimensions)
            {
                Assert.Equal(actualMetric.dimensions.Count, expectedDimensions.Count);
                foreach (var expectedDimension in expectedDimensions)
                {
                    var actualDimension = actualMetric.dimensions.FirstOrDefault(dim => dim.key == expectedDimension.Key);
                    Assert.True(actualDimension != null, $"Expected dimension {expectedDimension.Key} was not found.");
                    Assert.Equal(expectedDimension.Value, actualDimension.value);
                }
            }
        }
    }
}
