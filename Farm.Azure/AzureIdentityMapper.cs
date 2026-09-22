using AzureIdentity = Microsoft.VisualStudio.Services.Identity.Identity;
using AzureIdentitySelf = Microsoft.VisualStudio.Services.Identity.IdentitySelf;
using CoreGroup = Farm.Core.Domain.Group;
using CoreIdentity = Farm.Core.Domain.Identity;

namespace Farm.Azure;

public static class AzureIdentityMapper
{
    public static CoreIdentity ToDomain(AzureIdentitySelf self)
    {
        ArgumentNullException.ThrowIfNull(self);

        return new CoreIdentity(self.Id.ToString(), self.DisplayName, self.AccountName);
    }

    public static CoreGroup ToGroup(AzureIdentity group)
    {
        ArgumentNullException.ThrowIfNull(group);

        return new CoreGroup(group.Id.ToString(), group.DisplayName, []);
    }
}
