using System;
using System.IO;
using System.Net;
using System.Security;

using Amazon.Lambda.Core;
using com.signalfuse.metrics.protobuf;
using ProtoBuf;
using SignalFx.LambdaWrapper.Helpers;

namespace SignalFx.LambdaWrapper
{
    public class SignalFxReporter : ISignalFxReporter
    {
        private readonly IWebRequestorFactory _requestor;

        public SignalFxReporter(string baseURI, string apiToken, int timeoutInMilliseconds, IWebRequestorFactory requestor = null)
        {
            if (requestor == null)
            {
                requestor = new WebRequestorFactory()
                    .WithUri(baseURI + "/v2/datapoint")
                    .WithMethod("POST")
                    .WithContentType("application/x-protobuf")
                    .WithHeader("X-SF-TOKEN", apiToken)
                    .WithTimeout(timeoutInMilliseconds);
            }

            this._requestor = requestor;
        }

        public void Send(DataPointUploadMessage msg)
        {
            try
            {
                var request = _requestor.GetRequestor();
                using (var rs = request.GetWriteStream())
                {
                    Serializer.Serialize(rs, msg);
                    // flush the message before disposing
                    rs.Flush();
                }
                try
                {
                    using (request.Send())
                    {
                    }
                }
                catch (SecurityException)
                {
                    LambdaLogger.Log("API token for sending metrics to SignalFx is invalid");
                    throw;
                }
            }
            catch (Exception ex)
            {
                if (ex is WebException)
                {
                    var webex = ex as WebException;
                    using (var exresp = webex.Response)
                    {
                        if (exresp != null)
                        {
                            var stream2 = exresp.GetResponseStream();
                            var reader2 = new StreamReader(stream2);
                            var errorStr = reader2.ReadToEnd();
                            LambdaLogger.Log(errorStr);
                        }
                    }
                }
                throw;
            }
        }
    }
}
