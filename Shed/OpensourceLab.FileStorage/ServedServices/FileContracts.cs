using OpensourceLab.FileStorage.Meta;
using System;
using System.Collections.Generic;

namespace OpensourceLab.FileStorage.ServedServices
{
    public record FileCardDto(
        Guid Id,
        string Path,
        string ContentType,
        string ThumbnailUrl,
        bool IsPublic,
        OwnerType OwnerType,
        string OwnerId,
        AccessLevel PermissionLevel,
        DateTime UpdatedAt
    );

    public record FilePermissionUpsertRequest(
        Guid FileItemId,
        OwnerType OwnerType,
        string OwnerId,
        AccessLevel PermissionLevel
    );

    public record CreateAccessKeyRequest(
        string UserId,
        IReadOnlyCollection<string> AllowedPaths
    );

    public record UpdateAllowedPathsRequest(
        string AccessKey,
        string UserId,
        IReadOnlyCollection<string> AllowedPaths
    );

    public record AccessKeyViewDto(
        string Key,
        string UserId,
        IReadOnlyCollection<string> AllowedPaths,
        DateTime CreatedAt,
        DateTime UpdatedAt
    );

    public record UserSettingsDto(
        string UserId,
        bool ShowHiddenFiles,
        string TranslatedText,
        string ThumbnailLogoUrl,
        string FontStyle,
        string Theme,
        string PrimaryColor,
        DateTime UpdatedAt
    );

    public record UserSettingsUpsertRequest(
        string UserId,
        bool ShowHiddenFiles,
        string TranslatedText,
        string ThumbnailLogoUrl,
        string FontStyle,
        string Theme,
        string PrimaryColor
    );
}
