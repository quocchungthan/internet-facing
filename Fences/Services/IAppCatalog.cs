using Fences.Models;

namespace Fences.Services;

public interface IAppCatalog
{
    IReadOnlyList<IdentityNavigationApp> GetApps();
}
