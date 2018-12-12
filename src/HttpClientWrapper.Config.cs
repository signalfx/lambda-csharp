using System;
using Amazon.Lambda.Core;

namespace SignalFx.LambdaWrapper
{
    public partial class HttpClientWrapper
    {
        private static string SignalFxAuthTokenEnvVar      = "SIGNALFX_AUTH_TOKEN";
        private static string SignalFxIngestHostEnvVar     = "SIGNALFX_API_HOSTNAME";
        private static string SignalFxIngestPortEnvVar     = "SIGNALFX_API_PORT";
        private static string SignalFxIngestSchemeEnvVar   = "SIGNALFX_API_SCHEME";
        private static string RequestTimeoutEnvVar         = "SIGNALFX_SEND_TIMEOUT";
        private static string ConnectionLeaseTimeoutEnvVar = "CONNECTION_LEASE_TIMEOUT";
        private static string DnsRefreshTimeoutEnvVar      = "DNS_REFRESH_TIMEOUT";

        internal static class Config
        {
            internal static string AuthToken { get => GetStringEnvironmentVariable(SignalFxAuthTokenEnvVar); }
            internal static Uri BaseAddress { get => GetBaseAddress(); }
            internal static readonly Uri DataPointIngestPath = new Uri("v2/datapoint", UriKind.Relative);
            internal static readonly string AuthTokenHeaderName = "X-Sf-Token";
            internal static TimeSpan Timeout { get => TimeSpan.FromMilliseconds(GetDoubleEnvironmentVariable(RequestTimeoutEnvVar, 2000)); }
            internal static TimeSpan ConnectionLeaseTimeout { get => TimeSpan.FromMilliseconds(GetDoubleEnvironmentVariable(ConnectionLeaseTimeoutEnvVar, 5000)); }
            internal static TimeSpan DnsRefreshTimeout { get => TimeSpan.FromMilliseconds(GetDoubleEnvironmentVariable(DnsRefreshTimeoutEnvVar, 5000)); }

            private static Uri GetBaseAddress()
            {
                Uri uri = null;
                try
                {
                    uri = new UriBuilder
                    {
                        Scheme = GetStringEnvironmentVariable(SignalFxIngestSchemeEnvVar, "https"),
                        Host = GetStringEnvironmentVariable(SignalFxIngestHostEnvVar, "pops.signalfx.com"),
                        Port = GetIntEnvironmentVariable(SignalFxIngestPortEnvVar, 443)
                    }.Uri;
                }
                catch (Exception exception)
                {
                    LambdaLogger.Log($"[Error] contructing URI.{Environment.NewLine}{exception}{Environment.NewLine}");
                }
                return uri;
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

            private static int GetIntEnvironmentVariable(string variable, int defaultValue)
            {
                if (int.TryParse(Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.Process), out int intValue))
                {
                    return intValue;
                }
                else
                {
                    LambdaLogger.Log($"[Warning] environment variable {variable} is not set. Using default value of {defaultValue} instead.{Environment.NewLine}");
                    return defaultValue;
                }
            }
        }

    }

}
