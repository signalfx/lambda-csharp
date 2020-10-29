using BenchmarkDotNet.Attributes;
using com.signalfuse.metrics.protobuf;
using SignalFx.LambdaWrapper;

namespace Benchmark
{
    [MemoryDiagnoser]
    [ThreadingDiagnoser]

    public class SignalFxReporterBenchmarks
    {
        private NoopCollector _noopCollector;
        private SignalFxReporter _sfxReporter;
        private DataPointUploadMessage _dataPointUploadMessage;

        [Params(1, 10, 100)]
        public int N { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            _noopCollector = new NoopCollector();

            const int dataPointsPerMsg = 10;
            _dataPointUploadMessage = new DataPointUploadMessage();
            for (int i = 0; i < dataPointsPerMsg; ++i)
            {
                var dp = new DataPoint
                {
                    metric = "benchmark" + i,
                    value = new Datum { intValue = i },
                    metricType = MetricType.COUNTER,
                };

                dp.dimensions.Add(new Dimension { key = "test0", value = "benchmark" });
                dp.dimensions.Add(new Dimension { key = "test1", value = "benchmark" });

                _dataPointUploadMessage.datapoints.Add(dp);
            }
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _noopCollector.Dispose();
            _dataPointUploadMessage = null;
        }

        [IterationSetup]
        public void IterationSetup()
        {
            _sfxReporter = new SignalFxReporter($"http://localhost:{_noopCollector.Port}", "testToken", 1000);
        }

        [Benchmark]
        public void SendDataPointBatch()
        {
            for (int i = 0; i < N; i++)
            {
                _sfxReporter.Send(_dataPointUploadMessage);
            }
        }
    }
}
