namespace Farm.Azure;

public sealed class AzureDevOpsSettings
{
    public required Uri OrganizationUrl { get; init; }

    public required string Project { get; init; }

    public required string PersonalAccessToken { get; init; }

    public void Validate()
    {
        if (OrganizationUrl is null || !OrganizationUrl.IsAbsoluteUri || OrganizationUrl.Scheme is not "https")
        {
            throw new InvalidOperationException("Azure DevOps organization URL must be an absolute HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(Project))
        {
            throw new InvalidOperationException("Azure DevOps project is required.");
        }

        if (string.IsNullOrWhiteSpace(PersonalAccessToken))
        {
            throw new InvalidOperationException("Azure DevOps personal access token is required.");
        }
    }
}