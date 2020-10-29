using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Amazon.Lambda.Core;
using TestHelpers;
using Xunit;

namespace SampleLambdaFunctions.Tests
{
    public class FunctionTest : IClassFixture<LambdaEnvVarFixture>
    {
        [Fact]
        public void TestVoidFunction()
        {
            var functions = new Functions();
            var context = GetTestContext();

            var spans = BackendMock.CollectSpans(
                () => functions.VoidFunction("hello world", context));

            AssertRootSpan(spans, exceptionRaised: false);
        }

        [Fact]
        public void TestVoidFunctionException()
        {
            var functions = new Functions();
            var context = GetTestContext();

            Exception actualException = null;
            var spans = BackendMock.CollectSpans(() =>
            {
                try
                {
                    functions.VoidFunction(null, context);
                }
                catch (Exception e)
                {
                    actualException = e;
                }
            });

            AssertRootSpan(spans, exceptionRaised: true);

            var expectedException = new ArgumentNullException("input");
            Assert.Equal(expectedException.Message, actualException.Message);
        }

        [Fact]
        public void TestFunctionWithReturn()
        {
            var functions = new Functions();
            var context = GetTestContext();
            string upperCase = null;
            
            var spans = BackendMock.CollectSpans(
                () => upperCase = functions.FunctionWithReturn("hello world", context));

            AssertRootSpan(spans, exceptionRaised: false);

            Assert.Equal("HELLO WORLD", upperCase);
        }

        [Fact]
        public void TestFunctionWithReturnException()
        {
            var functions = new Functions();
            var context = GetTestContext();
            Exception actualException = null;
            var spans = BackendMock.CollectSpans(() =>
            {
                try
                {
                    functions.FunctionWithReturn(null, context);
                }
                catch (Exception e)
                {
                    actualException = e;
                }
            });

            AssertRootSpan(spans, exceptionRaised: true);

            var expectedException = new ArgumentNullException("input");
            Assert.Equal(expectedException.Message, actualException.Message);
        }

        [Fact]
        public void TestAsyncFunction()
        {
            var functions = new Functions();
            var context = GetTestContext();

            var spans = BackendMock.CollectSpans(
                async () => await functions.AsyncFunction("hello world", context));

            AssertRootSpan(spans, exceptionRaised: false);
        }

        [Fact]
        public void TestAsynFunctionException()
        {
            var functions = new Functions();
            var context = GetTestContext();

            Exception actualException = null;
            var spans = BackendMock.CollectSpans(async () =>
            {
                try
                {
                    await functions.AsyncFunction(null, context);
                }
                catch (Exception e)
                {
                    actualException = e;
                }
            });

            AssertRootSpan(spans, exceptionRaised: true);

            var expectedException = new ArgumentNullException("input");
            Assert.Equal(expectedException.Message, actualException.Message);
        }

        [Fact]
        public void TestAsyncFunctionWithReturn()
        {
            var functions = new Functions();
            var context = GetTestContext();
            string upperCase = null;

            var spans = BackendMock.CollectSpans(
                async () => upperCase = await functions.AsyncFunctionWithReturn("hello world", context));

            AssertRootSpan(spans, exceptionRaised: false);

            Assert.Equal("HELLO WORLD", upperCase);
        }

        [Fact]
        public void TestAsynFunctionWithReturnException()
        {
            var functions = new Functions();
            var context = GetTestContext();

            Exception actualException = null;
            var spans = BackendMock.CollectSpans(async () =>
            {
                try
                {
                    await functions.AsyncFunction(null, context);
                }
                catch (Exception e)
                {
                    actualException = e;
                }
            });

            AssertRootSpan(spans, exceptionRaised: true);

            var expectedException = new ArgumentNullException("input");
            Assert.Equal(expectedException.Message, actualException.Message);
        }

        private void AssertRootSpan(IImmutableList<IMockSpan> spans, bool exceptionRaised)
        {
            Assert.Single(spans);
            var span = spans.First();
            Assert.Equal("sample-lambda-functions", span.Name);
            Assert.Null(span.ParentId);

            var expectedTags = new Dictionary<string, string>
            {
                { "aws_request_id", "33d46d0c-297f-44ec-84f6-d69737d65553" },
                { "lambda_arn", "arn:aws:lambda:us-west-2:123456789012:function:sample-lambda-functions:$LATEST" },
                { "aws_region", "us-west-2" },
                { "aws_account_id", "123456789012" },
                { "aws_function_name", "sample-lambda-functions" },
                { "aws_function_version", "$LATEST" },
                { "aws_execution_env", LambdaEnvVarFixture.ExecutionEnvironment },
                { "function_wrapper_version", "signalfx_lambda_2.0.2.0" },
                { "component", "dotnet-lambda-wrapper" },
                { "span.kind", "server" },
            };

            if (!exceptionRaised)
            {
                Assert.False(span.Tags.TryGetValue("sfx.error.message", out var _));
                Assert.False(span.Tags.TryGetValue("sfx.error.kind", out var _));
                Assert.False(span.Tags.TryGetValue("sfx.error.stack", out var _));
            }
            else
            {
                expectedTags.Add("error", "true");

                // The logic to set the expection is on the Tracing package,
                // just check that the tags exist.
                Assert.True(span.Tags.TryGetValue("sfx.error.message", out var _));
                Assert.True(span.Tags.TryGetValue("sfx.error.kind", out var _));
                Assert.True(span.Tags.TryGetValue("sfx.error.stack", out var _));
            }

            foreach (var expectedTag in expectedTags)
            {
                Assert.True(span.Tags.TryGetValue(expectedTag.Key, out var actualValue), expectedTag.Key);
                Assert.Equal(expectedTag.Value, actualValue);
            }
        }

        private ILambdaContext GetTestContext()
        {
            return ContextUtils.FromJsonFile("./SampleContexts/lambda-context.json");
        }
    }
}
