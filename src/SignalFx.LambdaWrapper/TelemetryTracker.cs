using System;
using System.Collections.Generic;

using Amazon.Lambda.Core;
using OpenTracing;
using OpenTracing.Propagation;
using OpenTracing.Util;
using SignalFx.Tracing;

namespace SignalFx.LambdaWrapper
{
    /// <summary>
    /// This is a helper that is used to wrap AWS Lambda functions with telemetry data.
    /// </summary>
    internal struct TelemetryTracker : IDisposable
    {
        internal static readonly Tracer s_sfxTracer;
        internal static readonly ITracer s_otTracer;
        internal static bool s_coldStart;

        internal readonly IScope _otScope;
        internal readonly MetricWrapper _metricWrapper;

        static TelemetryTracker()
        {
            s_coldStart = true;
            if (TelemetryConfiguration.TracingEnabled)
            {
                // This forces the registration of the SFx tracer with OpenTracing.
                s_sfxTracer = Tracer.Instance;
#pragma warning disable CS0612 // Type or member is obsolete
                if (s_sfxTracer.Settings.DebugEnabled)
#pragma warning restore CS0612 // Type or member is obsolete
                {
                    LambdaLogger.Log("[DBG] Tracer.Instance registered");
                }

                s_otTracer = GlobalTracer.Instance;
            }
        }

        internal TelemetryTracker(
            ILambdaContext lambdaContext,
            string operationName = null,
            IEnumerable<KeyValuePair<string, string>> tags = null,
            IDictionary<string, string> headers = null)
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
                OpenTracing.ISpanContext parentContext = null;
                if (TelemetryConfiguration.ContextPropagationEnabled && headers != null)
                {
                    var tracer = GlobalTracer.Instance;
                    parentContext = tracer.Extract(BuiltinFormats.HttpHeaders, new TextMapExtractAdapter(headers));
                }

                operationName = operationName ?? lambdaContext.FunctionName;
                _otScope = s_otTracer.BuildSpan(operationName)
                    .AsChildOf(parentContext)
                    .WithTag("span.kind", "server")
                    .WithTag("component", "dotnet-lambda-wrapper")
                    .WithTag("aws_request_id", lambdaContext.AwsRequestId)
                    .StartActive();

                var otSpan = _otScope.Span;
                foreach (var kvp in commonTags)
                {
                    otSpan.SetTag(kvp.Key, kvp.Value);
                }

                if (coldStart)
                {
                    otSpan.SetTag("cold_start", "true");
                }

                if (tags != null)
                {
                    foreach (var kvp in tags)
                    {
                        otSpan.SetTag(kvp.Key, kvp.Value);
                    }
                }
            }
        }

        public void Dispose()
        {
            _metricWrapper?.Dispose();
            _otScope?.Dispose();
        }

        internal void SetErrorCounter()
        {
            _metricWrapper?.Error();
        }

        internal void SetException(Exception exception)
        {
            _metricWrapper?.Error();

            // Use SignalFx Span type to leverage the helper method to set error information tags.
            s_sfxTracer?.ActiveScope?.Span?.SetException(exception);
        }
    }
}
