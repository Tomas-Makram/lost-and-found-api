using BusinessLayer.DTOs;
using BusinessLayer.Models;
using DataLayer.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BusinessLayer.Services
{
    public interface IChatService
    {
        Task<ResponceApi<ChatMessageDTO>> SendMessageAsync(Guid senderId, SendMessageDTO dto);
        Task<ResponceApi<PagedResult<ChatMessageDTO>>> GetConversationAsync(Guid userId, Guid foundItemId, Guid otherUserId, int page, int pageSize);
        Task<ResponceApi<List<ConversationDTO>>> GetMyConversationsAsync(Guid userId);
        Task<ResponceApi<string>> MarkConversationReadAsync(Guid userId, Guid foundItemId, Guid otherUserId);
        Task<ResponceApi<int>> GetUnreadCountAsync(Guid userId);
    }

    public class ChatService : IChatService
    {
        private readonly DBContext _db;
        private readonly IHubContext<ChatHub> _chatHub;
        private readonly CairoTimeService _cairoTime;

        public ChatService(DBContext db, IHubContext<ChatHub> chatHub, CairoTimeService cairoTime)
        {
            _db = db;
            _chatHub = chatHub;
            _cairoTime = cairoTime;
        }

        public async Task<ResponceApi<ChatMessageDTO>> SendMessageAsync(Guid senderId, SendMessageDTO dto)
        {
            var sender = await _db.Users.FirstOrDefaultAsync(u => u.UserId == senderId);
            if (sender == null) return ResponceApi<ChatMessageDTO>.Fail("Sender not found.");
            if (sender.Blocked) return ResponceApi<ChatMessageDTO>.Fail("Your account is blocked.");

            var item = await _db.FoundItems
                .FirstOrDefaultAsync(i => i.Id == dto.FoundItemId);
            if (item == null) return ResponceApi<ChatMessageDTO>.Fail("Item not found.");
            if (item.Status != FoundItemStatus.Published && item.Status != FoundItemStatus.Claimed)
                return ResponceApi<ChatMessageDTO>.Fail("This item is not available for chat.");

            // Only finder or users who attempted a claim can chat
            bool isFinder = item.ReportedByUserId == senderId;
            bool hasAttempt = await _db.ClaimAttempts.AnyAsync(c => c.FoundItemId == dto.FoundItemId && c.ClaimantUserId == senderId);

            if (!isFinder && !hasAttempt)
                return ResponceApi<ChatMessageDTO>.Fail("You can only chat about items you have attempted to claim.");

            // Verify recipient is the other party
            bool recipientIsFinder = item.ReportedByUserId == dto.RecipientId;
            bool recipientHasAttempt = await _db.ClaimAttempts.AnyAsync(c => c.FoundItemId == dto.FoundItemId && c.ClaimantUserId == dto.RecipientId);
            if (!recipientIsFinder && !recipientHasAttempt)
                return ResponceApi<ChatMessageDTO>.Fail("Invalid recipient for this item.");

            var message = new ChatMessage
            {
                Id = Guid.NewGuid(),
                FoundItemId = dto.FoundItemId,
                SenderId = senderId,
                RecipientId = dto.RecipientId,
                Content = dto.Content.Trim(),
                IsRead = false,
                SentAt = DateTime.UtcNow
            };

            _db.ChatMessages.Add(message);
            await _db.SaveChangesAsync();

            var msgDto = new ChatMessageDTO
            {
                Id = message.Id,
                FoundItemId = dto.FoundItemId,
                FoundItemTitle = item.Title,
                SenderId = senderId,
                SenderName = sender.UserName,
                SenderProfileImg = sender.UserProfileImgURL,
                RecipientId = dto.RecipientId,
                Content = message.Content,
                IsRead = false,
                SentAt = _cairoTime.UtcToCairo(message.SentAt),
                IsMine = true
            };

            // Push real-time to recipient
            var recipientMsg = new ChatMessageDTO
            {
                Id = message.Id,
                FoundItemId = dto.FoundItemId,
                FoundItemTitle = item.Title,
                SenderId = senderId,
                SenderName = sender.UserName,
                SenderProfileImg = sender.UserProfileImgURL,
                RecipientId = dto.RecipientId,
                Content = message.Content,
                IsRead = false,
                SentAt = _cairoTime.UtcToCairo(message.SentAt),
                IsMine = false
            };

            await _chatHub.Clients.Group($"chat:user:{dto.RecipientId}")
                .SendAsync("ReceiveMessage", recipientMsg);

            return ResponceApi<ChatMessageDTO>.Ok(msgDto, "Message sent.");
        }

        public async Task<ResponceApi<PagedResult<ChatMessageDTO>>> GetConversationAsync(Guid userId, Guid foundItemId, Guid otherUserId, int page, int pageSize)
        {
            // Verify user is participant
            var item = await _db.FoundItems.FirstOrDefaultAsync(i => i.Id == foundItemId);
            if (item == null) return ResponceApi<PagedResult<ChatMessageDTO>>.Fail("Item not found.");

            bool isFinder = item.ReportedByUserId == userId;
            bool hasAttempt = await _db.ClaimAttempts.AnyAsync(c => c.FoundItemId == foundItemId && c.ClaimantUserId == userId);
            bool isAdmin = (await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId))?.AccountType == "Admin";

            if (!isFinder && !hasAttempt && !isAdmin)
                return ResponceApi<PagedResult<ChatMessageDTO>>.Fail("Access denied.");

            var query = _db.ChatMessages
                .Include(m => m.Sender)
                .Where(m => m.FoundItemId == foundItemId &&
                    ((m.SenderId == userId && m.RecipientId == otherUserId) ||
                     (m.SenderId == otherUserId && m.RecipientId == userId)))
                .OrderBy(m => m.SentAt);

            var total = await query.CountAsync();
            var messages = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Mark received messages as read
            var unread = messages.Where(m => m.RecipientId == userId && !m.IsRead).ToList();
            unread.ForEach(m => m.IsRead = true);
            if (unread.Any()) await _db.SaveChangesAsync();

            var dtos = messages.Select(m => new ChatMessageDTO
            {
                Id = m.Id,
                FoundItemId = m.FoundItemId,
                FoundItemTitle = item.Title,
                SenderId = m.SenderId,
                SenderName = m.Sender?.UserName ?? "",
                SenderProfileImg = m.Sender?.UserProfileImgURL ?? "",
                RecipientId = m.RecipientId,
                Content = m.Content,
                IsRead = m.IsRead,
                SentAt = _cairoTime.UtcToCairo(m.SentAt),
                IsMine = m.SenderId == userId
            }).ToList();

            return ResponceApi<PagedResult<ChatMessageDTO>>.Ok(new PagedResult<ChatMessageDTO>
            {
                Items = dtos,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            });
        }

        public async Task<ResponceApi<List<ConversationDTO>>> GetMyConversationsAsync(Guid userId)
        {
            // Get all messages involving this user, grouped by (foundItemId, otherUserId)
            var messages = await _db.ChatMessages
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .Include(m => m.FoundItem)
                .Where(m => m.SenderId == userId || m.RecipientId == userId)
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();

            var conversations = messages
                .GroupBy(m => new
                {
                    m.FoundItemId,
                    OtherUserId = m.SenderId == userId ? m.RecipientId : m.SenderId
                })
                .Select(g =>
                {
                    var last = g.First();
                    var other = last.SenderId == userId ? last.Recipient : last.Sender;
                    var unread = g.Count(m => m.RecipientId == userId && !m.IsRead);

                    return new ConversationDTO
                    {
                        FoundItemId = g.Key.FoundItemId,
                        FoundItemTitle = last.FoundItem?.Title ?? "",
                        OtherUserId = g.Key.OtherUserId,
                        OtherUserName = other?.UserName ?? "",
                        OtherUserProfileImg = other?.UserProfileImgURL ?? "",
                        UnreadCount = unread,
                        LastMessage = new ChatMessageDTO
                        {
                            Id = last.Id,
                            FoundItemId = last.FoundItemId,
                            FoundItemTitle = last.FoundItem?.Title ?? "",
                            SenderId = last.SenderId,
                            SenderName = last.Sender?.UserName ?? "",
                            SenderProfileImg = last.Sender?.UserProfileImgURL ?? "",
                            RecipientId = last.RecipientId,
                            Content = last.Content,
                            IsRead = last.IsRead,
                            SentAt = _cairoTime.UtcToCairo(last.SentAt),
                            IsMine = last.SenderId == userId
                        }
                    };
                })
                .ToList();

            return ResponceApi<List<ConversationDTO>>.Ok(conversations);
        }

        public async Task<ResponceApi<string>> MarkConversationReadAsync(Guid userId, Guid foundItemId, Guid otherUserId)
        {
            await _db.ChatMessages
                .Where(m => m.FoundItemId == foundItemId && m.RecipientId == userId && m.SenderId == otherUserId && !m.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));

            return ResponceApi<string>.Ok("done", "Conversation marked as read.");
        }

        public async Task<ResponceApi<int>> GetUnreadCountAsync(Guid userId)
        {
            var count = await _db.ChatMessages.CountAsync(m => m.RecipientId == userId && !m.IsRead);
            return ResponceApi<int>.Ok(count);
        }
    }
}