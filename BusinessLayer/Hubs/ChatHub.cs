using BusinessLayer.DTOs;
using DataLayer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BusinessLayer.Services
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly DBContext _db;

        public ChatHub(DBContext db)
        {
            _db = db;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"chat:user:{userId}");
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat:user:{userId}");
            }
            await base.OnDisconnectedAsync(exception);
        }

        // Join a chat room for a specific found item
        public async Task JoinItemChat(string foundItemId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId)) return;

            // Check if user has permission (is finder or claimant)
            if (!Guid.TryParse(foundItemId, out var itemId) || !Guid.TryParse(userId, out var uid)) return;

            var item = await _db.FoundItems.FirstOrDefaultAsync(f => f.Id == itemId);
            if (item == null) return;

            bool isFinder = item.ReportedByUserId == uid;
            bool hasAttempt = await _db.ClaimAttempts.AnyAsync(c => c.FoundItemId == itemId && c.ClaimantUserId == uid);

            if (isFinder || hasAttempt)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"chat:item:{foundItemId}");
            }
        }

        public async Task LeaveItemChat(string foundItemId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat:item:{foundItemId}");
        }

        // Send message via SignalR (alternative to REST API)
        public async Task SendMessage(string foundItemId, string recipientId, string content)
        {
            var senderIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(senderIdStr, out var senderId)) return;
            if (!Guid.TryParse(foundItemId, out var itemId)) return;
            if (!Guid.TryParse(recipientId, out var recipientGuid)) return;

            if (string.IsNullOrWhiteSpace(content) || content.Length > 2000) return;

            var item = await _db.FoundItems.FirstOrDefaultAsync(f => f.Id == itemId && f.Status == FoundItemStatus.Published);
            if (item == null) return;

            bool isFinder = item.ReportedByUserId == senderId;
            bool hasAttempt = await _db.ClaimAttempts.AnyAsync(c => c.FoundItemId == itemId && c.ClaimantUserId == senderId);

            if (!isFinder && !hasAttempt) return;

            var sender = await _db.Users.FirstOrDefaultAsync(u => u.UserId == senderId);
            if (sender == null) return;

            var message = new ChatMessage
            {
                Id = Guid.NewGuid(),
                FoundItemId = itemId,
                SenderId = senderId,
                RecipientId = recipientGuid,
                Content = content.Trim(),
                IsRead = false,
                SentAt = DateTime.UtcNow
            };

            _db.ChatMessages.Add(message);
            await _db.SaveChangesAsync();

            var msgDto = new ChatMessageDTO
            {
                Id = message.Id,
                FoundItemId = itemId,
                FoundItemTitle = item.Title,
                SenderId = senderId,
                SenderName = sender.UserName,
                SenderProfileImg = sender.UserProfileImgURL,
                RecipientId = recipientGuid,
                Content = content.Trim(),
                IsRead = false,
                SentAt = message.SentAt,
                IsMine = false
            };

            // Deliver to recipient
            await Clients.Group($"chat:user:{recipientId}").SendAsync("ReceiveMessage", msgDto);

            // Confirm to sender (with IsMine=true)
            msgDto.IsMine = true;
            await Clients.Caller.SendAsync("ReceiveMessage", msgDto);
        }
    }
}