using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpensourceLab.FileStorage.ServedServices;
using System.Security.Claims;
using Shed.Services;

namespace Shed.Pages
{
    public class UserSettingsModel : PageModel
    {
        private readonly IServeUserPreferences _serveUserPreferences;
        private readonly IStorageOwnerResolver _storageOwnerResolver;

        public UserSettingsModel(IServeUserPreferences serveUserPreferences, IStorageOwnerResolver storageOwnerResolver)
        {
            _serveUserPreferences = serveUserPreferences;
            _storageOwnerResolver = storageOwnerResolver;
        }

        [BindProperty]
        public bool ShowHiddenFiles { get; set; }

        [BindProperty]
        public string TranslatedText { get; set; } = string.Empty;

        [BindProperty]
        public string ThumbnailLogoUrl { get; set; } = string.Empty;

        [BindProperty]
        public string FontStyle { get; set; } = "Ubuntu";

        [BindProperty]
        public string Theme { get; set; } = "ubuntu";

        [BindProperty]
        public string PrimaryColor { get; set; } = "#E95420";

        public DateTime LastUpdatedAt { get; private set; }

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            var settings = await _serveUserPreferences.GetUserSettingsAsync(await GetCurrentUserIdAsync(cancellationToken), cancellationToken);
            MapFrom(settings);
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
        {
            await _serveUserPreferences.UpsertUserSettingsAsync(
                new UserSettingsUpsertRequest(await GetCurrentUserIdAsync(cancellationToken), ShowHiddenFiles, TranslatedText, ThumbnailLogoUrl, FontStyle, Theme, PrimaryColor),
                cancellationToken);

            return RedirectToPage();
        }

        private void MapFrom(UserSettingsDto dto)
        {
            ShowHiddenFiles = dto.ShowHiddenFiles;
            TranslatedText = dto.TranslatedText;
            ThumbnailLogoUrl = dto.ThumbnailLogoUrl;
            FontStyle = dto.FontStyle;
            Theme = dto.Theme;
            PrimaryColor = dto.PrimaryColor;
            LastUpdatedAt = dto.UpdatedAt;
        }

        private async Task<string> GetCurrentUserIdAsync(CancellationToken cancellationToken)
        {
            var resolution = await _storageOwnerResolver.ResolveAsync(User, cancellationToken);
            return resolution.UserId;
        }
    }
}


