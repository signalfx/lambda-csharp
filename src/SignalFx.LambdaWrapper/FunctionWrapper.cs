using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using SignalFx.Tracing.Headers;

namespace SignalFx.LambdaWrapper
{
    /// <summary>
    /// This type is used to wrap the original function handlers
    /// </summary>
    public struct FunctionWrapper
    {
        public void Invoke<TInput>(
            Action<TInput, ILambdaContext> handler,
            TInput input,
            ILambdaContext context,
            string operationName = null,
            IEnumerable<KeyValuePair<string, string>> tags = null)
        {
            using (var tracker = new TelemetryTracker(context, operationName, tags))
            {
                try
                {
                    handler(input, context);
                }
                catch (Exception e)
                {
                    tracker.SetException(e);
                    throw;
                }
            }
        }

        public TOutput Invoke<TInput, TOutput>(
            Func<TInput, ILambdaContext, TOutput> handler,
            TInput input,
            ILambdaContext context,
            string operationName = null,
            IEnumerable<KeyValuePair<string, string>> tags = null)
        {
            using (var tracker = new TelemetryTracker(context, operationName, tags))
            {
                try
                {
                    return handler(input, context);
                }
                catch (Exception e)
                {
                    tracker.SetException(e);
                    throw;
                }
            }
        }

        public async Task InvokeAsync<TInput>(
            Func<TInput, ILambdaContext, Task> asyncHandler,
            TInput input,
            ILambdaContext context,
            string operationName = null,
            IEnumerable<KeyValuePair<string, string>> tags = null)
        {
            using (var tracker = new TelemetryTracker(context, operationName, tags))
            {
                try
                {
                    await asyncHandler(input, context);
                }
                catch (Exception e)
                {
                    tracker.SetException(e);
                    throw;
                }
            }
        }

        public async Task<TOutput> InvokeAsync<TInput, TOutput>(
            Func<TInput, ILambdaContext, Task<TOutput>> asyncHandler,
            TInput input,
            ILambdaContext context,
            string operationName = null,
            IEnumerable<KeyValuePair<string, string>> tags = null)
        {
            using (var tracker = new TelemetryTracker(context, operationName, tags))
            {
                try
                {
                    return await asyncHandler(input, context);
                }
                catch (Exception e)
                {
                    tracker.SetException(e);
                    throw;
                }
            }
        }

        public async Task<APIGatewayProxyResponse> InvokeAPIGatewayProxyAsync(
            Func<APIGatewayProxyRequest, ILambdaContext, Task<APIGatewayProxyResponse>> asyncHandler,
            APIGatewayProxyRequest request,
            ILambdaContext context,
            string operationName = null,
            IEnumerable<KeyValuePair<string, string>> tags = null)
        {
            IHeadersCollection headersCollection = null;
            if (TelemetryConfiguration.ContextPropagationEnabled)
            {
                headersCollection = new DictionaryHeadersCollection(request.MultiValueHeaders);
            }

            using (var tracker = new TelemetryTracker(context, operationName, tags, headersCollection))
            {
                try
                {
                    APIGatewayProxyResponse apiGatewayProxyResponse = await asyncHandler(request, context);
                    if (!apiGatewayProxyResponse.IsSuccessStatusCode())
                    {
                        tracker.SetErrorCounter();

                        // Preserve the legacy logging.
                        LambdaLogger.Log($"[ERR] Invoking lambda function. Http status code: {apiGatewayProxyResponse.StatusCode}. Response body: {apiGatewayProxyResponse.Body}{Environment.NewLine}");
                    }

                    return apiGatewayProxyResponse;
                }
                catch (Exception e)
                {
                    tracker.SetException(e);
                    throw;
                }
            }
        }

        public async Task<APIGatewayHttpApiV2ProxyResponse> InvokeAPIGatewayHttpApiV2ProxyAsync(
            Func<APIGatewayHttpApiV2ProxyRequest, ILambdaContext, Task<APIGatewayHttpApiV2ProxyResponse>> asyncHandler,
            APIGatewayHttpApiV2ProxyRequest request,
            ILambdaContext context,
            string operationName = null,
            IDictionary<string, string> tags = null)
        {
            IHeadersCollection headersCollection = null;
            if (TelemetryConfiguration.ContextPropagationEnabled)
            {
                headersCollection = new CommaDelimitedValueHeaders(request.Headers);
            }

            using (var tracker = new TelemetryTracker(context, operationName, tags, headersCollection))
            {
                try
                {
                    APIGatewayHttpApiV2ProxyResponse apiGatewayProxyResponse = await asyncHandler(request, context);
                    if (!apiGatewayProxyResponse.IsSuccessStatusCode())
                    {
                        tracker.SetErrorCounter();

                        // Preserve the legacy logging.
                        LambdaLogger.Log($"[ERR] Invoking lambda function. Http status code: {apiGatewayProxyResponse.StatusCode}. Response body: {apiGatewayProxyResponse.Body}{Environment.NewLine}");
                    }

                    return apiGatewayProxyResponse;
                }
                catch (Exception e)
                {
                    tracker.SetException(e);
                    throw;
                }
            }
        }
    }
}
