using System;

namespace SampleLambdaFunctions
{
    public class LambdaEnvVarFixture : IDisposable
    {
        public const string ExecutionEnvironment = "TestLambdaEnv";

        public LambdaEnvVarFixture()
        {
            Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", ExecutionEnvironment);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("AWS_EXECUTION_ENV", null);
        }
    }
}
