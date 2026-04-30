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
    [Authorize(Roles = "Admin")]
    [RequireActiveLogin]
    public class AdminController : ControllerBase
    {
        private readonly IFoundItemService _foundItemService;

        public AdminController(IFoundItemService foundItemService)
        {
            _foundItemService = foundItemService;
        }

        // ── DASHBOARD ─────────────────────────────────────────────────────

        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            var result = await _foundItemService.AdminGetDashboardAsync();
            return Ok(result);
        }

        // ── ITEMS ────────────────────────────────────────────────────────

        [HttpGet("items")]
        public async Task<IActionResult> GetAllItems([FromQuery] FoundItemFilterDTO filter)
        {
            var result = await _foundItemService.AdminGetAllItemsAsync(filter);
            return Ok(result);
        }

        [HttpPost("items/review")]
        public async Task<IActionResult> ReviewItem([FromBody] AdminReviewItemDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var adminId = GetAdminId();
            var result = await _foundItemService.AdminReviewItemAsync(adminId, dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("items/{id:guid}")]
        public async Task<IActionResult> DeleteItem(Guid id)
        {
            var adminId = GetAdminId();
            var result = await _foundItemService.AdminDeleteItemAsync(adminId, id);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        // ── CLAIMS ───────────────────────────────────────────────────────

        [HttpGet("items/{id:guid}/claims")]
        public async Task<IActionResult> GetItemClaims(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var adminId = GetAdminId();
            var result = await _foundItemService.GetItemClaimsAsync(adminId, id, page, pageSize);
            return Ok(result);
        }

        [HttpPost("claims/review")]
        public async Task<IActionResult> ReviewClaim([FromBody] AdminReviewClaimDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var adminId = GetAdminId();
            var result = await _foundItemService.AdminReviewClaimAsync(adminId, dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        // ── USERS ────────────────────────────────────────────────────────

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers([FromQuery] string? search, [FromQuery] bool? blocked, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var result = await _foundItemService.AdminGetUsersAsync(search, blocked, page, pageSize);
            return Ok(result);
        }

        [HttpPost("users/manage")]
        public async Task<IActionResult> ManageUser([FromBody] ManageUserDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var adminId = GetAdminId();
            var result = await _foundItemService.AdminManageUserAsync(adminId, dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        private Guid GetAdminId()
        {
            Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id);
            return id;
        }
    }
}
