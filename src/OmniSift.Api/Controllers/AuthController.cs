using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniSift.Api.Data;
using OmniSift.Api.Models;
using OmniSift.Api.Services;
using OmniSift.Shared.DTOs;

namespace OmniSift.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(
    OmniSiftDbContext dbContext,
    IPasswordHasher passwordHasher,
    JwtTokenService jwtTokenService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var slug = Slugify(request.TenantName) + "-" + Guid.NewGuid().ToString("N")[..8];

        var tenant = new Tenant
        {
            Name = request.TenantName,
            Slug = slug,
            ApiKeyHash = Guid.NewGuid().ToString("N")
        };

        dbContext.Tenants.Add(tenant);

        var user = new AppUser
        {
            TenantId = tenant.Id,
            Email = request.Email,
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = "owner"
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var (token, expiresAt) = jwtTokenService.CreateToken(user);

        return Ok(new AuthResponse
        {
            Token = token,
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = user.Role,
            ExpiresAt = expiresAt
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.TenantId == request.TenantId && u.Email == request.Email, cancellationToken)
            .ConfigureAwait(false);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid credentials." });

        var (token, expiresAt) = jwtTokenService.CreateToken(user);

        return Ok(new AuthResponse
        {
            Token = token,
            UserId = user.Id,
            TenantId = user.TenantId,
            Role = user.Role,
            ExpiresAt = expiresAt
        });
    }

    private static string Slugify(string name) =>
        Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
}
