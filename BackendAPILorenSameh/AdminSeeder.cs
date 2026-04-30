using BackendAPILorenSameh;
using BusinessLayer.Services;
using DataLayer.Models;
using Microsoft.EntityFrameworkCore;

namespace BackendAPILorenSameh
{
    public static class AdminSeeder
    {
        /// <summary>
        /// Call this once at startup (after migrations).
        /// Creates the default Admin account only if no Admin exists yet.
        /// Credentials are read from appsettings.json → "AdminSeed" section.
        /// </summary>
        public static async Task SeedAsync(IServiceProvider services)
        {
            // Resolve everything inside a fresh scope so we don't pollute
            // the singleton container.
            using var scope = services.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<DBContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IDataHasher>();
            var cipher = scope.ServiceProvider.GetRequiredService<IDataCiphers>();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var logger = scope.ServiceProvider
                                  .GetRequiredService<ILogger<Program>>();

            // ── Apply any pending migrations automatically ───────────────
            try
            {
                await db.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AdminSeeder: database migration failed.");
                throw;
            }

            // ── Skip if an Admin already exists ─────────────────────────
            bool adminExists = await db.Users
                .AnyAsync(u => u.AccountType == "Admin");

            if (adminExists)
            {
                logger.LogInformation("AdminSeeder: Admin account already exists — skipping.");
                return;
            }

            // ── Read seed values from configuration ──────────────────────
            var section = config.GetSection("AdminSeed");

            string email = section["Email"] ?? "admin@lostandfound.com";
            string password = section["Password"] ?? "Admin@1234";
            string userName = section["UserName"] ?? "admin";
            string fullName = section["FullName"] ?? "System Administrator";
            string phone = section["Phone"] ?? "01000000000";
            string nationalId = section["NationalId"] ?? "00000000000000";
            string address = section["Address"] ?? "Cairo, Egypt";
            string lan = section["Latitude"] ?? "30.044420";
            string lon = section["Longitude"] ?? "31.235712";

            // Normalise before hashing (same logic as the rest of the app)
            email = email.Trim().ToLowerInvariant();
            phone = phone.Trim();
            nationalId = nationalId.Trim();
            userName = userName.Trim();
            fullName = fullName.Trim();
            address = address.Trim();

            var now = DateTime.UtcNow;

            var admin = new User
            {
                UserId = Guid.NewGuid(),

                UserProfileImgURL = section["ProfileImageUrl"] ?? string.Empty,
                UserName = userName,
                FullName = fullName,

                // Sensitive fields stored encrypted + hashed for lookup
                EmailChipher = cipher.Encrypt(email),
                EmailHash = hasher.HashComparison(email),
                PhoneNumberChipher = cipher.Encrypt(phone),
                PhoneNumberHash = hasher.HashComparison(phone),
                NationalIdChipher = cipher.Encrypt(nationalId),
                NationalIdHash = hasher.HashComparison(nationalId),

                PasswordHash = hasher.HashData(password),

                // Location (Cairo default)
                Latitude = decimal.Parse(lan),
                Longitude = decimal.Parse(lon),
                Address = address,

                // Account flags
                AccountType = "Admin",
                JoinDate = now,
                Verified = true,   // Admin is always verified
                Login = false,
                Blocked = false,
                FailedLoginAttempts = 0,

                // OTP fields — zeroed out (admin doesn't need them)
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
                MailActionsResetAt = now,
                MailBlockedUntil = null,
                ValidationOtpWindowResetAt = now,
                ResetOtpWindowResetAt = now,
                OtpVerifyBlockedUntil = null,
                ResetOtpVerifyBlockedUntil = null,
                PasswordResetSessionTokenHash = null,
                PasswordResetSessionTokenExpiresAt = null,
                PasswordChangedAt = null
            };

            db.Users.Add(admin);
            await db.SaveChangesAsync();

            logger.LogWarning(
                "AdminSeeder: Default Admin account created. " +
                "UserName={UserName} | Email={Email} — " +
                "CHANGE THE PASSWORD immediately after first login!",
                userName, email);
        }
    }
}