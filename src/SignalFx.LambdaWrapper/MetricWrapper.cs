using System;
using System.Collections.Generic;

using Amazon.Lambda.Core;
using com.signalfuse.metrics.protobuf;

namespace SignalFx.LambdaWrapper
{
    public class MetricWrapper : IDisposable
    {
        // signalfx env variables
        private const string AUTH_TOKEN = "SIGNALFX_AUTH_TOKEN";
        private const string TIMEOUT_MS = "SIGNALFX_SEND_TIMEOUT";


        //metric names
        protected const string METRIC_NAME_PREFIX = "function.";
        protected const string METRIC_NAME_INVOCATIONS = "invocations";
        protected const string METRIC_NAME_COLD_STARTS = "cold_starts";
        protected const string METRIC_NAME_ERRORS = "errors";
        protected const string METRIC_NAME_DURATION = "duration";

        private static readonly Dimension s_metricSourceDim = new Dimension { key = "metric_source", value = "lambda_wrapper" };
        private static readonly ISignalFxReporter s_reporter;
        private static bool s_isColdStart = true;

        private readonly System.Diagnostics.Stopwatch _watch;
        private readonly List<Dimension> _defaultDimensions;
        private readonly List<DataPoint> _metricsBatch;
        private readonly ISignalFxReporter _reporter; // Separated from the static to facilitate tests

        static MetricWrapper()
        {
            int timeoutMs = 300; //default timeout 300ms
            if (int.TryParse(Environment.GetEnvironmentVariable(TIMEOUT_MS), out int intValue))
            {
                timeoutMs = intValue;
            }

            // create endpoint
            SignalFxLambdaEndpoint signalFxEndpoint = new SignalFxLambdaEndpoint();

            // create reporter with endpoint
            s_reporter = new SignalFxReporter(signalFxEndpoint.ToString(), Environment.GetEnvironmentVariable(AUTH_TOKEN), timeoutMs);
        }

        public MetricWrapper(ILambdaContext context, ISignalFxReporter reporter = null)
            : this(context.ExtractCommonTags(), reporter)
        {
        }

        public MetricWrapper(List<KeyValuePair<string, string>> commonTags, ISignalFxReporter reporter = null)
        {
            _defaultDimensions = GetDefaultDimensions(commonTags);
            _metricsBatch = new List<DataPoint>(4);
            _reporter = reporter ?? s_reporter;

            _watch = System.Diagnostics.Stopwatch.StartNew();
            AddMetricCounter(METRIC_NAME_INVOCATIONS, MetricType.COUNTER);
            if (s_isColdStart)
            {
                s_isColdStart = false;
                AddMetricCounter(METRIC_NAME_COLD_STARTS, MetricType.COUNTER);
            }
        }

        private void AddMetricCounter(string metricName, MetricType metricType)
        {
            Datum datum = new Datum();
            datum.intValue = 1;

            AddMetric(metricName, metricType, datum);
        }


        private void AddMetric(string metricName, MetricType metricType,
                               Datum datum)
        {
            DataPoint dp = new DataPoint();
            dp.metric = METRIC_NAME_PREFIX + metricName;
            dp.metricType = metricType;
            dp.value = datum;
            dp.dimensions.AddRange(_defaultDimensions);

            _metricsBatch.Add(dp);
        }

        public void Dispose()
        {
            //end stopwatch and send duration
            var elapsedMs = _watch.ElapsedMilliseconds;
            Datum timer = new Datum();
            timer.doubleValue = elapsedMs;
            AddMetric(METRIC_NAME_DURATION, MetricType.GAUGE, timer);

            var msg = new DataPointUploadMessage();
            msg.datapoints.AddRange(_metricsBatch);
            try
            {
                _reporter.Send(msg);
            }
            catch (Exception ex)
            {
                LambdaLogger.Log($"[ERR] Failed to send metrics: {ex.Message}{Environment.NewLine}");
            }
        }

        public void Error()
        {
            AddMetricCounter(METRIC_NAME_ERRORS, MetricType.COUNTER);
        }

        internal static List<Dimension> GetDefaultDimensions(List<KeyValuePair<string, string>> commonTags)
        {
            var defaultDimensions = new List<Dimension>(commonTags.Capacity);

            defaultDimensions.Add(s_metricSourceDim);
            foreach (var tag in commonTags)
            {
                defaultDimensions.Add(new Dimension { key = tag.Key, value = tag.Value });
            }

            return defaultDimensions;
        }
    }
}
