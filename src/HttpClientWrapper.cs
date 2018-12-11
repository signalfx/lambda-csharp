using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using com.signalfuse.metrics.protobuf;
using ProtoBuf;

namespace SignalFx.LambdaWrapper
{
    public class HttpClientWrapper
    {
        private static readonly string DefaultBaseAddressUrl = "https://ingest.signalfx.com";
        private static readonly string DefaultDataPointIngestPath = "v2/datapoint";
        private static readonly string AuthTokenHeaderName = "X-Sf-Token";
        private readonly HttpClient _httpClient;
        private readonly Config _config;
        
        public class Config
        {
            public string   AuthToken              { get; set; } = GetStringEnvironmentVariable("SIGNALFX_AUTH_TOKEN");
            public Uri      BaseAddress            { get; set; } = new Uri(GetStringEnvironmentVariable("SIGNALFX_INGEST_ENDPOINT_BASE_ADDRESS", DefaultBaseAddressUrl));
            public Uri      DataPointIngestPath    { get; set; } = new Uri(GetStringEnvironmentVariable("SIGNALFX_INGEST_ENDPOINT_PATH", DefaultDataPointIngestPath), UriKind.Relative);
            public TimeSpan Timeout                { get; set; } = TimeSpan.FromSeconds(GetDoubleEnvironmentVariable("SIGNALFX_SEND_TIMEOUT_SECONDS", 5));
            public TimeSpan ConnectionLeaseTimeout { get; set; } = TimeSpan.FromSeconds(5);
            public TimeSpan DnsRefreshTimeout      { get; set; } = TimeSpan.FromSeconds(5);
        }

        public HttpClientWrapper(Config config = null, HttpMessageHandler handler = null, bool disposeHandler = false)
        {
            _config = config ?? new Config();
            _httpClient = handler == null ? new HttpClient() : new HttpClient(handler, disposeHandler);
            _httpClient.BaseAddress = _config.BaseAddress;
            _httpClient.Timeout = _config.Timeout;
            ServicePointManager.FindServicePoint(_config.BaseAddress).ConnectionLeaseTimeout = (int)_config.ConnectionLeaseTimeout.TotalMilliseconds;
            ServicePointManager.DnsRefreshTimeout = (int)_config.DnsRefreshTimeout.TotalMilliseconds;
        }

        ~HttpClientWrapper()
        {
            _httpClient.Dispose();
        }

        internal async Task<HttpResponseMessage> PostDataPointsAsync(IEnumerable<DataPoint> dataPoints)
        {
            DataPointUploadMessage dataPointUploadMessage = new DataPointUploadMessage();
            dataPointUploadMessage.datapoints.AddRange(dataPoints);
            using (var httpContent = NewHttpContent(_config, dataPointUploadMessage))
            {
                return await _httpClient.PostAsync(_config.DataPointIngestPath, httpContent);
            }
        }

        private static HttpContent NewHttpContent(Config config, DataPointUploadMessage dataPointUploadMessage)
        {
            var memoryStream = new MemoryStream();
            Serializer.Serialize<DataPointUploadMessage>(memoryStream, dataPointUploadMessage);
            memoryStream.Flush();
            memoryStream.Seek(0, SeekOrigin.Begin);
            HttpContent httpContent = new StreamContent(memoryStream);
            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");
            httpContent.Headers.Add(AuthTokenHeaderName, config.AuthToken);
            return httpContent;
        }

        private static string GetStringEnvironmentVariable(string variable, string defaultValue = null)
        {
            var stringValue = Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.Process);
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                if (defaultValue == null)
                {
                    LambdaLogger.Log($"[Error] environment variable {variable} is not set.{Environment.NewLine}");
                }
                else
                {
                    stringValue = defaultValue;
                    LambdaLogger.Log($"[Warning] environment variable {variable} is not set. Using default value instead.{Environment.NewLine}");
                }
            }
            return stringValue;
        }

        private static double GetDoubleEnvironmentVariable(string variable, double defaultValue)
        {
            if (double.TryParse(Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.Process), out double doubleValue))
            {
                return doubleValue;
            }
            else
            {
                LambdaLogger.Log($"[Warning] environment variable {variable} is not set. Using default value of {defaultValue} instead.{Environment.NewLine}");
                return defaultValue;
            }
        }
    }
}
