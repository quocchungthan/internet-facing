using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Shed.Services;

namespace Shed.Pages
{
    public class ShareModel : PageModel
    {
        private readonly ShareLinkService _shareLinkService;
        private readonly ILogger<ShareModel> _logger;
        private readonly IStorageOwnerResolver _storageOwnerResolver;

        public ShareModel(
            ShareLinkService shareLinkService,
            ILogger<ShareModel> logger,
            IStorageOwnerResolver storageOwnerResolver)
        {
            _shareLinkService = shareLinkService;
            _logger = logger;
            _storageOwnerResolver = storageOwnerResolver;
        }

        [BindProperty]
        public string TargetPath { get; set; } = string.Empty;

        [BindProperty]
        public int ExpirationMinutes { get; set; } = 1440;

        public IReadOnlyCollection<ShareLinkDto> ShareLinks { get; private set; } = Array.Empty<ShareLinkDto>();

        public async Task OnGetAsync([FromQuery(Name = "targetPath")] string? targetPath, CancellationToken cancellationToken)
        {
            // Prefill the TargetPath if provided from query parameter
            if (!string.IsNullOrEmpty(targetPath))
            {
                TargetPath = targetPath;
            }

            ShareLinks = (await _shareLinkService.GetShareLinksAsync(await GetCurrentUserIdAsync(cancellationToken), cancellationToken)).ToList();
        }

        public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(TargetPath))
            {
                ModelState.AddModelError("TargetPath", "Path is required");
                return Page();
            }

            if (ExpirationMinutes <= 0)
            {
                ModelState.AddModelError("ExpirationMinutes", "Expiration must be greater than 0");
                return Page();
            }

            try
            {
                var link = await _shareLinkService.CreateShareLinkAsync(
                    await GetCurrentUserIdAsync(cancellationToken),
                    TargetPath,
                    ExpirationMinutes,
                    cancellationToken);

                TempData["SuccessMessage"] = $"Share link created: {link.AccessKey}";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating share link");
                ModelState.AddModelError("", "Failed to create share link");
                return Page();
            }
        }

        public async Task<IActionResult> OnPostRevokeAsync(
            [FromBody] RevokeRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.AccessKey))
                return BadRequest("Access key required");

            try
            {
                await _shareLinkService.RevokeShareLinkAsync(
                    request.AccessKey,
                    await GetCurrentUserIdAsync(cancellationToken),
                    cancellationToken);

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking share link");
                return StatusCode(500, new { error = "Failed to revoke share link" });
            }
        }

        public async Task<IActionResult> OnPostGetLinksAsync(
            [FromBody] GetLinksRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.TargetPath))
                return BadRequest("Target path required");

            try
            {
                var links = await _shareLinkService.GetShareLinksForPathAsync(
                    await GetCurrentUserIdAsync(cancellationToken),
                    request.TargetPath,
                    cancellationToken);

                return new JsonResult(new { shareLinks = links });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting share links");
                return StatusCode(500, new { error = "Failed to get share links" });
            }
        }

        public async Task<IActionResult> OnPostCleanupAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _shareLinkService.CleanupExpiredLinksAsync(await GetCurrentUserIdAsync(cancellationToken), cancellationToken);
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired links");
                return StatusCode(500, new { error = "Cleanup failed" });
            }
        }

        private async Task<string> GetCurrentUserIdAsync(CancellationToken cancellationToken)
        {
            var resolution = await _storageOwnerResolver.ResolveAsync(User, cancellationToken);
            return resolution.UserId;
        }

        public class RevokeRequest
        {
            public string AccessKey { get; set; } = string.Empty;
        }

        public class GetLinksRequest
        {
            public string TargetPath { get; set; } = string.Empty;
        }
    }
}


