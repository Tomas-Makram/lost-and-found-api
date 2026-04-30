using BusinessLayer.DTOs;
using BusinessLayer.Models;
using BusinessLayer.Models.BusinessLayer.Models;
using DataLayer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace BusinessLayer.Services
{
    public interface IOtpService
    {
        Task<ResponceApi<string>> SendAccountVerificationOtpAsync(Guid userId, bool bypassCooldown = false);
        Task<ResponceApi<bool>> VerifyAccountOtpAsync(Guid userId, string otpCode);

        Task<ResponceApi<string>> SendResetPasswordOtpAsync(Guid userId);
        Task<ResponceApi<ResetPasswordSessionResult>> VerifyResetPasswordOtpAsync(Guid userId, string otpCode);
        Task<ResponceApi<bool>> ResetPasswordWithTokenAsync(Guid userId, string resetToken, string newPassword);

        Task<bool> CanSendMailOTPOrReset(Guid userId);

        int GetOtpExpiryMinutes();
        int GetMaxOtpAttempts();
        int GetResendCooldownMinutes();
    }

    public class OtpService : IOtpService
    {
        private readonly DBContext _db;
        private readonly IDataHasher _dataHasher;
        private readonly ILogger<OtpService> _logger;
        private readonly OtpSettings _otpSettings;

        public OtpService(DBContext db, IDataHasher dataHasher, ILogger<OtpService> logger, IOptions<OtpSettings> otpOptions)
        {
            _db = db;
            _dataHasher = dataHasher;
            _logger = logger;
            _otpSettings = otpOptions.Value;
        }

        public int GetOtpExpiryMinutes() => _otpSettings.OtpExpiryMinutes;
        public int GetMaxOtpAttempts() => _otpSettings.MaxOtpAttempts;
        public int GetResendCooldownMinutes() => _otpSettings.ResendCooldownMinutes;

        private static DateTime UtcNow() => DateTime.UtcNow;

        private string GenerateOtpCode()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            var number = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 1_000_000;
            return number.ToString("D6");
        }

        private string GenerateSecureToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        }

        private bool IsActiveOtp(string? hash, DateTime? expiresAt)
        {
            if (string.IsNullOrWhiteSpace(hash))
                return false;

            if (!expiresAt.HasValue)
                return false;

            return expiresAt.Value > UtcNow();
        }

        private int RemainingAttempts(int used) => Math.Max(0, _otpSettings.MaxOtpAttempts - used);

        private async Task EnsureGlobalMailWindowAsync(User user)
        {
            var now = UtcNow();

            if (user.MailActionsResetAt <= now.AddDays(-_otpSettings.MailWindowDays))
            {
                user.MailActionsCount = 0;
                user.MailActionsResetAt = now;
                user.MailBlockedUntil = null;

                await _db.SaveChangesAsync();
            }
        }

        private async Task EnsureValidationWindowAsync(User user)
        {
            var now = UtcNow();

            if (user.ValidationOtpWindowResetAt <= now.AddDays(-_otpSettings.MailWindowDays))
            {
                user.OtpRequestsCount = 0;
                user.ValidationOtpWindowResetAt = now;
                user.OtpVerifyBlockedUntil = null;

                await _db.SaveChangesAsync();
            }
        }

        private async Task EnsureResetWindowAsync(User user)
        {
            var now = UtcNow();

            if (user.ResetOtpWindowResetAt <= now.AddDays(-_otpSettings.MailWindowDays))
            {
                user.PasswordOtpResetCount = 0;
                user.ResetOtpWindowResetAt = now;
                user.ResetOtpVerifyBlockedUntil = null;

                await _db.SaveChangesAsync();
            }
        }

        public async Task<bool> CanSendMailOTPOrReset(Guid userId)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
                return false;

            var now = UtcNow();

            if (user.MailBlockedUntil.HasValue && user.MailBlockedUntil.Value > now)
                return false;

            await EnsureGlobalMailWindowAsync(user);

            if (user.MailActionsCount >= _otpSettings.MaxMailsInWindow)
            {
                user.MailBlockedUntil = now.AddDays(_otpSettings.MailWindowDays);
                await _db.SaveChangesAsync();
                return false;
            }

            return true;
        }

        private async Task<bool> CanSendValidationOtpAsync(User user, bool bypassCooldown)
        {
            var now = UtcNow();

            if (user.Blocked)
                return false;

            if (user.Verified)
                return false;

            await EnsureValidationWindowAsync(user);

            if (!await CanSendMailOTPOrReset(user.UserId))
                return false;

            if (user.OtpRequestsCount >= _otpSettings.MaxValidationSendsPerWindow)
                return false;

            if (!bypassCooldown &&
                user.OtpLastSentAt.HasValue &&
                now < user.OtpLastSentAt.Value.AddMinutes(_otpSettings.ResendCooldownMinutes))
            {
                return false;
            }

            return true;
        }

        private async Task<string> CreateAndSaveValidationOtpAsync(User user)
        {
            var now = UtcNow();
            var otp = GenerateOtpCode();

            user.OtpHash = _dataHasher.HashData(otp);
            user.OtpExpiresAt = now.AddMinutes(_otpSettings.OtpExpiryMinutes);
            user.OtpAttempts = 0;
            user.OtpLastSentAt = now;
            user.OtpRequestsCount++;
            user.MailActionsCount++;

            await _db.SaveChangesAsync();

            return otp;
        }

        public async Task<ResponceApi<string>> SendAccountVerificationOtpAsync(Guid userId, bool bypassCooldown = false)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
                return ResponceApi<string>.Fail("User not found.");

            if (!await CanSendValidationOtpAsync(user, bypassCooldown))
                return ResponceApi<string>.Fail("Unable to send verification OTP at this time.");

            var otp = await CreateAndSaveValidationOtpAsync(user);

            return ResponceApi<string>.Ok(otp, "Verification OTP generated successfully.");
        }

        public async Task<ResponceApi<bool>> VerifyAccountOtpAsync(Guid userId, string otpCode)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
                return ResponceApi<bool>.Fail("User not found.");

            var now = UtcNow();

            if (user.Blocked)
                return ResponceApi<bool>.Fail("Account is blocked.");

            if (user.Verified)
                return ResponceApi<bool>.Fail("Account is already verified.");

            if (user.OtpVerifyBlockedUntil.HasValue && user.OtpVerifyBlockedUntil.Value > now)
                return ResponceApi<bool>.Fail("OTP verification is temporarily blocked.");

            if (!IsActiveOtp(user.OtpHash, user.OtpExpiresAt))
                return ResponceApi<bool>.Fail("OTP is invalid or expired.");

            if (RemainingAttempts(user.OtpAttempts) <= 0)
            {
                user.OtpHash = string.Empty;
                user.OtpExpiresAt = null;
                user.OtpVerifyBlockedUntil = now.AddMinutes(_otpSettings.VerifyBlockMinutes);
                await _db.SaveChangesAsync();

                return ResponceApi<bool>.Fail("Maximum OTP attempts exceeded.");
            }

            var isValid = _dataHasher.VerifyHashed(otpCode, user.OtpHash);

            if (!isValid)
            {
                user.OtpAttempts++;

                if (RemainingAttempts(user.OtpAttempts) <= 0)
                {
                    user.OtpHash = string.Empty;
                    user.OtpExpiresAt = null;
                    user.OtpVerifyBlockedUntil = now.AddMinutes(_otpSettings.VerifyBlockMinutes);
                }

                await _db.SaveChangesAsync();

                return ResponceApi<bool>.Fail(
                    "Invalid OTP code.",
                    $"Remaining attempts: {RemainingAttempts(user.OtpAttempts)}"
                );
            }

            user.Verified = true;
            user.OtpHash = string.Empty;
            user.OtpExpiresAt = null;
            user.OtpAttempts = 0;
            user.OtpVerifyBlockedUntil = null;

            await _db.SaveChangesAsync();

            return ResponceApi<bool>.Ok(true, "Account verified successfully.");
        }

        private async Task<bool> CanSendResetOtpAsync(User user)
        {
            var now = UtcNow();

            if (user.Blocked)
                return false;

            await EnsureResetWindowAsync(user);

            if (!await CanSendMailOTPOrReset(user.UserId))
                return false;

            if (user.PasswordOtpResetCount >= _otpSettings.MaxResetSendsPerWindow)
                return false;

            if (user.LastMailSentResetPasswordAt.HasValue &&
                now < user.LastMailSentResetPasswordAt.Value.AddMinutes(_otpSettings.ResendCooldownMinutes))
            {
                return false;
            }

            return true;
        }

        public async Task<ResponceApi<string>> SendResetPasswordOtpAsync(Guid userId)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
                return ResponceApi<string>.Fail("User not found.");

            if (!await CanSendResetOtpAsync(user))
                return ResponceApi<string>.Fail("Unable to send password reset OTP at this time.");

            var now = UtcNow();
            var otp = GenerateOtpCode();

            user.PasswordResetOtpHash = _dataHasher.HashData(otp);
            user.PasswordResetOtpExpiresAt = now.AddMinutes(_otpSettings.OtpExpiryMinutes);
            user.PasswordResetOtpAttempts = 0;
            user.LastMailSentResetPasswordAt = now;
            user.PasswordOtpResetCount++;
            user.MailActionsCount++;

            user.PasswordResetSessionTokenHash = null;
            user.PasswordResetSessionTokenExpiresAt = null;

            await _db.SaveChangesAsync();

            return ResponceApi<string>.Ok(otp, "Password reset OTP generated successfully.");
        }

        public async Task<ResponceApi<ResetPasswordSessionResult>> VerifyResetPasswordOtpAsync(Guid userId, string otpCode)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
                return ResponceApi<ResetPasswordSessionResult>.Fail("User not found.");

            var now = UtcNow();

            if (user.Blocked)
                return ResponceApi<ResetPasswordSessionResult>.Fail("Account is blocked.");

            if (user.ResetOtpVerifyBlockedUntil.HasValue && user.ResetOtpVerifyBlockedUntil.Value > now)
                return ResponceApi<ResetPasswordSessionResult>.Fail("Reset OTP verification is temporarily blocked.");

            if (!IsActiveOtp(user.PasswordResetOtpHash, user.PasswordResetOtpExpiresAt))
                return ResponceApi<ResetPasswordSessionResult>.Fail("OTP is invalid or expired.");

            if (RemainingAttempts(user.PasswordResetOtpAttempts) <= 0)
            {
                user.PasswordResetOtpHash = null;
                user.PasswordResetOtpExpiresAt = null;
                user.ResetOtpVerifyBlockedUntil = now.AddMinutes(_otpSettings.VerifyBlockMinutes);
                await _db.SaveChangesAsync();

                return ResponceApi<ResetPasswordSessionResult>.Fail("Maximum OTP attempts exceeded.");
            }

            var isValid = _dataHasher.VerifyHashed(otpCode, user.PasswordResetOtpHash!);

            if (!isValid)
            {
                user.PasswordResetOtpAttempts++;

                if (RemainingAttempts(user.PasswordResetOtpAttempts) <= 0)
                {
                    user.PasswordResetOtpHash = null;
                    user.PasswordResetOtpExpiresAt = null;
                    user.ResetOtpVerifyBlockedUntil = now.AddMinutes(_otpSettings.VerifyBlockMinutes);
                }

                await _db.SaveChangesAsync();

                return ResponceApi<ResetPasswordSessionResult>.Fail("Invalid OTP code.", $"Remaining attempts: {RemainingAttempts(user.PasswordResetOtpAttempts)}");
            }

            user.PasswordResetOtpHash = null;
            user.PasswordResetOtpExpiresAt = null;
            user.PasswordResetOtpAttempts = 0;
            user.ResetOtpVerifyBlockedUntil = null;
            user.PasswordOtpResetCount = 0;

            var rawResetToken = GenerateSecureToken();
            user.PasswordResetSessionTokenHash = _dataHasher.HashData(rawResetToken);
            user.PasswordResetSessionTokenExpiresAt = now.AddMinutes(_otpSettings.ResetSessionTokenExpiryMinutes);

            await _db.SaveChangesAsync();

            return ResponceApi<ResetPasswordSessionResult>.Ok(
                new ResetPasswordSessionResult
                {
                    UserId = user.UserId,
                    ResetToken = rawResetToken,
                    ExpiresAtUtc = user.PasswordResetSessionTokenExpiresAt.Value
                },
                "Reset OTP verified successfully."
            );
        }

        public async Task<ResponceApi<bool>> ResetPasswordWithTokenAsync(Guid userId, string resetToken, string newPassword)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
                return ResponceApi<bool>.Fail("User not found.");

            var now = UtcNow();

            if (user.Blocked)
                return ResponceApi<bool>.Fail("Account is blocked.");

            if (string.IsNullOrWhiteSpace(user.PasswordResetSessionTokenHash) || !user.PasswordResetSessionTokenExpiresAt.HasValue || user.PasswordResetSessionTokenExpiresAt.Value <= now) 
            {
                return ResponceApi<bool>.Fail("Reset session is invalid or expired.");
            }

            var tokenValid = _dataHasher.VerifyHashed(resetToken, user.PasswordResetSessionTokenHash);

            if (!tokenValid)
                return ResponceApi<bool>.Fail("Invalid reset token.");

            user.PasswordHash = _dataHasher.HashData(newPassword);
            user.PasswordChangedAt = now;
            user.FailedLoginAttempts = 0;

            user.PasswordResetSessionTokenHash = null;
            user.PasswordResetSessionTokenExpiresAt = null;

            user.PasswordResetOtpHash = null;
            user.PasswordResetOtpExpiresAt = null;
            user.PasswordResetOtpAttempts = 0;
            user.ResetOtpVerifyBlockedUntil = null;

            await _db.SaveChangesAsync();

            return ResponceApi<bool>.Ok(true, "Password reset successfully.");
        }
    }
}