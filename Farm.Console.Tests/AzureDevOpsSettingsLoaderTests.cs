using Xunit;

namespace Farm.Console.Tests;

public sealed class AzureDevOpsSettingsLoaderTests
{
    [Fact]
    public void LoadFromEnvironment_rejects_missing_variables()
    {
        var names = new[]
        {
            "FARM_AZURE_DEVOPS_ORGANIZATION_URL",
            "FARM_AZURE_DEVOPS_PROJECT",
            "FARM_AZURE_DEVOPS_PAT"
        };
        var values = names.Select(Environment.GetEnvironmentVariable).ToArray();

        try
        {
            foreach (var name in names)
            {
                Environment.SetEnvironmentVariable(name, null);
            }

            var exception = Assert.Throws<InvalidOperationException>(AzureDevOpsSettingsLoader.LoadFromEnvironment);

            Assert.Contains("required", exception.Message);
        }
        finally
        {
            for (var index = 0; index < names.Length; index++)
            {
                Environment.SetEnvironmentVariable(names[index], values[index]);
            }
        }
    }
}