using System;
using System.Collections.Generic;

using Amazon.Lambda.Core;
using SignalFx.LambdaWrapper.AspNetCoreServer;
using SignalFx.Tracing.Configuration;

namespace SignalFx.LambdaWrapper
{
    /// <summary>
    /// This type handles the configuration for telemetry under AWS Lambda.
    /// It must be the first to execute so it can properly set the environment
    /// variables of the process prior to starting the SignalFx tracer.
    /// This allows it to set the proper settings for AWS Lambda, this is
    /// critial because some of the Tracer defaults are not appropriated 
    /// when wrapping a Lambda function.
    /// </summary>
    public static class TelemetryConfiguration
    {
        static TelemetryConfiguration()
        {
            Init();
        }

        public static bool MetricsEnabled { get; internal set; }
        public static bool TracingEnabled { get; internal set; }
        public static bool DebugEnabled { get; internal set; }
        public static bool ContextPropagationEnabled { get; internal set; }

        /// <summary>
        /// Perform the initializations according to the environment variables
        /// and proper defaults.
        /// </summary>
        /// <remarks>
        /// In principle could have been done all in the static constructor but
        /// a separate method makes easier to test it.
        /// </remarks>
        internal static void Init()
        {
            if (TracingEnabled = GetBoolEnvVar(ConfigurationKeys.TraceEnabled, true))
            {
                // Set proper defaults for AWS Lambda before the tracer is instantiated.
                TrySetEnvVar(ConfigurationKeys.SynchronousSend, "true");
                TrySetEnvVar(ConfigurationKeys.FileLogEnabled, "false");
                TrySetEnvVar(ConfigurationKeys.StdoutLogEnabled, "true");

                if (TryGetEnvVar("AWS_LAMBDA_FUNCTION_NAME", out string lambdaFnName))
                {
                    TrySetEnvVar(ConfigurationKeys.ServiceName, lambdaFnName);
                }
            }

            if (MetricsEnabled = GetBoolEnvVar("SIGNALFX_METRICS_ENABLED", false))
            {
                // TODO: Adapt common settings from Tracing so user can avoid double config.
            }

            if (DebugEnabled = GetBoolEnvVar(ConfigurationKeys.DebugEnabled, false))
            {
                LogAllEnvVars();
            }
        }

        internal static void TryAdaptTracingSettingsToMetrics()
        {
            if (TryGetEnvVar(ConfigurationKeys.SignalFxAccessToken, out var accessToken))
            {
                TrySetEnvVar(HttpClientWrapper.SignalFxAuthTokenEnvVar, accessToken);
            }

            if (TryGetEnvVar(ConfigurationKeys.EndpointUrl, out var uriString) &&
                Uri.TryCreate(uriString, UriKind.Absolute, out var tracingUri))
            {
                TrySetEnvVar(HttpClientWrapper.SignalFxIngestSchemeEnvVar, tracingUri.Scheme);
                TrySetEnvVar(HttpClientWrapper.SignalFxIngestHostEnvVar, tracingUri.Scheme);
                TrySetEnvVar(HttpClientWrapper.SignalFxIngestPortEnvVar, tracingUri.Port.ToString());
            }
        }

        internal static bool GetBoolEnvVar(string variable, bool defaultValue)
        {
            if (!TryGetEnvVar(variable, out string content))
            {
                return defaultValue;
            }

            if (long.TryParse(content, out var longVal))
            {
                return longVal != 0;
            }

            if (bool.TryParse(content, out var boolVal))
            {
                return boolVal;
            }

            LambdaLogger.Log($"[Warning] Env var {variable} has invalid value \"{content}\" using the default value ({defaultValue}){Environment.NewLine}");
            return defaultValue;
        }

        internal static bool TryGetEnvVar(string variable, out string content)
        {
            content = Environment.GetEnvironmentVariable(variable);
            if (content == null)
            {
                return false;
            }

            return true;
        }

        internal static bool TrySetEnvVar(string variable, string value)
        {
            if (Environment.GetEnvironmentVariable(variable) != null)
            {
                return false;
            }

            Environment.SetEnvironmentVariable(variable, value);
            return true;
        }

        internal static void LogAllEnvVars()
        {
            var envVarDict = Environment.GetEnvironmentVariables();
            var envVarKeys = envVarDict.Keys;
            var envVars = new List<string>(envVarKeys.Count);
            foreach (var key in envVarKeys)
            {
                envVars.Add((string)key);
            }

            // Typically it is no necessary to be that careful but trying to prevent
            // accidental disclosure from env vars captured to logs.
            var readctionKeys = new[] { "access", "secret", "token", "password", "pwd", "auth" };
            envVars.Sort();
            foreach (var variable in envVars)
            {
                var varValue = envVarDict[variable];
                foreach (var fragment in readctionKeys)
                {
                    if (variable.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        varValue = "<redacted>";
                        break;
                    }
                }

                LambdaLogger.Log($"[DBG] env.var {variable}={varValue}{Environment.NewLine}");
            }
        }
    }
}
