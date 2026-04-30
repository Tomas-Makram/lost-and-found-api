using Asp.Versioning;
using BusinessLayer.Attributes;
using BusinessLayer.DTOs;
using BusinessLayer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BackendAPILorenSameh.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    [Authorize]
    [RequireActiveLogin]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        /// <summary>Send a message (REST fallback — prefer SignalR ChatHub.SendMessage)</summary>
        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var userId = GetUserId();
            var result = await _chatService.SendMessageAsync(userId, dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        /// <summary>Get conversation history between me and another user for a specific item</summary>
        [HttpGet("conversation/{foundItemId:guid}/{otherUserId:guid}")]
        public async Task<IActionResult> GetConversation(Guid foundItemId, Guid otherUserId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var userId = GetUserId();
            var result = await _chatService.GetConversationAsync(userId, foundItemId, otherUserId, page, pageSize);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        /// <summary>Get all my conversations (inbox)</summary>
        [HttpGet("conversations")]
        public async Task<IActionResult> GetMyConversations()
        {
            var userId = GetUserId();
            var result = await _chatService.GetMyConversationsAsync(userId);
            return Ok(result);
        }

        /// <summary>Mark a conversation as read</summary>
        [HttpPost("read/{foundItemId:guid}/{otherUserId:guid}")]
        public async Task<IActionResult> MarkRead(Guid foundItemId, Guid otherUserId)
        {
            var userId = GetUserId();
            var result = await _chatService.MarkConversationReadAsync(userId, foundItemId, otherUserId);
            return Ok(result);
        }

        /// <summary>Get total unread messages count</summary>
        [HttpGet("unread")]
        public async Task<IActionResult> UnreadCount()
        {
            var userId = GetUserId();
            var result = await _chatService.GetUnreadCountAsync(userId);
            return Ok(result);
        }

        private Guid GetUserId()
        {
            Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id);
            return id;
        }
    }
}
