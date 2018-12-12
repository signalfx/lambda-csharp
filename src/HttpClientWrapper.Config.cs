using System;
using Amazon.Lambda.Core;

namespace SignalFx.LambdaWrapper
{
    public partial class HttpClientWrapper
    {
        private const string SignalFxAuthTokenEnvVar = "SIGNALFX_AUTH_TOKEN";
        private const string SignalFxIngestHostEnvVar = "SIGNALFX_API_HOSTNAME";
        private const string SignalFxIngestPortEnvVar = "SIGNALFX_API_PORT";
        private const string SignalFxIngestSchemeEnvVar = "SIGNALFX_API_SCHEME";
        private const string RequestTimeoutEnvVar = "SIGNALFX_SEND_TIMEOUT";
        private const string ConnectionLeaseTimeoutEnvVar = "CONNECTION_LEASE_TIMEOUT";
        private const string DnsRefreshTimeoutEnvVar = "DNS_REFRESH_TIMEOUT";

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
                        Host = GetStringEnvironmentVariable(SignalFxIngestHostEnvVar, "pops.signalfx.com"),
                        Port = GetIntEnvironmentVariable(SignalFxIngestPortEnvVar, 443)
                    }.Uri;
                }
                catch (Exception exception)
                {
                    LambdaLogger.Log($"[Error] constructing URI.{Environment.NewLine}{exception}{Environment.NewLine}");
                }
                return uri;
            }

            private static string GetStringEnvironmentVariable(string variable, string defaultValue = null)
            {
                var stringValue = Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.Process);
                if (!string.IsNullOrWhiteSpace(stringValue)) return stringValue;
                if (defaultValue == null)
                {
                    LambdaLogger.Log($"[Error] environment variable {variable} is not set.{Environment.NewLine}");
                }
                else
                {
                    stringValue = defaultValue;
                    LambdaLogger.Log($"[Warning] environment variable {variable} is not set. Using default value instead.{Environment.NewLine}");
                }
                return stringValue;
            }

            private static double GetDoubleEnvironmentVariable(string variable, double defaultValue)
            {
                if (double.TryParse(Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.Process), out var doubleValue))
                {
                    return doubleValue;
                }
                LambdaLogger.Log($"[Warning] environment variable {variable} is not set. Using default value of {defaultValue} instead.{Environment.NewLine}");
                return defaultValue;
            }

            private static int GetIntEnvironmentVariable(string variable, int defaultValue)
            {
                if (int.TryParse(Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.Process), out var intValue))
                {
                    return intValue;
                }
                LambdaLogger.Log($"[Warning] environment variable {variable} is not set. Using default value of {defaultValue} instead.{Environment.NewLine}");
                return defaultValue;
            }
        }

    }

}
