using BusinessLayer.DTOs;
using BusinessLayer.Models;
using DataLayer.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BusinessLayer.Services
{
    public interface IFoundItemService
    {
        // Finder actions
        Task<ResponceApi<FoundItemDetailDTO>> CreateAsync(Guid userId, CreateFoundItemDTO dto);
        Task<ResponceApi<FoundItemDetailDTO>> UpdateAsync(Guid userId, Guid itemId, UpdateFoundItemDTO dto);
        Task<ResponceApi<string>> DeleteAsync(Guid userId, Guid itemId);

        // Public browsing
        Task<ResponceApi<PagedResult<FoundItemListDTO>>> GetPublishedAsync(FoundItemFilterDTO filter, Guid? currentUserId);
        Task<ResponceApi<FoundItemDetailDTO>> GetByIdAsync(Guid itemId, Guid? currentUserId);

        // My items
        Task<ResponceApi<PagedResult<FoundItemListDTO>>> GetMyItemsAsync(Guid userId, int page, int pageSize);

        // Claim flow
        Task<ResponceApi<ClaimAttemptDTO>> SubmitClaimAsync(Guid userId, SubmitClaimDTO dto);
        Task<ResponceApi<PagedResult<ClaimAttemptDTO>>> GetItemClaimsAsync(Guid adminOrOwnerId, Guid itemId, int page, int pageSize);

        // Admin
        Task<ResponceApi<string>> AdminReviewItemAsync(Guid adminId, AdminReviewItemDTO dto);
        Task<ResponceApi<string>> AdminReviewClaimAsync(Guid adminId, AdminReviewClaimDTO dto);
        Task<ResponceApi<PagedResult<FoundItemListDTO>>> AdminGetAllItemsAsync(FoundItemFilterDTO filter);
        Task<ResponceApi<AdminDashboardDTO>> AdminGetDashboardAsync();

        // Admin user management
        Task<ResponceApi<string>> AdminManageUserAsync(Guid adminId, ManageUserDTO dto);
        Task<ResponceApi<PagedResult<UserListDTO>>> AdminGetUsersAsync(string? search, bool? blocked, int page, int pageSize);
        Task<ResponceApi<string>> AdminDeleteItemAsync(Guid adminId, Guid itemId);
    }

    public class FoundItemService : IFoundItemService
    {
        private readonly DBContext _db;
        private readonly IDataHasher _dataHasher;
        private readonly INotificationService _notificationService;
        private readonly IEmailTemplateService _emailTemplateService;
        private readonly IDataCiphers _dataCiphers;
        private readonly CairoTimeService _cairoTime;

        public FoundItemService(DBContext db, IDataHasher dataHasher, INotificationService notificationService, IEmailTemplateService emailTemplateService, IDataCiphers dataCiphers, CairoTimeService cairoTime)
        {
            _db = db;
            _dataHasher = dataHasher;
            _notificationService = notificationService;
            _emailTemplateService = emailTemplateService;
            _dataCiphers = dataCiphers;
            _cairoTime = cairoTime;
        }

        // ----------------------------------------------------------------
        // CREATE
        // ----------------------------------------------------------------
        public async Task<ResponceApi<FoundItemDetailDTO>> CreateAsync(Guid userId, CreateFoundItemDTO dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return ResponceApi<FoundItemDetailDTO>.Fail("User not found.");
            if (user.Blocked) return ResponceApi<FoundItemDetailDTO>.Fail("Your account is blocked.");
            if (!user.Verified) return ResponceApi<FoundItemDetailDTO>.Fail("Your account must be verified before you can post items.");

            var item = new FoundItem
            {
                Id = Guid.NewGuid(),
                ReportedByUserId = userId,
                Title = dto.Title.Trim(),
                Description = dto.Description.Trim(),
                Category = dto.Category.Trim(),
                FoundLatitude = dto.FoundLatitude,
                FoundLongitude = dto.FoundLongitude,
                FoundAddress = dto.FoundAddress.Trim(),
                ImagesJson = JsonSerializer.Serialize(dto.ImageUrls),
                RewardPercentage = dto.RewardPercentage,
                EstimatedValueEGP = dto.EstimatedValueEGP,
                VerificationQuestion = dto.VerificationQuestion.Trim(),
                VerificationAnswerHash = _dataHasher.HashData(dto.VerificationAnswer.Trim().ToLowerInvariant()),
                Status = FoundItemStatus.PendingReview,
                FoundAt = dto.FoundAt,
                CreatedAt = DateTime.UtcNow
            };

            _db.FoundItems.Add(item);
            await _db.SaveChangesAsync();

            // Notify admins (all users with AccountType == "Admin")
            var admins = await _db.Users.Where(u => u.AccountType == "Admin").ToListAsync();
            foreach (var admin in admins)
            {
                await _notificationService.SendAsync(admin.UserId,
                    "New Found Item Pending Review",
                    $"A new item '{item.Title}' has been submitted and awaits your review.",
                    NotificationType.ItemApproved, item.Id);
            }

            return ResponceApi<FoundItemDetailDTO>.Ok(await MapToDetailDtoAsync(item, userId), "Item submitted for review successfully.");
        }

        // ----------------------------------------------------------------
        // UPDATE
        // ----------------------------------------------------------------
        public async Task<ResponceApi<FoundItemDetailDTO>> UpdateAsync(Guid userId, Guid itemId, UpdateFoundItemDTO dto)
        {
            var item = await _db.FoundItems.FirstOrDefaultAsync(i => i.Id == itemId);
            if (item == null) return ResponceApi<FoundItemDetailDTO>.Fail("Item not found.");
            if (item.ReportedByUserId != userId) return ResponceApi<FoundItemDetailDTO>.Fail("You do not have permission to edit this item.");
            if (item.Status == FoundItemStatus.Claimed) return ResponceApi<FoundItemDetailDTO>.Fail("Cannot edit a claimed item.");
            if (item.Status == FoundItemStatus.Blocked) return ResponceApi<FoundItemDetailDTO>.Fail("Cannot edit a blocked item.");

            if (dto.Title != null) item.Title = dto.Title.Trim();
            if (dto.Description != null) item.Description = dto.Description.Trim();
            if (dto.Category != null) item.Category = dto.Category.Trim();
            if (dto.FoundLatitude.HasValue) item.FoundLatitude = dto.FoundLatitude.Value;
            if (dto.FoundLongitude.HasValue) item.FoundLongitude = dto.FoundLongitude.Value;
            if (dto.FoundAddress != null) item.FoundAddress = dto.FoundAddress.Trim();
            if (dto.ImageUrls != null) item.ImagesJson = JsonSerializer.Serialize(dto.ImageUrls);
            if (dto.RewardPercentage.HasValue) item.RewardPercentage = dto.RewardPercentage.Value;
            if (dto.EstimatedValueEGP.HasValue) item.EstimatedValueEGP = dto.EstimatedValueEGP.Value;
            if (dto.VerificationQuestion != null) item.VerificationQuestion = dto.VerificationQuestion.Trim();
            if (dto.VerificationAnswer != null) item.VerificationAnswerHash = _dataHasher.HashData(dto.VerificationAnswer.Trim().ToLowerInvariant());

            // If it was rejected, editing resubmits it for review
            if (item.Status == FoundItemStatus.Rejected)
                item.Status = FoundItemStatus.PendingReview;

            await _db.SaveChangesAsync();

            return ResponceApi<FoundItemDetailDTO>.Ok(await MapToDetailDtoAsync(item, userId), "Item updated successfully.");
        }

        // ----------------------------------------------------------------
        // DELETE (soft: archive)
        // ----------------------------------------------------------------
        public async Task<ResponceApi<string>> DeleteAsync(Guid userId, Guid itemId)
        {
            var item = await _db.FoundItems.FirstOrDefaultAsync(i => i.Id == itemId);
            if (item == null) return ResponceApi<string>.Fail("Item not found.");
            if (item.ReportedByUserId != userId) return ResponceApi<string>.Fail("You do not have permission to delete this item.");
            if (item.Status == FoundItemStatus.Claimed) return ResponceApi<string>.Fail("Cannot delete a claimed item.");

            item.Status = FoundItemStatus.Archived;
            await _db.SaveChangesAsync();

            return ResponceApi<string>.Ok(itemId.ToString(), "Item archived successfully.");
        }

        // ----------------------------------------------------------------
        // GET PUBLISHED (public)
        // ----------------------------------------------------------------
        public async Task<ResponceApi<PagedResult<FoundItemListDTO>>> GetPublishedAsync(FoundItemFilterDTO filter, Guid? currentUserId)
        {
            var query = _db.FoundItems
                .Include(i => i.ReportedByUser)
                .Where(i => i.Status == FoundItemStatus.Published && !i.IsClaimed)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(filter.Category))
                query = query.Where(i => i.Category.ToLower() == filter.Category.ToLower());

            if (!string.IsNullOrWhiteSpace(filter.Keyword))
            {
                var kw = filter.Keyword.ToLower();
                query = query.Where(i => i.Title.ToLower().Contains(kw) || i.Description.ToLower().Contains(kw));
            }

            var items = await query.OrderByDescending(i => i.CreatedAt).ToListAsync();

            // Geo filter (in memory after DB fetch for simplicity)
            if (filter.NearLatitude.HasValue && filter.NearLongitude.HasValue && filter.RadiusKm.HasValue)
            {
                items = items.Where(i => HaversineKm(
                    (double)filter.NearLatitude.Value, (double)filter.NearLongitude.Value,
                    (double)i.FoundLatitude, (double)i.FoundLongitude) <= filter.RadiusKm.Value).ToList();
            }

            var total = items.Count;
            var paged = items
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToList();

            HashSet<Guid>? attempted = null;
            if (currentUserId.HasValue)
            {
                var ids = paged.Select(i => i.Id).ToList();
                attempted = (await _db.ClaimAttempts
                    .Where(c => c.ClaimantUserId == currentUserId.Value && ids.Contains(c.FoundItemId))
                    .Select(c => c.FoundItemId)
                    .ToListAsync()).ToHashSet();
            }

            var dtos = paged.Select(i => MapToListDto(i, currentUserId, attempted)).ToList();

            return ResponceApi<PagedResult<FoundItemListDTO>>.Ok(new PagedResult<FoundItemListDTO>
            {
                Items = dtos,
                TotalCount = total,
                Page = filter.Page,
                PageSize = filter.PageSize
            });
        }

        // ----------------------------------------------------------------
        // GET BY ID
        // ----------------------------------------------------------------
        public async Task<ResponceApi<FoundItemDetailDTO>> GetByIdAsync(Guid itemId, Guid? currentUserId)
        {
            var item = await _db.FoundItems
                .Include(i => i.ReportedByUser)
                .Include(i => i.ReviewedByAdmin)
                .Include(i => i.ClaimedByUser)
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == itemId);

            if (item == null) return ResponceApi<FoundItemDetailDTO>.Fail("Item not found.");

            // Non-admins can only see published items
            if (currentUserId.HasValue)
            {
                var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == currentUserId.Value);
                bool isAdmin = user?.AccountType == "Admin";
                bool isOwner = item.ReportedByUserId == currentUserId.Value;

                if (!isAdmin && !isOwner && item.Status != FoundItemStatus.Published)
                    return ResponceApi<FoundItemDetailDTO>.Fail("Item not found.");
            }
            else if (item.Status != FoundItemStatus.Published)
            {
                return ResponceApi<FoundItemDetailDTO>.Fail("Item not found.");
            }

            return ResponceApi<FoundItemDetailDTO>.Ok(await MapToDetailDtoAsync(item, currentUserId));
        }

        // ----------------------------------------------------------------
        // MY ITEMS
        // ----------------------------------------------------------------
        public async Task<ResponceApi<PagedResult<FoundItemListDTO>>> GetMyItemsAsync(Guid userId, int page, int pageSize)
        {
            var query = _db.FoundItems
                .Include(i => i.ReportedByUser)
                .Where(i => i.ReportedByUserId == userId)
                .OrderByDescending(i => i.CreatedAt)
                .AsNoTracking();

            var total = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var dtos = items.Select(i => MapToListDto(i, userId, null)).ToList();

            return ResponceApi<PagedResult<FoundItemListDTO>>.Ok(new PagedResult<FoundItemListDTO>
            {
                Items = dtos,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            });
        }

        // ----------------------------------------------------------------
        // SUBMIT CLAIM
        // ----------------------------------------------------------------
        public async Task<ResponceApi<ClaimAttemptDTO>> SubmitClaimAsync(Guid userId, SubmitClaimDTO dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return ResponceApi<ClaimAttemptDTO>.Fail("User not found.");
            if (user.Blocked) return ResponceApi<ClaimAttemptDTO>.Fail("Your account is blocked.");
            if (!user.Verified) return ResponceApi<ClaimAttemptDTO>.Fail("You must verify your account before claiming items.");

            var item = await _db.FoundItems
                .Include(i => i.ReportedByUser)
                .FirstOrDefaultAsync(i => i.Id == dto.FoundItemId && i.Status == FoundItemStatus.Published);

            if (item == null) return ResponceApi<ClaimAttemptDTO>.Fail("Item not found or not available.");
            if (item.IsClaimed) return ResponceApi<ClaimAttemptDTO>.Fail("This item has already been claimed.");
            if (item.ReportedByUserId == userId) return ResponceApi<ClaimAttemptDTO>.Fail("You cannot claim your own item.");

            // One attempt per user per item
            var existing = await _db.ClaimAttempts.FirstOrDefaultAsync(c => c.FoundItemId == dto.FoundItemId && c.ClaimantUserId == userId);
            if (existing != null) return ResponceApi<ClaimAttemptDTO>.Fail("You have already submitted a claim for this item. Each user gets one attempt.");

            var normalizedAnswer = dto.Answer.Trim().ToLowerInvariant();
            var isCorrect = _dataHasher.VerifyHashed(normalizedAnswer, item.VerificationAnswerHash);

            var attempt = new ClaimAttempt
            {
                Id = Guid.NewGuid(),
                FoundItemId = dto.FoundItemId,
                ClaimantUserId = userId,
                ProvidedAnswer = dto.Answer.Trim(),
                IsCorrect = isCorrect,
                Status = ClaimAttemptStatus.Pending,
                AttemptedAt = DateTime.UtcNow
            };

            _db.ClaimAttempts.Add(attempt);
            await _db.SaveChangesAsync();

            // Notify item owner
            await _notificationService.SendAsync(item.ReportedByUserId,
                "New Claim Attempt",
                $"{user.UserName} submitted a claim for '{item.Title}'. Answer was {(isCorrect ? "CORRECT ✅" : "incorrect ❌")}.",
                NotificationType.ClaimSubmitted, item.Id);

            // Notify admins when correct answer
            if (isCorrect)
            {
                var admins = await _db.Users.Where(u => u.AccountType == "Admin").ToListAsync();
                foreach (var admin in admins)
                {
                    await _notificationService.SendAsync(admin.UserId,
                        "Correct Claim Answer",
                        $"User {user.UserName} correctly answered the verification question for '{item.Title}'.",
                        NotificationType.ClaimSubmitted, item.Id);
                }
            }

            var claimDto = new ClaimAttemptDTO
            {
                Id = attempt.Id,
                FoundItemId = attempt.FoundItemId,
                FoundItemTitle = item.Title,
                ClaimantUserId = userId,
                ClaimantUserName = user.UserName,
                ProvidedAnswer = attempt.ProvidedAnswer,
                IsCorrect = isCorrect,
                Status = attempt.Status.ToString(),
                AttemptedAt = attempt.AttemptedAt
            };

            string msg = isCorrect
                ? "Your answer is correct! The item owner or admin will review your claim."
                : "Your answer is incorrect. Unfortunately you only have one attempt per item.";

            return ResponceApi<ClaimAttemptDTO>.Ok(claimDto, msg);
        }

        // ----------------------------------------------------------------
        // GET ITEM CLAIMS (owner or admin)
        // ----------------------------------------------------------------
        public async Task<ResponceApi<PagedResult<ClaimAttemptDTO>>> GetItemClaimsAsync(Guid requestorId, Guid itemId, int page, int pageSize)
        {
            var item = await _db.FoundItems.FirstOrDefaultAsync(i => i.Id == itemId);
            if (item == null) return ResponceApi<PagedResult<ClaimAttemptDTO>>.Fail("Item not found.");

            var requestor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == requestorId);
            bool isAdmin = requestor?.AccountType == "Admin";
            bool isOwner = item.ReportedByUserId == requestorId;

            if (!isAdmin && !isOwner) return ResponceApi<PagedResult<ClaimAttemptDTO>>.Fail("Access denied.");

            var query = _db.ClaimAttempts
                .Include(c => c.ClaimantUser)
                .Where(c => c.FoundItemId == itemId)
                .OrderByDescending(c => c.AttemptedAt);

            var total = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var dtos = items.Select(c => new ClaimAttemptDTO
            {
                Id = c.Id,
                FoundItemId = c.FoundItemId,
                FoundItemTitle = item.Title,
                ClaimantUserId = c.ClaimantUserId,
                ClaimantUserName = c.ClaimantUser?.UserName ?? "",
                ProvidedAnswer = c.ProvidedAnswer,
                IsCorrect = c.IsCorrect,
                Status = c.Status.ToString(),
                AttemptedAt = c.AttemptedAt,
                AdminNote = c.AdminNote
            }).ToList();

            return ResponceApi<PagedResult<ClaimAttemptDTO>>.Ok(new PagedResult<ClaimAttemptDTO>
            {
                Items = dtos,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            });
        }

        // ----------------------------------------------------------------
        // ADMIN: REVIEW ITEM
        // ----------------------------------------------------------------
        public async Task<ResponceApi<string>> AdminReviewItemAsync(Guid adminId, AdminReviewItemDTO dto)
        {
            var admin = await _db.Users.FirstOrDefaultAsync(u => u.UserId == adminId);
            if (admin == null || admin.AccountType != "Admin")
                return ResponceApi<string>.Fail("Unauthorized.");

            var item = await _db.FoundItems
                .Include(i => i.ReportedByUser)
                .FirstOrDefaultAsync(i => i.Id == dto.FoundItemId);
            if (item == null) return ResponceApi<string>.Fail("Item not found.");

            var previousStatus = item.Status;

            item.ReviewedByAdminId = adminId;
            item.ReviewedAt = DateTime.UtcNow;
            item.AdminNote = dto.Note;

            switch (dto.Action)
            {
                case "Approve":
                    item.Status = FoundItemStatus.Published;
                    break;
                case "Reject":
                    item.Status = FoundItemStatus.Rejected;
                    break;
                case "Block":
                    item.Status = FoundItemStatus.Blocked;
                    break;
                default:
                    return ResponceApi<string>.Fail("Invalid action.");
            }

            await _db.SaveChangesAsync();

            // Notify item owner
            var notifType = dto.Action switch
            {
                "Approve" => NotificationType.ItemApproved,
                "Reject" => NotificationType.ItemRejected,
                _ => NotificationType.ItemBlocked
            };

            string notifBody = dto.Action switch
            {
                "Approve" => $"Your item '{item.Title}' has been approved and is now live.",
                "Reject" => $"Your item '{item.Title}' was rejected. Note: {dto.Note}",
                _ => $"Your item '{item.Title}' has been blocked. Note: {dto.Note}"
            };

            await _notificationService.SendAsync(item.ReportedByUserId,
                $"Item {dto.Action}d",
                notifBody,
                notifType, item.Id);

            // Send email
            try
            {
                var email = _dataCiphers.Decrypt(item.ReportedByUser.EmailChipher);
                var mailBody = dto.Action == "Approve"
                    ? _emailTemplateService.CreateOrderConfirmationEmail(email, item.Id.ToString(), item.ReportedByUser.FullName)
                    : _emailTemplateService.CreateNotificationEmail(email, $"Item {dto.Action}d", notifBody, item.ReportedByUser.FullName);
                _ = _emailTemplateService.SendEmailAsync(mailBody);
            }
            catch { /* non-critical */ }

            return ResponceApi<string>.Ok(item.Id.ToString(), $"Item {dto.Action.ToLower()}d successfully.");
        }

        // ----------------------------------------------------------------
        // ADMIN: REVIEW CLAIM
        // ----------------------------------------------------------------
        public async Task<ResponceApi<string>> AdminReviewClaimAsync(Guid adminId, AdminReviewClaimDTO dto)
        {
            var admin = await _db.Users.FirstOrDefaultAsync(u => u.UserId == adminId);
            if (admin == null || admin.AccountType != "Admin")
                return ResponceApi<string>.Fail("Unauthorized.");

            var claim = await _db.ClaimAttempts
                .Include(c => c.FoundItem)
                .Include(c => c.ClaimantUser)
                .FirstOrDefaultAsync(c => c.Id == dto.ClaimAttemptId);
            if (claim == null) return ResponceApi<string>.Fail("Claim not found.");

            claim.ReviewedByAdminId = adminId;
            claim.ReviewedAt = DateTime.UtcNow;
            claim.AdminNote = dto.Note;

            if (dto.Action == "Approve")
            {
                claim.Status = ClaimAttemptStatus.Approved;

                // Mark item as claimed
                claim.FoundItem.IsClaimed = true;
                claim.FoundItem.Status = FoundItemStatus.Claimed;
                claim.FoundItem.ClaimedByUserId = claim.ClaimantUserId;
                claim.FoundItem.ClaimedAt = DateTime.UtcNow;

                // Notify claimant
                await _notificationService.SendAsync(claim.ClaimantUserId,
                    "Claim Approved! 🎉",
                    $"Your claim for '{claim.FoundItem.Title}' has been approved. Please contact the finder to arrange pickup.",
                    NotificationType.ClaimApproved, claim.FoundItemId);

                // Notify finder
                await _notificationService.SendAsync(claim.FoundItem.ReportedByUserId,
                    "Item Claimed",
                    $"Your found item '{claim.FoundItem.Title}' has been claimed by {claim.ClaimantUser?.UserName}.",
                    NotificationType.ItemClaimed, claim.FoundItemId);
            }
            else
            {
                claim.Status = ClaimAttemptStatus.Rejected;

                await _notificationService.SendAsync(claim.ClaimantUserId,
                    "Claim Rejected",
                    $"Your claim for '{claim.FoundItem.Title}' has been reviewed and rejected. Note: {dto.Note}",
                    NotificationType.ClaimRejected, claim.FoundItemId);
            }

            await _db.SaveChangesAsync();

            return ResponceApi<string>.Ok(claim.Id.ToString(), $"Claim {dto.Action.ToLower()}d successfully.");
        }

        // ----------------------------------------------------------------
        // ADMIN: GET ALL ITEMS
        // ----------------------------------------------------------------
        public async Task<ResponceApi<PagedResult<FoundItemListDTO>>> AdminGetAllItemsAsync(FoundItemFilterDTO filter)
        {
            var query = _db.FoundItems
                .Include(i => i.ReportedByUser)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<FoundItemStatus>(filter.Status, out var statusEnum))
                query = query.Where(i => i.Status == statusEnum);

            if (!string.IsNullOrWhiteSpace(filter.Category))
                query = query.Where(i => i.Category.ToLower() == filter.Category.ToLower());

            if (!string.IsNullOrWhiteSpace(filter.Keyword))
            {
                var kw = filter.Keyword.ToLower();
                query = query.Where(i => i.Title.ToLower().Contains(kw) || i.Description.ToLower().Contains(kw));
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(i => i.CreatedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            var dtos = items.Select(i => MapToListDto(i, null, null)).ToList();

            return ResponceApi<PagedResult<FoundItemListDTO>>.Ok(new PagedResult<FoundItemListDTO>
            {
                Items = dtos,
                TotalCount = total,
                Page = filter.Page,
                PageSize = filter.PageSize
            });
        }

        // ----------------------------------------------------------------
        // ADMIN: DASHBOARD
        // ----------------------------------------------------------------
        public async Task<ResponceApi<AdminDashboardDTO>> AdminGetDashboardAsync()
        {
            var dashboard = new AdminDashboardDTO
            {
                TotalUsers = await _db.Users.CountAsync(),
                ActiveUsers = await _db.Users.CountAsync(u => !u.Blocked),
                BlockedUsers = await _db.Users.CountAsync(u => u.Blocked),
                TotalItems = await _db.FoundItems.CountAsync(),
                PendingReviewItems = await _db.FoundItems.CountAsync(i => i.Status == FoundItemStatus.PendingReview),
                PublishedItems = await _db.FoundItems.CountAsync(i => i.Status == FoundItemStatus.Published),
                ClaimedItems = await _db.FoundItems.CountAsync(i => i.Status == FoundItemStatus.Claimed),
                RejectedItems = await _db.FoundItems.CountAsync(i => i.Status == FoundItemStatus.Rejected),
                BlockedItems = await _db.FoundItems.CountAsync(i => i.Status == FoundItemStatus.Blocked),
                TotalClaimAttempts = await _db.ClaimAttempts.CountAsync(),
                PendingClaims = await _db.ClaimAttempts.CountAsync(c => c.Status == ClaimAttemptStatus.Pending)
            };

            return ResponceApi<AdminDashboardDTO>.Ok(dashboard);
        }

        // ----------------------------------------------------------------
        // ADMIN: MANAGE USER
        // ----------------------------------------------------------------
        public async Task<ResponceApi<string>> AdminManageUserAsync(Guid adminId, ManageUserDTO dto)
        {
            var admin = await _db.Users.FirstOrDefaultAsync(u => u.UserId == adminId);
            if (admin == null || admin.AccountType != "Admin")
                return ResponceApi<string>.Fail("Unauthorized.");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == dto.UserId);
            if (user == null) return ResponceApi<string>.Fail("User not found.");

            if (dto.Action == "Block")
            {
                user.Blocked = true;
                user.Login = false;

                // Deactivate all sessions
                var sessions = await _db.UserSessions.Where(s => s.UserId == dto.UserId && s.IsActive).ToListAsync();
                sessions.ForEach(s => s.IsActive = false);

                await _db.SaveChangesAsync();

                await _notificationService.SendAsync(user.UserId,
                    "Account Blocked",
                    $"Your account has been blocked. Reason: {dto.Reason ?? "Policy violation."}",
                    NotificationType.AdminMessage);
            }
            else if (dto.Action == "Unblock")
            {
                user.Blocked = false;
                user.FailedLoginAttempts = 0;

                await _db.SaveChangesAsync();

                await _notificationService.SendAsync(user.UserId,
                    "Account Unblocked",
                    "Your account has been unblocked. You can now log in again.",
                    NotificationType.AdminMessage);
            }

            return ResponceApi<string>.Ok(dto.UserId.ToString(), $"User {dto.Action.ToLower()}ed successfully.");
        }

        // ----------------------------------------------------------------
        // ADMIN: GET USERS
        // ----------------------------------------------------------------
        public async Task<ResponceApi<PagedResult<UserListDTO>>> AdminGetUsersAsync(string? search, bool? blocked, int page, int pageSize)
        {
            var query = _db.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(u => u.UserName.ToLower().Contains(s) || u.FullName.ToLower().Contains(s));
            }

            if (blocked.HasValue)
                query = query.Where(u => u.Blocked == blocked.Value);

            var total = await query.CountAsync();
            var users = await query
                .OrderByDescending(u => u.JoinDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var userIds = users.Select(u => u.UserId).ToList();
            var itemCounts = await _db.FoundItems
                .Where(i => userIds.Contains(i.ReportedByUserId))
                .GroupBy(i => i.ReportedByUserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.UserId, g => g.Count);

            var claimCounts = await _db.ClaimAttempts
                .Where(c => userIds.Contains(c.ClaimantUserId))
                .GroupBy(c => c.ClaimantUserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.UserId, g => g.Count);

            var dtos = users.Select(u => new UserListDTO
            {
                UserId = u.UserId,
                UserName = u.UserName,
                FullName = u.FullName,
                AccountType = u.AccountType,
                Verified = u.Verified,
                Blocked = u.Blocked,
                Login = u.Login,
                JoinDate = _cairoTime.UtcToCairo(u.JoinDate),
                LastLogin = u.LastLogin.HasValue ? _cairoTime.UtcToCairo(u.LastLogin.Value) : null,
                TotalItemsReported = itemCounts.GetValueOrDefault(u.UserId, 0),
                TotalClaimAttempts = claimCounts.GetValueOrDefault(u.UserId, 0)
            }).ToList();

            return ResponceApi<PagedResult<UserListDTO>>.Ok(new PagedResult<UserListDTO>
            {
                Items = dtos,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            });
        }

        // ----------------------------------------------------------------
        // ADMIN: DELETE ITEM
        // ----------------------------------------------------------------
        public async Task<ResponceApi<string>> AdminDeleteItemAsync(Guid adminId, Guid itemId)
        {
            var admin = await _db.Users.FirstOrDefaultAsync(u => u.UserId == adminId);
            if (admin == null || admin.AccountType != "Admin")
                return ResponceApi<string>.Fail("Unauthorized.");

            var item = await _db.FoundItems.FirstOrDefaultAsync(i => i.Id == itemId);
            if (item == null) return ResponceApi<string>.Fail("Item not found.");

            item.Status = FoundItemStatus.Archived;
            await _db.SaveChangesAsync();

            await _notificationService.SendAsync(item.ReportedByUserId,
                "Item Removed",
                $"Your item '{item.Title}' has been removed by an admin.",
                NotificationType.AdminMessage, item.Id);

            return ResponceApi<string>.Ok(itemId.ToString(), "Item removed successfully.");
        }

        // ----------------------------------------------------------------
        // HELPERS
        // ----------------------------------------------------------------
        private FoundItemListDTO MapToListDto(FoundItem item, Guid? currentUserId, HashSet<Guid>? attemptedIds)
        {
            var images = new List<string>();
            try { images = JsonSerializer.Deserialize<List<string>>(item.ImagesJson) ?? new(); } catch { }

            return new FoundItemListDTO
            {
                Id = item.Id,
                Title = item.Title,
                Description = item.Description,
                Category = item.Category,
                FoundLatitude = item.FoundLatitude,
                FoundLongitude = item.FoundLongitude,
                FoundAddress = item.FoundAddress,
                ImageUrls = images,
                RewardPercentage = item.RewardPercentage,
                EstimatedValueEGP = item.EstimatedValueEGP,
                RewardAmountEGP = item.EstimatedValueEGP.HasValue
                    ? Math.Round(item.EstimatedValueEGP.Value * item.RewardPercentage / 100, 2)
                    : null,
                VerificationQuestion = item.VerificationQuestion,
                Status = item.Status.ToString(),
                ReportedByUserName = item.ReportedByUser?.UserName ?? "",
                ReportedByUserId = item.ReportedByUserId,
                FoundAt = _cairoTime.UtcToCairo(item.FoundAt),
                CreatedAt = _cairoTime.UtcToCairo(item.CreatedAt),
                IsClaimed = item.IsClaimed,
                HasAttempted = currentUserId.HasValue && (attemptedIds?.Contains(item.Id) ?? false)
            };
        }

        private async Task<FoundItemDetailDTO> MapToDetailDtoAsync(FoundItem item, Guid? currentUserId)
        {
            var images = new List<string>();
            try { images = JsonSerializer.Deserialize<List<string>>(item.ImagesJson) ?? new(); } catch { }

            bool hasAttempted = false;
            if (currentUserId.HasValue)
                hasAttempted = await _db.ClaimAttempts.AnyAsync(c => c.FoundItemId == item.Id && c.ClaimantUserId == currentUserId.Value);

            return new FoundItemDetailDTO
            {
                Id = item.Id,
                Title = item.Title,
                Description = item.Description,
                Category = item.Category,
                FoundLatitude = item.FoundLatitude,
                FoundLongitude = item.FoundLongitude,
                FoundAddress = item.FoundAddress,
                ImageUrls = images,
                RewardPercentage = item.RewardPercentage,
                EstimatedValueEGP = item.EstimatedValueEGP,
                RewardAmountEGP = item.EstimatedValueEGP.HasValue
                    ? Math.Round(item.EstimatedValueEGP.Value * item.RewardPercentage / 100, 2)
                    : null,
                VerificationQuestion = item.VerificationQuestion,
                Status = item.Status.ToString(),
                ReportedByUserName = item.ReportedByUser?.UserName ?? "",
                ReportedByUserId = item.ReportedByUserId,
                FoundAt = _cairoTime.UtcToCairo(item.FoundAt),
                CreatedAt = _cairoTime.UtcToCairo(item.CreatedAt),
                IsClaimed = item.IsClaimed,
                HasAttempted = hasAttempted,
                AdminNote = item.AdminNote,
                ReviewedByAdminName = item.ReviewedByAdmin?.UserName,
                ReviewedAt = item.ReviewedAt.HasValue ? _cairoTime.UtcToCairo(item.ReviewedAt.Value) : null,
                ClaimedAt = item.ClaimedAt.HasValue ? _cairoTime.UtcToCairo(item.ClaimedAt.Value) : null,
                ClaimedByUserName = item.ClaimedByUser?.UserName
            };
        }

        private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            var dLat = ToRad(lat2 - lat1);
            var dLon = ToRad(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private static double ToRad(double deg) => deg * Math.PI / 180;
    }
}