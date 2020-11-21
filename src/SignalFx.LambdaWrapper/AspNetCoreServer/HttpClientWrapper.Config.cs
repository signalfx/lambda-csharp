using System;

using Amazon.Lambda.Core;

namespace SignalFx.LambdaWrapper.AspNetCoreServer
{
    public partial class HttpClientWrapper
    {
        public const string SignalFxAuthTokenEnvVar = "SIGNALFX_AUTH_TOKEN";
        public const string SignalFxIngestHostEnvVar = "SIGNALFX_API_HOSTNAME";
        public const string SignalFxIngestPortEnvVar = "SIGNALFX_API_PORT";
        public const string SignalFxIngestSchemeEnvVar = "SIGNALFX_API_SCHEME";
        public const string RequestTimeoutEnvVar = "SIGNALFX_SEND_TIMEOUT";
        public const string ConnectionLeaseTimeoutEnvVar = "CONNECTION_LEASE_TIMEOUT";
        public const string DnsRefreshTimeoutEnvVar = "DNS_REFRESH_TIMEOUT";

        private static class Config
        {
            internal static string AuthToken => GetStringEnvironmentVariable(SignalFxAuthTokenEnvVar);
            internal static Uri BaseAddress => GetBaseAddress();
            internal static readonly Uri DataPointIngestPath = new Uri("v2/datapoint", UriKind.Relative);
            internal const string AuthTokenHeaderName = "X-Sf-Token";
            internal static TimeSpan Timeout => TimeSpan.FromMilliseconds(GetDoubleEnvironmentVariable(RequestTimeoutEnvVar, 2000));
            internal static TimeSpan ConnectionLeaseTimeout => TimeSpan.FromMilliseconds(GetDoubleEnvironmentVariable(ConnectionLeaseTimeoutEnvVar, 5000));
            internal static TimeSpan DnsRefreshTimeout => TimeSpan.FromMilliseconds(GetDoubleEnvironmentVariable(DnsRefreshTimeoutEnvVar, 5000));

            private static Uri GetBaseAddress()
            {
                Uri uri = null;
                try
                {
                    uri = new UriBuilder
                    {
                        Scheme = GetStringEnvironmentVariable(SignalFxIngestSchemeEnvVar, "https"),
                        Host = GetStringEnvironmentVariable(SignalFxIngestHostEnvVar, "ingest.us0.signalfx.com"),
                        Port = GetIntEnvironmentVariable(SignalFxIngestPortEnvVar, 443)
                    }.Uri;
                }
                catch (Exception exception)
                {
                    LambdaLogger.Log($"[Error] {typeof(Config).FullName}: constructing URI.{Environment.NewLine}{exception}{Environment.NewLine}");
                }
                return uri;
            }

            private static string GetStringEnvironmentVariable(string variable, string defaultValue = null)
            {
                var stringValue = Environment.GetEnvironmentVariable(variable);
                if (!string.IsNullOrWhiteSpace(stringValue)) return stringValue;
                if (defaultValue == null)
                {
                    LambdaLogger.Log($"[Error] {typeof(Config).FullName}: environment variable {variable} is not set.{Environment.NewLine}");
                }
                else
                {
                    LambdaLogger.Log($"[Warning] {typeof(Config).FullName}: environment variable {variable} is not set. Using default value {defaultValue}.{Environment.NewLine}");
                    stringValue = defaultValue;
                }
                return stringValue;
            }

            private static double GetDoubleEnvironmentVariable(string variable, double defaultValue)
            {
                if (double.TryParse(Environment.GetEnvironmentVariable(variable), out var doubleValue))
                {
                    return doubleValue;
                }
                LambdaLogger.Log($"[Warning] {typeof(Config).FullName}: environment variable {variable} is not set. Using default value {defaultValue}.{Environment.NewLine}");
                return defaultValue;
            }

            private static int GetIntEnvironmentVariable(string variable, int defaultValue)
            {
                if (int.TryParse(Environment.GetEnvironmentVariable(variable), out var intValue))
                {
                    return intValue;
                }
                LambdaLogger.Log($"[Warning] {typeof(Config).FullName}: environment variable {variable} is not set. Using default value {defaultValue}.{Environment.NewLine}");
                return defaultValue;
            }
        }

    }

}
