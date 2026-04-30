using Asp.Versioning;
using BusinessLayer.Filters;
using BusinessLayer.Functions;
using BusinessLayer.Services;
using DataLayer.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using StackExchange.Redis;
using System.Text;
using System.Text.Json;

namespace BackendAPILorenSameh
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
            builder.Services.AddOpenApi();
            builder.Services.AddEndpointsApiExplorer();

            // ── SignalR ──────────────────────────────────────────────────────
            builder.Services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = builder.Environment.IsDevelopment();
            });

            // ── Options Binding ──────────────────────────────────────────────
            builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
            builder.Services.Configure<SessionSettings>(builder.Configuration.GetSection("SessionSettings"));
            builder.Services.Configure<OtpSettings>(builder.Configuration.GetSection("OtpSettings"));
            builder.Services.Configure<PasswordHashSettings>(builder.Configuration.GetSection("PasswordHashSettings"));
            builder.Services.Configure<DataProtectionSettings>(builder.Configuration.GetSection("DataProtectionSettings"));
            builder.Services.Configure<CacheSettings>(builder.Configuration.GetSection("CacheSettings"));
            builder.Services.Configure<UserThrottleOptions>(builder.Configuration.GetSection("UserThrottle"));
            builder.Services.Configure<RateLimitSettings>(builder.Configuration.GetSection("RateLimitSettings"));

            // ── Swagger ──────────────────────────────────────────────────────
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Lost & Found API",
                    Version = "v1",
                    Description = "API for managing lost and found items with real-time chat and notifications."
                });

                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Name = "Authorization",
                    Description = "Enter: Bearer {your token}"
                });

                options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
                });

                options.OperationFilter<CsrfHeaderOperationFilter>();
            });

            // ── API Versioning ───────────────────────────────────────────────
            builder.Services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = false;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

            // ── Database ─────────────────────────────────────────────────────
            builder.Services.AddDbContext<DBContext>(options =>
            {
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("Connection"),
                    b => b.MigrationsAssembly("BackendAPILorenSameh")
                );
            });

            // ── JWT ──────────────────────────────────────────────────────────
            var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
            if (string.IsNullOrWhiteSpace(jwtSettings.Key) || Encoding.UTF8.GetByteCount(jwtSettings.Key) < 32)
                throw new InvalidOperationException("Jwt:Key must be at least 32 bytes.");

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = true;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
                    ClockSkew = TimeSpan.Zero
                };

                // Support SignalR token in query string
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            (path.StartsWithSegments("/hubs/notifications") || path.StartsWithSegments("/hubs/chat")))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        var response = new { success = false, message = "You are not logged in, please log in first.", errors = new[] { "Unauthorized" } };
                        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";
                        var response = new { success = false, message = "You do not have permission to access this resource.", errors = new[] { "Forbidden" } };
                        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
                    }
                };
            });

            // ── Authorization ────────────────────────────────────────────────
            builder.Services.AddScoped<RequireActiveLoginFilter>();
            builder.Services.AddAuthorization();

            // ── Core App Services ────────────────────────────────────────────
            builder.Services.AddScoped<IAuthenticate, Authenticate>();
            builder.Services.AddScoped<CairoTimeService>();
            builder.Services.AddDataProtection();

            builder.Services.AddSingleton<IDataCiphers, DataCiphers>();
            builder.Services.AddSingleton<IDataHasher, DataHasher>();

            builder.Services.AddScoped<IImageStorageService, ImageStorageService>();
            builder.Services.AddScoped<IOtpService, OtpService>();
            builder.Services.AddScoped<ITokenSessionService, TokenSessionService>();

            builder.Services.AddTransient<IEmailSender, MailService>();
            builder.Services.AddScoped<IEmailTemplateService, MailService>();

            // ── New Feature Services ─────────────────────────────────────────
            builder.Services.AddScoped<IFoundItemService, FoundItemService>();
            builder.Services.AddScoped<IChatService, ChatService>();
            builder.Services.AddScoped<INotificationService, NotificationService>();

            // ── Rate Limiting ────────────────────────────────────────────────
            builder.Services.AddAppRateLimiting(builder.Configuration);

            // ── Cache / Redis ────────────────────────────────────────────────
            var cacheSettings = builder.Configuration.GetSection("CacheSettings").Get<CacheSettings>() ?? new CacheSettings();
            builder.Services.AddMemoryCache();

            if (cacheSettings.UseRedis)
            {
                var redisOptions = ConfigurationOptions.Parse(cacheSettings.RedisConnection);
                redisOptions.AbortOnConnectFail = false;
                redisOptions.ConnectRetry = 3;
                redisOptions.ConnectTimeout = 5000;
                redisOptions.SyncTimeout = 5000;

                var redis = ConnectionMultiplexer.Connect(redisOptions);
                builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
                builder.Services.AddSingleton<ISecurityCounterStore, RedisSecurityCounterStore>();
            }
            else
            {
                builder.Services.AddSingleton<ISecurityCounterStore, MemorySecurityCounterStore>();
            }

            builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

            // ── CORS ─────────────────────────────────────────────────────────
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("DefaultCorsPolicy", policy =>
                {
                    policy.WithOrigins(allowedOrigins ?? Array.Empty<string>())
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials(); // Required for SignalR
                });
            });

            // ── Antiforgery ──────────────────────────────────────────────────
            builder.Services.AddAntiforgery(options =>
            {
                var csrfSection = builder.Configuration.GetSection("Csrf");
                options.HeaderName = csrfSection["HeaderName"] ?? "X-CSRF-TOKEN";
                options.SuppressXFrameOptionsHeader = true;
                options.Cookie.Name = csrfSection["CookieName"] ?? "__Host-X-CSRF-COOKIE";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.IsEssential = true;
            });

            // ── Static Files (for uploaded images) ──────────────────────────
            builder.Services.AddDirectoryBrowser();

            var app = builder.Build();

            // ── Middleware pipeline ──────────────────────────────────────────
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.UseSwagger();
                app.UseSwaggerUI(u =>
                {
                    u.SwaggerEndpoint("/swagger/v1/swagger.json", "Lost & Found API V1");
                    u.RoutePrefix = "swagger";
                });
            }

            app.UseHttpsRedirection();
            app.UseMiddleware<ExceptionHandlingMiddleware>();
            app.UseResponseCompression();
            app.UseMiddleware<SecurityHeadersMiddleware>();
            app.UseStaticFiles(); // serve /uploads

            app.UseCors("DefaultCorsPolicy");
            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseMiddleware<SessionActivityMiddleware>();
            app.UseMiddleware<DistributedUserThrottleMiddleware>();
            app.UseAuthorization();

            app.MapControllers();

            // ── SignalR Hubs ─────────────────────────────────────────────────
            app.MapHub<NotificationHub>("/hubs/notifications");
            app.MapHub<ChatHub>("/hubs/chat");

            await AdminSeeder.SeedAsync(app.Services);
            app.Run();
        }
    }
}