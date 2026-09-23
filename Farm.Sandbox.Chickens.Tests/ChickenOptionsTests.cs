using Xunit;

namespace Farm.Sandbox.Chickens.Tests;

public sealed class ChickenOptionsTests
{
    [Fact]
    public void Safe_process_environment_loads_explicit_values()
    {
        var previous = Environment.GetEnvironmentVariable("FARM_CHICKENS_SAFE_PROCESS_ENV_JSON");
        try
        {
            Environment.SetEnvironmentVariable("FARM_CHICKENS_SAFE_PROCESS_ENV_JSON", "{\"CI\":\"true\",\"FEATURE_FLAG\":\"enabled\"}");

            var environment = ChickenOptions.LoadSafeProcessEnvironment();

            Assert.Equal("true", environment["CI"]);
            Assert.Equal("enabled", environment["FEATURE_FLAG"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FARM_CHICKENS_SAFE_PROCESS_ENV_JSON", previous);
        }
    }

    [Fact]
    public void Push_defaults_disabled_and_requires_explicit_true_to_enable()
    {
        var artifacts = Environment.GetEnvironmentVariable("FARM_CHICKENS_ARTIFACTS_PATH");
        var enablePush = Environment.GetEnvironmentVariable("FARM_CHICKENS_ENABLE_PUSH");
        try
        {
            Environment.SetEnvironmentVariable("FARM_CHICKENS_ARTIFACTS_PATH", Path.GetTempPath());
            Environment.SetEnvironmentVariable("FARM_CHICKENS_ENABLE_PUSH", null);
            Assert.False(ChickenOptions.FromEnvironment().EnablePush);

            Environment.SetEnvironmentVariable("FARM_CHICKENS_ENABLE_PUSH", "true");
            Assert.True(ChickenOptions.FromEnvironment().EnablePush);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FARM_CHICKENS_ARTIFACTS_PATH", artifacts);
            Environment.SetEnvironmentVariable("FARM_CHICKENS_ENABLE_PUSH", enablePush);
        }
    }
}