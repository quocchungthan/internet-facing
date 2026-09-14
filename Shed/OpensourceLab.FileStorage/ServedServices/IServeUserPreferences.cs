using OpensourceLab.FileStorage.Domain;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpensourceLab.FileStorage.ServedServices
{
    public interface IServeUserPreferences
    {
        Task<IEnumerable<UserFeatureFlag>> GetUserPreferencesAsync(CancellationToken cancellationToken);
        Task<UserSettingsDto> GetUserSettingsAsync(string userId, CancellationToken cancellationToken);
        Task UpsertUserSettingsAsync(UserSettingsUpsertRequest request, CancellationToken cancellationToken);
        Task UpsertUserPreferenceAsync(UserFeatureFlag userFeatureFlag, CancellationToken cancellationToken);
    }
}
