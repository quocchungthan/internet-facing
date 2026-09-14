using Fences.Models;
using Microsoft.Extensions.Options;

namespace Fences.Services;

public sealed class AppCatalog(IOptionsMonitor<IdentityAppOptions> optionsMonitor) : IAppCatalog
{
    public IReadOnlyList<IdentityNavigationApp> GetApps() => optionsMonitor.CurrentValue.Apps;
}
