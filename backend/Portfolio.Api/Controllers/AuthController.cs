using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Api.Dtos;
using Portfolio.Api.Entities;
using Portfolio.Api.Services;

namespace Portfolio.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AdminAccountService accounts) : ControllerBase
{
    public const string StampClaim = "security_stamp";

    public record SessionInfo(string Email);

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<SessionInfo>> Login(LoginRequest request)
    {
        var (result, user) = await accounts.ValidateAsync(request.Email, request.Password, HttpContext.RequestAborted);
        return result switch
        {
            LoginResult.Success => await SignIn(user!),
            LoginResult.LockedOut => Problem(statusCode: 423, title: "Too many failed attempts. Try again in 15 minutes."),
            _ => Problem(statusCode: 401, title: "Invalid email or password."),
        };
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<SessionInfo> Me() => new SessionInfo(User.FindFirstValue(ClaimTypes.Email)!);

    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (!await accounts.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, HttpContext.RequestAborted))
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["currentPassword"] = ["Current password is incorrect."],
            }));

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    private async Task<ActionResult<SessionInfo>> SignIn(AdminUser user)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(StampClaim, user.SecurityStamp),
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return new SessionInfo(user.Email);
    }
}

public static class RateLimitPolicies
{
    public const string Login = "login";
    public const string Contact = "contact";
}
