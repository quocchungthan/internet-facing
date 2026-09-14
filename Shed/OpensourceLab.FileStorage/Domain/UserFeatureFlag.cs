using OpensourceLab.FileStorage.Meta;
using System;

namespace OpensourceLab.FileStorage.Domain
{
    public record UserFeatureFlag (
        string UserId,
        FeatureName Feature,
        bool IsEnabled,
        DateTime CreatedAt,
        DateTime UpdatedAt
    );
}
