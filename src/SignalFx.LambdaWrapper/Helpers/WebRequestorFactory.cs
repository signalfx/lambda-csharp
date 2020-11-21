using System.Collections.Generic;

namespace SignalFx.LambdaWrapper.Helpers
{
    public class WebRequestorFactory : IWebRequestorFactory
    {
        public WebRequestorFactory()
        {
            _headers = new List<KeyValuePair<string, string>>();
        }

        public WebRequestorFactory WithUri(string uri)
        {
            _uri = uri;
            return this;
        }

        public WebRequestorFactory WithMethod(string method)
        {
            _method = method;
            return this;
        }

        public WebRequestorFactory WithContentType(string contentType)
        {
            _contentType = contentType;
            return this;
        }

        public WebRequestorFactory WithHeader(string header, string headerValue)
        {
            _headers.Add(new KeyValuePair<string, string>(header, headerValue));
            return this;
        }

        public WebRequestorFactory WithTimeout(int timeoutInMilliseconds)
        {
            _timeout = timeoutInMilliseconds;
            return this;
        }

        public IWebRequestor GetRequestor()
        {
            var req = new WebRequestor(_uri)
                .WithMethod(_method)
                .WithContentType(_contentType);

            foreach (var header in _headers)
            {
                req.WithHeader(header.Key, header.Value);
            }

            return req;
        }

        private string _uri;
        private string _method;
        private string _contentType;
        private int _timeout;
        private List<KeyValuePair<string, string>> _headers;
    }
}
