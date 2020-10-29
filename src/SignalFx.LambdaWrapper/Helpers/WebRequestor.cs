using System.IO;
using System.Net;
using System.Security;

namespace SignalFx.LambdaWrapper.Helpers
{
    public class WebRequestor : IWebRequestor
    {
        public WebRequestor(string uri)
        {
            _request = WebRequest.CreateHttp(uri);
        }

        public WebRequestor WithMethod(string method)
        {
            _request.Method = method;
            return this;
        }

        public WebRequestor WithContentType(string contentType)
        {
            _request.ContentType = contentType;
            return this;
        }

        public WebRequestor WithHeader(string name, string value)
        {
            _request.Headers.Add(name, value);
            return this;
        }

        public Stream GetWriteStream()
        {
            return _request.GetRequestStream();
        }

        public Stream Send()
        {
            var resp = (HttpWebResponse)_request.GetResponse();
            if (resp.StatusCode != HttpStatusCode.OK)
            {
                if (resp.StatusCode == HttpStatusCode.Forbidden)
                {
                    throw new SecurityException("HTTP 403: " + resp.StatusDescription);
                }
                throw new WebException(resp.StatusDescription, null, WebExceptionStatus.UnknownError, resp);
            }
            return resp.GetResponseStream();
        }

        public WebRequestor WithTimeout(int timeoutInMilliseconds)
        {
            _request.Timeout = timeoutInMilliseconds;
            return this;
        }

        private HttpWebRequest _request;
    }
}
