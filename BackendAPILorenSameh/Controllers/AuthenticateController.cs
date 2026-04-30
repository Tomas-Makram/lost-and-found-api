using Asp.Versioning;
using BusinessLayer.Attributes;
using BusinessLayer.DTOs;
using BusinessLayer.Functions;
using BusinessLayer.Models;
using BusinessLayer.Services;
using DataLayer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace BackendAPILorenSameh.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    public class AuthenticateController : ControllerBase
    {

        private readonly DBContext _data;
        private readonly IAuthenticate _authenticate;
        private readonly ITokenSessionService _tokenSessionService;

        public AuthenticateController(DBContext data, IAuthenticate authenticate, IEmailSender emailSender, ITokenSessionService tokenSessionService)
        {
            _data = data;
            _authenticate = authenticate;
            _tokenSessionService = tokenSessionService;
        }

        [HttpPost("Login")]
        [EnableRateLimiting(RateLimitSetup.LoginPolicy)]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authenticate.Login(dto);

            if (!result.Success)
                return Unauthorized(result);

            return Ok(result);
        }


        [HttpPost("Signup")]
        [EnableRateLimiting(RateLimitSetup.RegisterPolicy)]
        public IActionResult CreateAccount([FromBody] CreateNewAccountDTO createNewAccount)
        {

            if (ModelState.IsValid)
                return Ok(_authenticate.CreateNewAccount(createNewAccount));
            else
                return BadRequest();
        }

        [HttpGet("me")]
        [RequireActiveLogin]
        [EnableRateLimiting(RateLimitSetup.BurstPerPathPolicy)]
        public IActionResult GetMyAccount()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            return Ok(_authenticate.GetMyAccount(userId));
        }

        [Authorize]
        [RequireActiveLogin]
        [HttpPost("changePassword")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            dto.UserId = userId;

            var result = _authenticate.ChangePassword(dto);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        [Authorize]
        [RequireActiveLogin]
        [HttpPost("changeFields")]
        public async Task<IActionResult> ChangeFields([FromBody] ChangeFieldsDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            dto.UserId = userId;

            var result = _authenticate.ChangeFields(dto);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        [Authorize]
        [RequireActiveLogin]
        [HttpPost("sendVerificationOtp")]
        public async Task<IActionResult> SendVerificationOtp()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var result = await _authenticate.SendVerificationOtpAsync(userId);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        [Authorize]
        [RequireActiveLogin]
        [HttpPost("verifyAccountOtp")]
        public async Task<IActionResult> VerifyAccountOtp([FromBody] VerifyAccountOtpDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var currentUserId))
                return Unauthorized();

            if (dto.UserId != currentUserId)
                return Forbid();

            var result = await _authenticate.VerifyAccountOtpAsync(dto);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpPost("forgotPassword/requestOtp")]
        public async Task<IActionResult> RequestPasswordResetOtp([FromBody] ForgotPasswordRequestDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authenticate.RequestPasswordResetOtpAsync(dto);

            return Ok(result);
        }

        [HttpPost("forgotPassword/verifyOtp")]
        public async Task<IActionResult> VerifyPasswordResetOtp([FromBody] VerifyResetPasswordOtpDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authenticate.VerifyPasswordResetOtpAsync(dto);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpPost("forgotPassword/reset")]
        public async Task<IActionResult> ResetPasswordByToken([FromBody] ResetPasswordByTokenDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authenticate.ResetPasswordByTokenAsync(dto);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        [Authorize]
        [RequireActiveLogin]
        [HttpPost("Logout")]
        public async Task<IActionResult> Logout()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var sessionIdClaim = User.FindFirst("session_id")?.Value;

            if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized(ResponceApi<string>.Fail("Invalid token.", "UserId claim is missing or invalid."));

            if (string.IsNullOrWhiteSpace(sessionIdClaim) || !Guid.TryParse(sessionIdClaim, out var sessionId))
                return Unauthorized(ResponceApi<string>.Fail("Invalid token.", "SessionId claim is missing or invalid."));

            var result = await _authenticate.Logout(userId, sessionId);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _tokenSessionService.RefreshSessionAsync(dto);

            if (!result.Success)
                return Unauthorized(result);

            return Ok(result);
        }
    }
}