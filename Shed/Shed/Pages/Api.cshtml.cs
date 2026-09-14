using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Shed.Services;
using Shed.Data;
using OpensourceLab.FileStorage.ServedServices;
using System.Security.Claims;

namespace Shed.Pages
{
    public class ApiModel : PageModel
    {
        private readonly IServeAcl _serveAcl;
        private readonly VanillaDbContext _dbContext;
        private readonly IStorageOwnerResolver _storageOwnerResolver;

        public ApiModel(IServeAcl serveAcl, VanillaDbContext dbContext, IStorageOwnerResolver storageOwnerResolver)
        {
            _serveAcl = serveAcl;
            _dbContext = dbContext;
            _storageOwnerResolver = storageOwnerResolver;
        }

        public IReadOnlyCollection<ApiAccessKeyRow> AccessKeys { get; private set; } = Array.Empty<ApiAccessKeyRow>();

        [BindProperty]
        public string AllowedPathsInput { get; set; } = "/public/*";

        [BindProperty]
        public bool CanReadInput { get; set; } = true;

        [BindProperty]
        public bool CanWriteInput { get; set; } = true;

        [BindProperty]
        public string RemarkInput { get; set; } = string.Empty;

        [BindProperty]
        public string AccessKey { get; set; } = string.Empty;

        [BindProperty]
        public string EditAllowedPathsInput { get; set; } = string.Empty;

        [BindProperty]
        public bool EditCanReadInput { get; set; } = true;

        [BindProperty]
        public bool EditCanWriteInput { get; set; } = true;

        [BindProperty]
        public string EditRemarkInput { get; set; } = string.Empty;

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            AccessKeys = await LoadAccessKeysAsync(cancellationToken);
        }

        public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
        {
            var userId = await GetCurrentUserIdAsync(cancellationToken);
            var created = await _serveAcl.CreateAccessKeyAsync(new CreateAccessKeyRequest(userId, ParsePaths(AllowedPathsInput)), cancellationToken);
            await UpsertAccessKeyOptionsAsync(created.Key, CanReadInput, CanWriteInput, RemarkInput, cancellationToken);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdatePathsAsync(CancellationToken cancellationToken)
        {
            var userId = await GetCurrentUserIdAsync(cancellationToken);
            await _serveAcl.UpdateAllowedPathsAsync(new UpdateAllowedPathsRequest(AccessKey, userId, ParsePaths(EditAllowedPathsInput)), cancellationToken);
            await UpsertAccessKeyOptionsAsync(AccessKey, EditCanReadInput, EditCanWriteInput, EditRemarkInput, cancellationToken);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRevokeAsync(CancellationToken cancellationToken)
        {
            await _serveAcl.RevokeAccessKeyAsync(AccessKey, cancellationToken);
            return RedirectToPage();
        }

        private static IReadOnlyCollection<string> ParsePaths(string input)
        {
            return input
                .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private async Task<IReadOnlyCollection<ApiAccessKeyRow>> LoadAccessKeysAsync(CancellationToken cancellationToken)
        {
            var userId = await GetCurrentUserIdAsync(cancellationToken);
            var keys = await _dbContext.AccessKeys
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.UpdatedAt)
                .Select(x => new ApiAccessKeyRow(
                    x.Key,
                    x.UserId,
                    x.FilePathWildCards.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    x.CreatedAt,
                    x.UpdatedAt,
                    EF.Property<bool>(x, "CanRead"),
                    EF.Property<bool>(x, "CanWrite"),
                    EF.Property<string>(x, "Remark") ?? string.Empty))
                .ToListAsync(cancellationToken);

            if (keys.Count == 0)
            {
                return Array.Empty<ApiAccessKeyRow>();
            }

            return keys;
        }

        private async Task UpsertAccessKeyOptionsAsync(string accessKey, bool canRead, bool canWrite, string remark, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(accessKey))
            {
                return;
            }

            var userId = await GetCurrentUserIdAsync(cancellationToken);
            var entity = await _dbContext.AccessKeys.FirstOrDefaultAsync(x => x.Key == accessKey && x.UserId == userId, cancellationToken);
            if (entity is null)
            {
                return;
            }

            // Detach the original instance before creating an updated copy (avoids EF Core tracking conflict)
            _dbContext.Entry(entity).State = EntityState.Detached;

            var updated = entity with { UpdatedAt = DateTime.UtcNow };
            var entry = _dbContext.Attach(updated);

            entry.Property("CanRead").CurrentValue = canRead;
            entry.Property("CanWrite").CurrentValue = canWrite;
            entry.Property("Remark").CurrentValue = string.IsNullOrWhiteSpace(remark)
                ? $"full-access-{Guid.NewGuid():N}"
                : remark.Trim();

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task<string> GetCurrentUserIdAsync(CancellationToken cancellationToken)
        {
            var resolution = await _storageOwnerResolver.ResolveAsync(User, cancellationToken);
            return resolution.UserId;
        }

        public record ApiAccessKeyRow(
            string Key,
            string UserId,
            IReadOnlyCollection<string> AllowedPaths,
            DateTime CreatedAt,
            DateTime UpdatedAt,
            bool CanRead,
            bool CanWrite,
            string Remark);
    }
}


