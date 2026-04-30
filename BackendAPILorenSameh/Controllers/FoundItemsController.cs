using Asp.Versioning;
using BusinessLayer.Attributes;
using BusinessLayer.DTOs;
using BusinessLayer.Models;
using BusinessLayer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace BackendAPILorenSameh.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    public class FoundItemsController : ControllerBase
    {
        private readonly IFoundItemService _foundItemService;

        public FoundItemsController(IFoundItemService foundItemService)
        {
            _foundItemService = foundItemService;
        }

        // ── PUBLIC ───────────────────────────────────────────────────────

        /// <summary>Browse all published found items (filterable)</summary>
        [HttpGet]
        [EnableRateLimiting(RateLimitSetup.ApiDefaultPolicy)]
        public async Task<IActionResult> GetPublished([FromQuery] FoundItemFilterDTO filter)
        {
            Guid? userId = TryGetUserId();
            var result = await _foundItemService.GetPublishedAsync(filter, userId);
            return Ok(result);
        }

        /// <summary>Get a single item by ID</summary>
        [HttpGet("{id:guid}")]
        [EnableRateLimiting(RateLimitSetup.ApiDefaultPolicy)]
        public async Task<IActionResult> GetById(Guid id)
        {
            Guid? userId = TryGetUserId();
            var result = await _foundItemService.GetByIdAsync(id, userId);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        // ── AUTHENTICATED USER ────────────────────────────────────────────

        /// <summary>Report a new found item</summary>
        [HttpPost]
        [Authorize]
        [RequireActiveLogin]
        [EnableRateLimiting(RateLimitSetup.RegisterPolicy)]
        public async Task<IActionResult> Create([FromBody] CreateFoundItemDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var userId = GetUserId();
            var result = await _foundItemService.CreateAsync(userId, dto);
            if (!result.Success) return BadRequest(result);
            return CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result);
        }

        /// <summary>Update your own item (re-submits for review if rejected)</summary>
        [HttpPut("{id:guid}")]
        [Authorize]
        [RequireActiveLogin]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFoundItemDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var userId = GetUserId();
            var result = await _foundItemService.UpdateAsync(userId, id, dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        /// <summary>Archive/delete your own item</summary>
        [HttpDelete("{id:guid}")]
        [Authorize]
        [RequireActiveLogin]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = GetUserId();
            var result = await _foundItemService.DeleteAsync(userId, id);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        /// <summary>Get items you have reported</summary>
        [HttpGet("mine")]
        [Authorize]
        [RequireActiveLogin]
        public async Task<IActionResult> GetMyItems([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userId = GetUserId();
            var result = await _foundItemService.GetMyItemsAsync(userId, page, pageSize);
            return Ok(result);
        }

        /// <summary>Submit a claim attempt (one per user per item)</summary>
        [HttpPost("claim")]
        [Authorize]
        [RequireActiveLogin]
        [EnableRateLimiting(RateLimitSetup.BurstPerPathPolicy)]
        public async Task<IActionResult> SubmitClaim([FromBody] SubmitClaimDTO dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var userId = GetUserId();
            var result = await _foundItemService.SubmitClaimAsync(userId, dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        /// <summary>Get claim attempts for an item (owner or admin only)</summary>
        [HttpGet("{id:guid}/claims")]
        [Authorize]
        [RequireActiveLogin]
        public async Task<IActionResult> GetItemClaims(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userId = GetUserId();
            var result = await _foundItemService.GetItemClaimsAsync(userId, id, page, pageSize);
            if (!result.Success) return Forbid();
            return Ok(result);
        }

        // ── HELPERS ───────────────────────────────────────────────────────

        private Guid GetUserId()
        {
            Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id);
            return id;
        }

        private Guid? TryGetUserId()
        {
            var val = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(val, out var id) ? id : null;
        }
    }
}