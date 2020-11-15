using System;

namespace TestHelpers
{
    public class LambdaEnvVarFixture : IDisposable
    {
        public const string ExecutionEnvironment = "TestLambdaEnv";

        public LambdaEnvVarFixture()
        {
            // In order to tests to work a few env vars are required:
            Environment.SetEnvironmentVariable("SIGNALFX_ENDPOINT_URL", "http://localhost:9080/v1/traces");
            Environment.SetEnvironmentVariable("SIGNALFX_ACCESS_TOKEN", "bogustoken");

            Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", ExecutionEnvironment);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", null);
        }
    }
}
