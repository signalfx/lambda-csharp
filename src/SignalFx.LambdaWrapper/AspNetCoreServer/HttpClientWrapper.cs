using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using com.signalfuse.metrics.protobuf;
using ProtoBuf;

namespace SignalFx.LambdaWrapper.AspNetCoreServer
{
    public partial class HttpClientWrapper
    {
        private readonly HttpClient _httpClient;

        public HttpClientWrapper(HttpMessageHandler handler = null, bool disposeHandler = false)
        {
            _httpClient = handler == null ? new HttpClient() : new HttpClient(handler, disposeHandler);
            _httpClient.BaseAddress = Config.BaseAddress;
            _httpClient.Timeout = Config.Timeout;
            ServicePointManager.FindServicePoint(Config.BaseAddress).ConnectionLeaseTimeout = (int)Config.ConnectionLeaseTimeout.TotalMilliseconds;
            ServicePointManager.DnsRefreshTimeout = (int)Config.DnsRefreshTimeout.TotalMilliseconds;
        }

        ~HttpClientWrapper()
        {
            _httpClient.Dispose();
        }

        internal async Task<HttpResponseMessage> PostDataPointsAsync(IEnumerable<DataPoint> dataPoints)
        {
            var dataPointUploadMessage = new DataPointUploadMessage();
            dataPointUploadMessage.datapoints.AddRange(dataPoints);
            using (var httpContent = NewHttpContent(dataPointUploadMessage))
            {
                return await _httpClient.PostAsync(Config.DataPointIngestPath, httpContent);
            }
        }

        private static HttpContent NewHttpContent(DataPointUploadMessage dataPointUploadMessage)
        {
            var memoryStream = new MemoryStream();
            Serializer.Serialize(memoryStream, dataPointUploadMessage);
            memoryStream.Flush();
            memoryStream.Seek(0, SeekOrigin.Begin);
            HttpContent httpContent = new StreamContent(memoryStream);
            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");
            httpContent.Headers.Add(Config.AuthTokenHeaderName, Config.AuthToken);
            return httpContent;
        }

    }

}
