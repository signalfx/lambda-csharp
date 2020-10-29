using System;
using System.Collections.Generic;

using Amazon.Lambda.Core;
using SignalFx.Tracing;
using SignalFx.Tracing.Headers;

namespace SignalFx.LambdaWrapper
{
    /// <summary>
    /// This is a helper that is used to wrap AWS Lambda functions with telemetry data.
    /// </summary>
    internal struct TelemetryTracker : IDisposable
    {
        internal static readonly Tracer s_sfxTracer;
        internal static bool s_coldStart;

        internal readonly Scope _sfxScope;
        internal readonly MetricWrapper _metricWrapper;

        static TelemetryTracker()
        {
            s_coldStart = true;
            if (TelemetryConfiguration.TracingEnabled)
            {
                // This forces the registration of the SFx tracer with OpenTracing.
                s_sfxTracer = Tracer.Instance;
            }
        }

        internal TelemetryTracker(
            ILambdaContext lambdaContext,
            string operationName = null,
            IEnumerable<KeyValuePair<string, string>> tags = null,
            IHeadersCollection headersCollection = null)
            : this()
        {
            if (!TelemetryConfiguration.TracingEnabled && !TelemetryConfiguration.MetricsEnabled)
            {
                return;
            }

            var coldStart = s_coldStart;
            if (coldStart)
            {
                s_coldStart = false;
            }

            var commonTags = lambdaContext.ExtractCommonTags();

            if (TelemetryConfiguration.MetricsEnabled)
            {
                _metricWrapper = new MetricWrapper(commonTags);
            }

            if (TelemetryConfiguration.TracingEnabled)
            {
                ISpanContext parentContext = null;
                if (TelemetryConfiguration.ContextPropagationEnabled && headersCollection != null)
                {
                    parentContext = B3SpanContextPropagator.Instance.Extract(headersCollection);
                }

                operationName = operationName ?? lambdaContext.FunctionName;
                _sfxScope = s_sfxTracer.StartActive(operationName, parentContext);

                var sfxSpan = _sfxScope.Span;
                sfxSpan.SetTag("span.kind", "server");
                sfxSpan.SetTag("component", "dotnet-lambda-wrapper");
                sfxSpan.SetTag("aws_request_id", lambdaContext.AwsRequestId);
                foreach (var kvp in commonTags)
                {
                    sfxSpan.SetTag(kvp.Key, kvp.Value);
                }

                if (coldStart)
                {
                    sfxSpan.SetTag("cold_start", "true");
                }

                if (tags != null)
                {
                    foreach (var kvp in tags)
                    {
                        sfxSpan.SetTag(kvp.Key, kvp.Value);
                    }
                }
            }
        }

        public void Dispose()
        {
            _metricWrapper?.Dispose();
            _sfxScope?.Dispose();
        }

        internal void SetErrorCounter()
        {
            _metricWrapper?.Error();
        }

        internal void SetException(Exception exception)
        {
            _metricWrapper?.Error();

            if (_sfxScope != null)
            {
                // Use SignalFx Span type to leverage the helper method to set error information tags.
                var sfxSpan = _sfxScope.Span;
                sfxSpan.SetException(exception);
            }
        }
    }
}
