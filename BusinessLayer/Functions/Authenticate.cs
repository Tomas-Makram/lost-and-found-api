using BusinessLayer.DTOs;
using BusinessLayer.Models;
using BusinessLayer.Models.BusinessLayer.Models;
using BusinessLayer.Services;
using DataLayer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BusinessLayer.Functions
{
    public interface IAuthenticate
    {
        ResponceApi<string> CreateNewAccount(CreateNewAccountDTO createAccount);
        Task<ResponceApi<LoginResponceDTO>> Login(LoginDTO login);
        ResponceApi<string> ChangePassword(ChangePasswordDTO changePassword);
        ResponceApi<string> ChangeFields(ChangeFieldsDTO changeFields);
        ResponceApi<MyAccountDTO> GetMyAccount(Guid userId);
        Task<ResponceApi<string>> Logout(Guid userId, Guid sessionId);
        Task<ResponceApi<string>> SendVerificationOtpAsync(Guid userId);
        Task<ResponceApi<bool>> VerifyAccountOtpAsync(VerifyAccountOtpDTO dto);

        Task<ResponceApi<string>> RequestPasswordResetOtpAsync(ForgotPasswordRequestDTO dto);
        Task<ResponceApi<ResetPasswordSessionResult>> VerifyPasswordResetOtpAsync(VerifyResetPasswordOtpDTO dto);
        Task<ResponceApi<bool>> ResetPasswordByTokenAsync(ResetPasswordByTokenDTO dto);
    }

    public class Authenticate : IAuthenticate
    {
        private readonly DBContext _data;
        private readonly IOtpService _otpService;
        private readonly IDataHasher _dataHasher;
        private readonly IDataCiphers _dataCiphers;
        private readonly IConfiguration _configuration;
        private readonly CairoTimeService _cairoTimeService;
        private readonly ITokenSessionService _tokenSessionService;
        private readonly IEmailTemplateService _emailTemplateService;
        private readonly int FailedLoginAttemptsCount = 5;
        public Authenticate(DBContext data, IDataHasher dataHasher,IOtpService otpService, IDataCiphers dataCiphers, IConfiguration configuration, CairoTimeService cairoTimeService, ITokenSessionService tokenSessionService, IEmailTemplateService emailTemplateService)
        {
            _data = data;
            _otpService = otpService;
            _dataHasher = dataHasher;
            _dataCiphers = dataCiphers;
            _configuration = configuration;
            _cairoTimeService = cairoTimeService;
            _tokenSessionService = tokenSessionService;
            _emailTemplateService = emailTemplateService;
        }

        //////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////Users Functions//////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////

        public ResponceApi<string> CreateNewAccount(CreateNewAccountDTO createAccount)
        {
            var responce = new ResponceApi<string>();

            try
            {
                // Normalize
                createAccount.Email = createAccount.Email.Trim().ToLower();
                createAccount.PhoneNumber = createAccount.PhoneNumber.Trim();
                createAccount.UserName = createAccount.UserName.Trim();
                createAccount.FullName = createAccount.FullName.Trim();
                createAccount.Address = createAccount.Address.Trim();
                createAccount.NationalId = createAccount.NationalId.Trim();

                // Extra manual validations
                if (createAccount.Password != createAccount.ConfirmPassword)
                {
                    responce.Success = false;
                    responce.Message = "Password confirmation does not match.";
                    responce.Errors = new List<string> { "ConfirmPassword does not match Password." };
                    return responce;
                }

                if (createAccount.Latitude < -90 || createAccount.Latitude > 90)
                {
                    responce.Success = false;
                    responce.Message = "Invalid latitude.";
                    responce.Errors = new List<string> { "Latitude must be between -90 and 90." };
                    return responce;
                }

                if (createAccount.Longitude < -180 || createAccount.Longitude > 180)
                {
                    responce.Success = false;
                    responce.Message = "Invalid longitude.";
                    responce.Errors = new List<string> { "Longitude must be between -180 and 180." };
                    return responce;
                }

                var hashEmail = _dataHasher.HashComparison(createAccount.Email);
                var hashPhone = _dataHasher.HashComparison(createAccount.PhoneNumber);
                var hashNational = _dataHasher.HashComparison(createAccount.NationalId);
                if (_data.Users.AsNoTracking().Any(u => u.EmailHash == hashEmail))
                {
                    responce.Success = false;
                    responce.Message = "This email is already registered.";
                    responce.Errors = new List<string> { "Duplicate email." };
                    return responce;
                }

                if (_data.Users.AsNoTracking().Any(u => u.PhoneNumberHash == hashPhone))
                {
                    responce.Success = false;
                    responce.Message = "This phone number is already registered.";
                    responce.Errors = new List<string> { "Duplicate phone number." };
                    return responce;
                }

                if (_data.Users.AsNoTracking().Any(u => u.NationalIdHash == hashNational))
                {
                    responce.Success = false;
                    responce.Message = "This National ID is already registered.";
                    responce.Errors = new List<string> { "Duplicate National ID." };
                    return responce;
                }

                if (_data.Users.AsNoTracking().Any(u => u.UserName == createAccount.UserName))
                {
                    responce.Success = false;
                    responce.Message = "Username already exists.";
                    responce.Errors = new List<string> { "Duplicate username." };
                    return responce;
                }

                var user = new User
                {
                    UserId = Guid.NewGuid(),
                    UserProfileImgURL = createAccount.UserProfileImgURL,
                    UserName = createAccount.UserName,
                    FullName = createAccount.FullName,
                    EmailChipher = _dataCiphers.Encrypt(createAccount.Email),
                    EmailHash = hashEmail,
                    PhoneNumberChipher = _dataCiphers.Encrypt(createAccount.PhoneNumber),
                    PhoneNumberHash = hashPhone,
                    PasswordHash = _dataHasher.HashData(createAccount.Password),
                    Latitude = createAccount.Latitude,
                    Longitude = createAccount.Longitude,
                    Address = createAccount.Address,
                    NationalIdChipher = _dataCiphers.Encrypt(createAccount.NationalId),
                    NationalIdHash = hashNational,
                    AccountType = createAccount.AccountType,
                    JoinDate = DateTime.UtcNow,
                    Verified = false,
                    Login = false,
                    Blocked = false,
                    FailedLoginAttempts = 0,

                    OtpHash = string.Empty,
                    OtpExpiresAt = null,
                    OtpAttempts = 0,
                    OtpLastSentAt = null,
                    OtpRequestsCount = 0,

                    PasswordResetOtpHash = null,
                    PasswordResetOtpExpiresAt = null,
                    PasswordResetOtpAttempts = 0,
                    LastMailSentResetPasswordAt = null,
                    PasswordOtpResetCount = 0,

                    MailActionsCount = 0,
                    MailActionsResetAt = DateTime.UtcNow,
                    MailBlockedUntil = null,
                    ValidationOtpWindowResetAt = DateTime.UtcNow,
                    ResetOtpWindowResetAt = DateTime.UtcNow,
                    OtpVerifyBlockedUntil = null,
                    ResetOtpVerifyBlockedUntil = null
                };

                _data.Users.Add(user);
                _data.SaveChanges();

                responce.Success = true;
                responce.Message = "Account created successfully.";
                responce.Data = user.UserId.ToString();
                _emailTemplateService.SendEmailAsync(_emailTemplateService.CreateWelcomeEmail(createAccount.Email, createAccount.FullName));
                return responce;
            }
            catch (Exception ex)
            {
                responce.Success = false;
                responce.Message = "Failed to create account.";
                responce.Errors = new List<string> { ex.Message };
                return responce;
            }
        }

        public async Task<ResponceApi<LoginResponceDTO>> Login(LoginDTO login)
        {
            var responce = new ResponceApi<LoginResponceDTO>();

            try
            {
                if (login == null)
                {
                    responce.Success = false;
                    responce.Message = "Login request is invalid.";
                    responce.Errors = new List<string> { "Request body is null." };
                    return responce;
                }

                if (string.IsNullOrWhiteSpace(login.EmailOrPhoneOrUsernameOrNationalId) ||
                    string.IsNullOrWhiteSpace(login.Password))
                {
                    responce.Success = false;
                    responce.Message = "Invalid email or password.";
                    responce.Errors = new List<string> { "Login credentials are required." };
                    return responce;
                }

                // Normalize input
                login.EmailOrPhoneOrUsernameOrNationalId = login.EmailOrPhoneOrUsernameOrNationalId.Trim().ToLower();

                var hashInput = _dataHasher.HashComparison(login.EmailOrPhoneOrUsernameOrNationalId);

                var userByEmail = await _data.Users.FirstOrDefaultAsync(u => u.EmailHash == hashInput);
                var userByPhone = await _data.Users.FirstOrDefaultAsync(u => u.PhoneNumberHash == hashInput);
                var userByUsername = await _data.Users.FirstOrDefaultAsync(u => u.UserName.ToLower() == login.EmailOrPhoneOrUsernameOrNationalId);
                var userByNationalId = await _data.Users.FirstOrDefaultAsync(u => u.NationalIdHash == hashInput);

                var user = userByEmail ?? userByPhone ?? userByUsername ?? userByNationalId;

                if (user == null)
                {
                    responce.Success = false;
                    responce.Message = "Invalid email or password.";
                    responce.Errors = new List<string> { "User not found." };
                    return responce;
                }

                if (user.Blocked)
                {
                    responce.Success = false;
                    responce.Message = "This account is blocked.";
                    responce.Errors = new List<string> { "Blocked account." };
                    return responce;
                }

                var checkPassword = _dataHasher.VerifyHashed(login.Password, user.PasswordHash);

                if (!checkPassword)
                {
                    user.FailedLoginAttempts++;

                    if (user.FailedLoginAttempts >= FailedLoginAttemptsCount)
                        user.Blocked = true;

                    await _data.SaveChangesAsync();

                    responce.Success = false;
                    responce.Message = "Invalid email or password.";
                    responce.Errors = new List<string> { "Wrong password." };
                    return responce;
                }

                // Reset failed attempts before creating session
                user.FailedLoginAttempts = 0;
                user.Login = true;
                user.LastLogin = DateTime.UtcNow;

                await _data.SaveChangesAsync();

                // Create session + access token + refresh token
                var sessionResult = await _tokenSessionService.CreateSessionAsync(user.UserId);

                if (!sessionResult.Success || sessionResult.Data == null)
                {
                    responce.Success = false;
                    responce.Message = "Login failed.";
                    responce.Errors = new List<string> { "Unable to create session." };
                    return responce;
                }

                responce.Success = true;
                responce.Message = "Login successful.";
                responce.Data = new LoginResponceDTO
                {
                    Token = sessionResult.Data.AccessToken,
                    ExpireAt = _cairoTimeService.UtcToCairo(sessionResult.Data.AccessTokenExpiresAt),
                    UserID = user.UserId,
                    RefreshToken = sessionResult.Data.RefreshToken,
                    RefreshTokenExpireAt = _cairoTimeService.UtcToCairo(sessionResult.Data.RefreshTokenExpiresAt),
                    SessionId = sessionResult.Data.SessionId
                };

                return responce;
            }
            catch (Exception ex)
            {
                responce.Success = false;
                responce.Message = "Login failed.";
                responce.Errors = new List<string> { ex.Message };
                return responce;
            }
        }

        public ResponceApi<string> ChangePassword(ChangePasswordDTO changePassword)
        {
            var responce = new ResponceApi<string>();

            User user = _data.Users.FirstOrDefault(u => u.UserId == changePassword.UserId)!;

            if (user == null)
            {
                responce.Success = false;
                responce.Message = "Invalid Account.";
                responce.Errors = new List<string> { "User not found." };
                return responce;
            }
            if (!_dataHasher.VerifyHashed(changePassword.OldPassword, user.PasswordHash))
            {
                responce.Success = false;
                responce.Message = "Invalid Old Password.";
                responce.Errors = new List<string> { "Old Password was wrong." };
                return responce;
            }
            if (changePassword.NewPassword != changePassword.ConfirmPassword)
            {
                responce.Success = false;
                responce.Message = "Password confirmation does not match.";
                responce.Errors = new List<string> { "ConfirmPassword does not match Password." };
                return responce;
            }

            user.PasswordHash = _dataHasher.HashData(changePassword.NewPassword);
            _data.SaveChanges();
            responce.Success = true;
            responce.Message = "Password Change Successfully.";
            responce.Data = user.UserId.ToString();
            _emailTemplateService.SendEmailAsync(_emailTemplateService.CreatePasswordChangedEmail(_dataCiphers.Decrypt(user.EmailChipher), user.FullName));

            return responce;
        }

        public ResponceApi<string> ChangeFields(ChangeFieldsDTO changeFields)
        {
            var responce = new ResponceApi<string>();
            User user = _data.Users.FirstOrDefault(u => u.UserId == changeFields.UserId)!;

            if (user == null)
            {
                responce.Success = false;
                responce.Message = "Invalid Account.";
                responce.Errors = new List<string> { "User not found." };
                return responce;
            }
            user.UserProfileImgURL = changeFields.UserProfileImgURL == string.Empty ? user.UserProfileImgURL : changeFields.UserProfileImgURL;
            user.FullName = changeFields.FullName == string.Empty ? user.FullName : changeFields.FullName;
            user.EmailChipher = changeFields.Email == string.Empty ? user.EmailChipher : _dataCiphers.Encrypt(changeFields.Email);
            user.EmailHash = changeFields.Email == string.Empty ? user.EmailHash : _dataHasher.HashComparison(changeFields.Email);
            user.PhoneNumberChipher = changeFields.PhoneNumber == string.Empty ? user.PhoneNumberChipher : _dataCiphers.Encrypt(changeFields.PhoneNumber);
            user.PhoneNumberHash = changeFields.PhoneNumber == string.Empty ? user.PhoneNumberHash : _dataHasher.HashComparison(changeFields.PhoneNumber);
            user.Latitude = user.Latitude == 0 ? user.Latitude : changeFields.Latitude;
            user.Longitude = user.Longitude == 0 ? user.Longitude : changeFields.Longitude;
            user.Address = changeFields.Address == string.Empty ? user.Address : changeFields.Address;
            user.NationalIdChipher = changeFields.NationalId == string.Empty ?  user.NationalIdChipher : _dataCiphers.Encrypt(changeFields.NationalId);
            user.NationalIdHash = changeFields.NationalId == string.Empty ? user.NationalIdHash : _dataHasher.HashComparison(changeFields.NationalId);
            
            if(changeFields.Email != string.Empty || changeFields.PhoneNumber != string.Empty || changeFields.NationalId != string.Empty)
                user.Verified = false;
    
            _data.SaveChanges();
            responce.Success = true;
            responce.Message = "Fields updated successfully.";
            responce.Data = user.UserId.ToString();
            return responce;
        }

        public ResponceApi<MyAccountDTO> GetMyAccount(Guid userId)
        { 
            var responce = new ResponceApi<MyAccountDTO>();
            User user = _data.Users.FirstOrDefault(u => u.UserId == userId)!;

            if (user == null)
            {
                responce.Success = false;
                responce.Message = "Invalid Account.";
                responce.Data = null;
                responce.Errors = new List<string> { "User not found." };
                return responce;
            }
            responce.Success = true;
            responce.Message = "Get Data User successfully.";
            responce.Data = new MyAccountDTO
            {
                UserId = userId,
                UserProfileImgURL = user.UserProfileImgURL,
                UserName = user.UserName,
                FullName = user.FullName,
                Email = _dataCiphers.Decrypt(user.EmailChipher),
                PhoneNumber = _dataCiphers.Decrypt(user.PhoneNumberChipher),
                NationalId = _dataCiphers.Decrypt(user.NationalIdChipher),
                Latitude = user.Latitude,
                Longitude = user.Longitude,
                Address = user.Address,
                AccountType = user.AccountType,
                JoinDate = _cairoTimeService.UtcToCairo(user.JoinDate),
                LastLogin = _cairoTimeService.UtcToCairo(user.LastLogin!.Value),
                Verified = user.Verified,
                Login = user.Login,
            };
            return responce;
        }

        public async Task<ResponceApi<string>> Logout(Guid userId, Guid sessionId)
        {
            var responce = new ResponceApi<string>();

            try
            {
                var user = await _data.Users.FirstOrDefaultAsync(u => u.UserId == userId);

                if (user == null)
                {
                    responce.Success = false;
                    responce.Message = "Invalid Account.";
                    responce.Errors = new List<string> { "User not found." };
                    return responce;
                }

                var revokeSessionResult = await _tokenSessionService.RevokeSessionAsync(sessionId);

                if (!revokeSessionResult.Success)
                {
                    responce.Success = false;
                    responce.Message = revokeSessionResult.Message ?? "Logout failed.";
                    responce.Errors = revokeSessionResult.Errors ?? new List<string> { "Unable to revoke session." };
                    return responce;
                }

                var hasAnotherActiveSession = await _data.UserSessions
                    .AnyAsync(s => s.UserId == userId && s.IsActive);

                user.Login = hasAnotherActiveSession;

                await _data.SaveChangesAsync();

                responce.Success = true;
                responce.Message = "Logout successful.";
                responce.Data = user.UserId.ToString();
                return responce;
            }
            catch (Exception ex)
            {
                responce.Success = false;
                responce.Message = "Logout failed.";
                responce.Errors = new List<string> { ex.Message };
                return responce;
            }
        }

        public async Task<ResponceApi<string>> SendVerificationOtpAsync(Guid userId)
        {
            try
            {
                var user = await _data.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                if (user == null)
                    return ResponceApi<string>.Fail("Invalid account.", "User not found.");

                if (user.Blocked)
                    return ResponceApi<string>.Fail("This account is blocked.", "Blocked account.");

                if (user.Verified)
                    return ResponceApi<string>.Fail("Account is already verified.");

                var otpResult = await _otpService.SendAccountVerificationOtpAsync(userId);

                if (!otpResult.Success || string.IsNullOrWhiteSpace(otpResult.Data))
                    return ResponceApi<string>.Fail(
                        otpResult.Message ?? "Unable to send verification OTP.",
                        otpResult.Errors?.ToArray() ?? Array.Empty<string>());

                var decryptedEmail = _dataCiphers.Decrypt(user.EmailChipher);

                await _emailTemplateService.SendEmailAsync(
                    _emailTemplateService.CreateOtpVerificationEmail(
                        decryptedEmail,
                        otpResult.Data,
                        user.FullName)
                );

                return ResponceApi<string>.Ok(user.UserId.ToString(), "Verification OTP sent successfully.");
            }
            catch (Exception ex)
            {
                return ResponceApi<string>.Fail("Failed to send verification OTP.", ex.Message);
            }
        }

        public async Task<ResponceApi<bool>> VerifyAccountOtpAsync(VerifyAccountOtpDTO dto)
        {
            try
            {
                var user = await _data.Users.FirstOrDefaultAsync(u => u.UserId == dto.UserId);
                if (user == null)
                    return ResponceApi<bool>.Fail("Invalid account.", "User not found.");

                var result = await _otpService.VerifyAccountOtpAsync(dto.UserId, dto.OtpCode.Trim());

                if (!result.Success)
                    return ResponceApi<bool>.Fail(
                        result.Message ?? "Verification failed.",
                        result.Errors?.ToArray() ?? Array.Empty<string>());

                return ResponceApi<bool>.Ok(true, "Account verified successfully.");
            }
            catch (Exception ex)
            {
                return ResponceApi<bool>.Fail("Verification failed.", ex.Message);
            }
        }

        public async Task<ResponceApi<string>> RequestPasswordResetOtpAsync(ForgotPasswordRequestDTO dto)
        {
            try
            {
                var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
                var emailHash = _dataHasher.HashComparison(normalizedEmail);

                var user = await _data.Users.FirstOrDefaultAsync(u => u.EmailHash == emailHash);

                // Prevent user enumeration
                if (user == null || user.Blocked)
                {
                    return ResponceApi<string>.Ok(
                        null,
                        "If an account exists with this email, a password reset OTP will be sent."
                    );
                }

                var otpResult = await _otpService.SendResetPasswordOtpAsync(user.UserId);

                if (!otpResult.Success || string.IsNullOrWhiteSpace(otpResult.Data))
                {
                    return ResponceApi<string>.Ok(
                        null,
                        "If an account exists with this email, a password reset OTP will be sent."
                    );
                }

                var decryptedEmail = _dataCiphers.Decrypt(user.EmailChipher);

                await _emailTemplateService.SendEmailAsync(
                    _emailTemplateService.CreatePasswordResetOtpEmail(
                        decryptedEmail,
                        otpResult.Data,
                        user.FullName)
                );

                return ResponceApi<string>.Ok(
                    null,
                    "If an account exists with this email, a password reset OTP will be sent."
                );
            }
            catch
            {
                return ResponceApi<string>.Ok(
                    null,
                    "If an account exists with this email, a password reset OTP will be sent."
                );
            }
        }

        public async Task<ResponceApi<ResetPasswordSessionResult>> VerifyPasswordResetOtpAsync(VerifyResetPasswordOtpDTO dto)
        {
            try
            {
                var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
                var emailHash = _dataHasher.HashComparison(normalizedEmail);

                var user = await _data.Users.FirstOrDefaultAsync(u => u.EmailHash == emailHash);
                if (user == null || user.Blocked)
                    return ResponceApi<ResetPasswordSessionResult>.Fail("Invalid request.");

                var result = await _otpService.VerifyResetPasswordOtpAsync(user.UserId, dto.OtpCode.Trim());

                if (!result.Success)
                    return ResponceApi<ResetPasswordSessionResult>.Fail(
                        result.Message ?? "OTP verification failed.",
                        result.Errors?.ToArray() ?? Array.Empty<string>());

                return result;
            }
            catch (Exception ex)
            {
                return ResponceApi<ResetPasswordSessionResult>.Fail("OTP verification failed.", ex.Message);
            }
        }

        public async Task<ResponceApi<bool>> ResetPasswordByTokenAsync(ResetPasswordByTokenDTO dto)
        {
            try
            {
                if (dto.NewPassword != dto.ConfirmPassword)
                {
                    return ResponceApi<bool>.Fail(
                        "Password confirmation does not match.",
                        "ConfirmPassword does not match NewPassword."
                    );
                }

                var user = await _data.Users.FirstOrDefaultAsync(u => u.UserId == dto.UserId);
                if (user == null)
                    return ResponceApi<bool>.Fail("Invalid account.", "User not found.");

                var result = await _otpService.ResetPasswordWithTokenAsync(
                    dto.UserId,
                    dto.ResetToken,
                    dto.NewPassword
                );

                if (!result.Success)
                {
                    return ResponceApi<bool>.Fail(
                        result.Message ?? "Password reset failed.",
                        result.Errors?.ToArray() ?? Array.Empty<string>());
                }

                var decryptedEmail = _dataCiphers.Decrypt(user.EmailChipher);

                await _emailTemplateService.SendEmailAsync(
                    _emailTemplateService.CreatePasswordChangedEmail(decryptedEmail, user.FullName)
                );

                return ResponceApi<bool>.Ok(true, "Password reset successfully.");
            }
            catch (Exception ex)
            {
                return ResponceApi<bool>.Fail("Password reset failed.", ex.Message);
            }
        }
    }
}
