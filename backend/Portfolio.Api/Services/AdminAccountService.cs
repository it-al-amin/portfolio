using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Portfolio.Api.Data;
using Portfolio.Api.Entities;

namespace Portfolio.Api.Services;

public enum LoginResult { Success, Invalid, LockedOut }

/// <summary>
/// Single-admin account management. Passwords are hashed with ASP.NET Core Identity's PasswordHasher
/// (PBKDF2-HMAC-SHA512 with per-password salt). There is no default account: the first admin is created
/// from ADMIN_EMAIL / ADMIN_PASSWORD configuration only when no admin exists yet.
/// </summary>
public class AdminAccountService(PortfolioDbContext db, ILogger<AdminAccountService> logger)
{
    public const int MinPasswordLength = 8;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private readonly PasswordHasher<AdminUser> _hasher = new();

    public async Task EnsureInitialAdminAsync(IConfiguration config)
    {
        if (await db.AdminUsers.AnyAsync()) return;

        var email = config["ADMIN_EMAIL"] ?? config["Admin:Email"];
        var password = config["ADMIN_PASSWORD"] ?? config["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            logger.LogWarning("No admin account exists. Set ADMIN_EMAIL and ADMIN_PASSWORD (user secrets or environment) and restart to create one.");
            return;
        }
        if (password.Length < MinPasswordLength)
        {
            logger.LogError("ADMIN_PASSWORD must be at least {Min} characters. Admin account was not created.", MinPasswordLength);
            return;
        }

        var user = new AdminUser { Email = email.Trim().ToLowerInvariant() };
        user.PasswordHash = _hasher.HashPassword(user, password);
        db.AdminUsers.Add(user);
        await db.SaveChangesAsync();
        logger.LogInformation("Initial admin account created. You can now remove ADMIN_PASSWORD from configuration.");
    }

    public async Task<(LoginResult Result, AdminUser? User)> ValidateAsync(string email, string password, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Email == normalized, ct);
        if (user is null)
        {
            // Hash anyway so response timing does not reveal whether the account exists.
            _hasher.HashPassword(new AdminUser(), password);
            return (LoginResult.Invalid, null);
        }

        if (user.LockoutEndUtc > DateTime.UtcNow) return (LoginResult.LockedOut, null);

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockoutEndUtc = DateTime.UtcNow.Add(LockoutDuration);
                user.FailedLoginCount = 0;
                logger.LogWarning("Admin account locked for {Minutes} minutes after repeated failed logins.", LockoutDuration.TotalMinutes);
            }
            await db.SaveChangesAsync(ct);
            return (LoginResult.Invalid, null);
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded) user.PasswordHash = _hasher.HashPassword(user, password);
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (LoginResult.Success, user);
    }

    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await db.AdminUsers.FindAsync([userId], ct);
        if (user is null || _hasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
            return false;

        user.PasswordHash = _hasher.HashPassword(user, newPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync(ct);
        return true;
    }
}
