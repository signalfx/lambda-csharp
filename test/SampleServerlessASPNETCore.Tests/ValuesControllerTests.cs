using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Amazon.Lambda.TestUtilities;
using Amazon.Lambda.APIGatewayEvents;
using Newtonsoft.Json;
using TestHelpers;
using Xunit;
using SignalFx.LambdaWrapper;
using SignalFx.Tracing;

namespace SampleServerlessASPNETCore.Tests
{
    public class ValuesControllerTests
    {
        [Fact]
        public void TestGet()
        {
            var lambdaFunction = new LambdaEntryPoint();

            var request = GetTestRequest("./SampleRequests/ValuesController-Get.json");
            var context = new TestLambdaContext
            {
                FunctionName = "ServerlessFunction",
                InvokedFunctionArn = "arn:aws:lambda:us-east-2:123456789012:function:my-function:1",
            };

            APIGatewayProxyResponse response = null;
            var spans = BackendMock.CollectSpans(
                async () => response = await lambdaFunction.FunctionHandlerAsync(request, context));

            // Check response.
            Assert.Equal(200, response.StatusCode);
            Assert.Equal("[\"value1\",\"value2\"]", response.Body);
            Assert.True(response.MultiValueHeaders.ContainsKey("Content-Type"));
            Assert.Equal("application/json; charset=utf-8", response.MultiValueHeaders["Content-Type"][0]);

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

            var request = GetTestRequest("./SampleRequests/ValuesController-GetId.json");
            var context = new TestLambdaContext
            {
                FunctionName = "ServerlessFunction",
                InvokedFunctionArn = "arn:aws:lambda:us-east-2:123456789012:function:my-function:1",
            };

            APIGatewayProxyResponse response = null;
            IImmutableList<IMockSpan> spans = null;
            const string propagatedTraceId = "0123456789abceff";
            const string parentSpanId = "0123456789abceff";

            try
            {
                TelemetryConfiguration.ContextPropagationEnabled = true;
                request.MultiValueHeaders = new Dictionary<string, IList<string>>
                {
                    { HttpHeaderNames.B3TraceId, new List<string> { propagatedTraceId }},
                    { HttpHeaderNames.B3SpanId, new List<string> { parentSpanId }},
                    { HttpHeaderNames.B3Sampled, new List<string> { "1" }},
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
            Assert.True(response.MultiValueHeaders.ContainsKey("Content-Type"));
            Assert.Equal("text/plain; charset=utf-8", response.MultiValueHeaders["Content-Type"][0]);

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

            var request = GetTestRequest("./SampleRequests/ValuesController-GetNegativeId.json");
            var context = new TestLambdaContext
            {
                FunctionName = "ServerlessFunction",
                InvokedFunctionArn = "arn:aws:lambda:us-east-2:123456789012:function:my-function:1",
            };

            APIGatewayProxyResponse response = null;
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

        private static APIGatewayProxyRequest GetTestRequest(string jsonRequestFile)
        {
            var requestStr = File.ReadAllText(jsonRequestFile);
            return JsonConvert.DeserializeObject<APIGatewayProxyRequest>(requestStr);
        }
    }
}
