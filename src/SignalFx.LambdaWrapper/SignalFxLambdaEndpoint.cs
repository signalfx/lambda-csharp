using System;

namespace SignalFx.LambdaWrapper
{
    public class SignalFxLambdaEndpoint
    {
        public static readonly string DEFAULT_SCHEME = "https";
        public static readonly string DEFAULT_HOST_NAME = "ingest.us0.signalfx.com";
        public static readonly int DEFAULT_PORT = 443;

        private const string API_HOST_NAME = "SIGNALFX_API_HOSTNAME";
        private const string API_PORT = "SIGNALFX_API_PORT";
        private const string API_SCHEME = "SIGNALFX_API_SCHEME";

        /**
         * API protocol scheme - https or http
         */
        private readonly string scheme;

        /**
         * API hostname
         */
        private readonly string hostname;

        /**
         * TCP port
         */
        private readonly int port;

        public SignalFxLambdaEndpoint() : this(GetDefaultScheme(), GetDefaultHostname(), GetDefaultPort())
        {

        }

        public SignalFxLambdaEndpoint(string hostname, int port) : this(GetDefaultScheme(), hostname, port)
        {

        }

        public SignalFxLambdaEndpoint(string scheme, string hostname, int port)
        {
            this.scheme = scheme;
            this.hostname = hostname;
            this.port = port;
        }

        private static string GetDefaultScheme()
        {
            string scheme = Environment.GetEnvironmentVariable(API_SCHEME);
            return string.IsNullOrEmpty(scheme) ? DEFAULT_SCHEME : scheme;
        }

        private static string GetDefaultHostname()
        {
            string host = Environment.GetEnvironmentVariable(API_HOST_NAME);
            return string.IsNullOrEmpty(host) ? DEFAULT_HOST_NAME : host;
        }

        private static int GetDefaultPort()
        {
            string port = Environment.GetEnvironmentVariable(API_PORT);
            return string.IsNullOrEmpty(port) ? DEFAULT_PORT : Int32.Parse(port);
        }

        public string getScheme()
        {
            return scheme;
        }

        public int getPort()
        {
            return port;
        }

        public string getHostname()
        {
            return hostname;
        }

        public override string ToString()
        {
            return getScheme() + "://" + getHostname() + ":" + getPort().ToString();
        }
    }
}
