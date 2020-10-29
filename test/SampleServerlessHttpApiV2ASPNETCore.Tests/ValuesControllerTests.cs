using System;
using System.IO;
using System.Linq;

using Amazon.Lambda.TestUtilities;
using Amazon.Lambda.APIGatewayEvents;
using Newtonsoft.Json;
using TestHelpers;
using Xunit;
using System.Collections.Immutable;
using SignalFx.LambdaWrapper;
using System.Collections.Generic;
using SignalFx.Tracing;

namespace SampleServerlessHttpApiV2ASPNETCore.Tests
{
    public class ValuesControllerTests
    {
        [Fact]
        public void TestGet()
        {
            var lambdaFunction = new LambdaEntryPoint();

            var request = GetTestRequest("./SampleRequests/ValuesController-V2-Get.json");
            var context = new TestLambdaContext
            {
                FunctionName = "ServerlessFunction",
                InvokedFunctionArn = "arn:aws:lambda:us-east-2:123456789012:function:my-function:1",
            };

            APIGatewayHttpApiV2ProxyResponse response = null;
            var spans = BackendMock.CollectSpans(
                async () => response = await lambdaFunction.FunctionHandlerAsync(request, context));

            // Check response.
            Assert.Equal(200, response.StatusCode);
            Assert.Equal("[\"value1\",\"value2\"]", response.Body);
            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("application/json; charset=utf-8", response.Headers["Content-Type"]);

            // Check trace.
            Assert.Single(spans);
            var span = spans.First();
            Assert.Equal("api/Values", span.Name);
            Assert.True(span.Tags.TryGetValue("span.kind", out var spanKind));
            Assert.Equal("server", spanKind);
            Assert.True(span.Tags.TryGetValue("http.method", out var httpMethod));
            Assert.Equal("GET", httpMethod);
        }

        [Fact]
        public void TestGetId()
        {
            var lambdaFunction = new LambdaEntryPoint();

            var request = GetTestRequest("./SampleRequests/ValuesController-V2-GetId.json");
            var context = new TestLambdaContext
            {
                FunctionName = "ServerlessFunction",
                InvokedFunctionArn = "arn:aws:lambda:us-east-2:123456789012:function:my-function:1",
            };

            APIGatewayHttpApiV2ProxyResponse response = null;
            IImmutableList<IMockSpan> spans = null;
            const string propagatedTraceId = "0123456789abcef0";
            const string parentSpanId = "0123456789abcef0";

            try
            {
                TelemetryConfiguration.ContextPropagationEnabled = true;
                request.Headers = new Dictionary<string, string>
                {
                    { HttpHeaderNames.B3TraceId, propagatedTraceId },
                    { HttpHeaderNames.B3SpanId, parentSpanId },
                    { HttpHeaderNames.B3Sampled, "1" },
                };

                spans = BackendMock.CollectSpans(
                    async () => response = await lambdaFunction.FunctionHandlerAsync(request, context));
            }
            finally
            {
                TelemetryConfiguration.ContextPropagationEnabled = false;
            }

            // Check response.
            Assert.Equal(200, response.StatusCode);
            Assert.Equal("value_56", response.Body);
            Assert.True(response.Headers.ContainsKey("Content-Type"));
            Assert.Equal("text/plain; charset=utf-8", response.Headers["Content-Type"]);

            // Check trace.
            Assert.Single(spans);
            var span = spans.First();
            Assert.Equal("api/Values/{id}", span.Name);
            Assert.True(span.Tags.TryGetValue("span.kind", out var spanKind));
            Assert.Equal("server", spanKind);
            Assert.True(span.Tags.TryGetValue("http.method", out var httpMethod));
            Assert.Equal("GET", httpMethod);

            // Check context propagation.
            Assert.Equal(propagatedTraceId, span.TraceId.ToString("x16"));
            Assert.True(span.ParentId.HasValue);
            Assert.Equal(parentSpanId, span.ParentId.Value.ToString("x16"));
        }

        [Fact]
        public void TestGetIdException()
        {
            var lambdaFunction = new LambdaEntryPoint();

            var request = GetTestRequest("./SampleRequests/ValuesController-V2-GetNegativeId.json");
            var context = new TestLambdaContext
            {
                FunctionName = "ServerlessFunction",
                InvokedFunctionArn = "arn:aws:lambda:us-east-2:123456789012:function:my-function:1",
            };

            APIGatewayHttpApiV2ProxyResponse response = null;
            var spans = BackendMock.CollectSpans(
                async () => response = await lambdaFunction.FunctionHandlerAsync(request, context));

            // Check response.
            Assert.Equal(500, response.StatusCode);

            // Check trace.
            Assert.Single(spans);

            var span = spans.First();
            Assert.Equal("api/Values/{id}", span.Name);
            Assert.True(span.Tags.TryGetValue("error", out var errorValue));
            Assert.Equal("true", errorValue);
            Assert.True(span.Tags.TryGetValue("sfx.error.kind", out var actualErrorKind));
            Assert.Equal(typeof(ArgumentOutOfRangeException).FullName, actualErrorKind);
        }

        private static APIGatewayHttpApiV2ProxyRequest GetTestRequest(string jsonRequestFile)
        {
            var requestStr = File.ReadAllText(jsonRequestFile);
            return JsonConvert.DeserializeObject<APIGatewayHttpApiV2ProxyRequest>(requestStr);
        }
    }
}
