using System;

using Xunit;

namespace SignalFx.LambdaWrapper.Tests
{
    public class TelemetryConfigurationTests
    {
        [Fact]
        public void TryGetEnvVar()
        {
            var variable = Guid.NewGuid().ToString();
            try
            {
                Assert.False(TelemetryConfiguration.TryGetEnvVar(variable, out var content));
                Assert.Null(content);

                Environment.SetEnvironmentVariable(variable, "test");
                Assert.True(TelemetryConfiguration.TryGetEnvVar(variable, out content));
                Assert.Equal("test", content);
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, null);
            }
        }

        [Theory]
        [InlineData(null, false, false)]
        [InlineData(null, true, true)]
        [InlineData("", false, false)]
        [InlineData("", true, true)]
        [InlineData("1", false, true)]
        [InlineData("1", true, true)]
        [InlineData("true", false, true)]
        [InlineData("TRUE", true, true)]
        [InlineData("0", false, false)]
        [InlineData("0", true, false)]
        [InlineData("false", false, false)]
        [InlineData("False", true, false)]
        public void GetBoolEnvVar(string varValue, bool defaultValue, bool expectedValue)
        {
            var variable = Guid.NewGuid().ToString();
            try
            {
                Environment.SetEnvironmentVariable(variable, varValue);
                var actualValue = TelemetryConfiguration.GetBoolEnvVar(variable, defaultValue);
                Assert.Equal(expectedValue, actualValue);
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, null);
            }
        }

        [Fact]
        public void LogAllEnvVars()
        {
            // Just to ensure no crashes.
            TelemetryConfiguration.LogAllEnvVars();
        }
    }
}
