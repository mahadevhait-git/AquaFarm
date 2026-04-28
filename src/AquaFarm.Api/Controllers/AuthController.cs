using System.Collections.Concurrent;
using AquaFarm.Api.Models;
using AquaFarm.Api.Security;
using AquaFarm.Core;
using AquaFarm.Core.Dtos;
using AquaFarm.Core.Entities;
using AquaFarm.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace AquaFarm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, (string Otp, DateTime ExpiresAt)> OtpStore = new();
    private readonly SimpleTokenService _tokenService;
    private readonly AquaFarmDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public AuthController(
        IOptions<JwtSettings> jwtOptions,
        AquaFarmDbContext dbContext,
        IConfiguration configuration,
        SimpleTokenService tokenService)
    {
        _ = jwtOptions.Value;
        _dbContext = dbContext;
        _configuration = configuration;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Address) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.PhoneNumber) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("All fields are required.");
        }

        if (!Enum.TryParse<UserRole>(request.Role, true, out var parsedRole))
        {
            return BadRequest("Invalid role.");
        }

        if (parsedRole == UserRole.Admin)
        {
            return BadRequest("Admin registration is not allowed.");
        }

        var normalizedPhone = NormalizePhone(request.PhoneNumber);
        if (normalizedPhone.Length < 10 || normalizedPhone.Length > 15)
        {
            return BadRequest("Phone number must contain 10 to 15 digits.");
        }

        if (!IsValidEmail(request.Email))
        {
            return BadRequest("Please enter a valid email address.");
        }

        var phoneExists = await _dbContext.Users.AnyAsync(u => u.PhoneNumber == normalizedPhone);
        if (phoneExists)
        {
            return BadRequest("Phone number is already registered.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var emailExists = await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail);
        if (emailExists)
        {
            return BadRequest("Email is already registered.");
        }

        var userName = await GenerateUserName(request.FirstName, request.LastName);
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Address = request.Address.Trim(),
            Email = normalizedEmail,
            PhoneNumber = normalizedPhone,
            PasswordHash = request.Password,
            Role = parsedRole,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var token = _tokenService.CreateToken(user.Id, user.UserName, user.Role.ToString());
        return Created(string.Empty, new AuthResponse(token, user.Role.ToString()));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Invalid credentials.");
        }

        var normalizedPhone = NormalizePhone(request.PhoneNumber);
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == normalizedPhone);
        if (user is null || user.PasswordHash != request.Password)
        {
            return BadRequest("Invalid credentials.");
        }

        if (!user.IsActive)
        {
            return BadRequest("Your account is inactive. Please contact admin.");
        }

        var role = user.Role.ToString();
        var token = _tokenService.CreateToken(user.Id, user.UserName, role);
        return Ok(new AuthResponse(token, role));
    }

    [HttpPost("forgot-password/request-otp")]
    public async Task<IActionResult> RequestForgotPasswordOtp([FromBody] ForgotPasswordOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest("Email is required.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (user is null)
        {
            return NotFound("User with this email was not found.");
        }

        var otp = Random.Shared.Next(100000, 999999).ToString();
        OtpStore[normalizedEmail] = (otp, DateTime.UtcNow.AddMinutes(10));
        var sent = await SendOtpEmail(normalizedEmail, otp);
        if (!sent)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, "OTP email could not be sent. Check SMTP configuration.");
        }

        return Ok(new { message = "OTP sent to your email." });
    }

    [HttpPost("forgot-password/reset")]
    public async Task<IActionResult> ResetForgotPassword([FromBody] ForgotPasswordResetRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Otp) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest("Email, OTP and new password are required.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (user is null)
        {
            return NotFound("User with this email was not found.");
        }

        if (!OtpStore.TryGetValue(normalizedEmail, out var otpEntry))
        {
            return BadRequest("OTP not requested or expired.");
        }

        if (otpEntry.ExpiresAt < DateTime.UtcNow || otpEntry.Otp != request.Otp.Trim())
        {
            return BadRequest("Invalid or expired OTP.");
        }

        user.PasswordHash = request.NewPassword;
        await _dbContext.SaveChangesAsync();
        OtpStore.TryRemove(normalizedEmail, out _);

        return Ok(new { message = "Password reset successfully." });
    }

    private static string NormalizePhone(string phoneNumber)
    {
        var chars = phoneNumber.Where(char.IsDigit).ToArray();
        return new string(chars);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var parsed = new MailAddress(email.Trim());
            return parsed.Address.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> GenerateUserName(string firstName, string lastName)
    {
        var baseName = $"{firstName.Trim()}.{lastName.Trim()}".ToLowerInvariant();
        baseName = new string(baseName.Where(c => char.IsLetterOrDigit(c) || c == '.').ToArray());
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "user";
        }

        var candidate = baseName;
        var suffix = 1;
        while (await _dbContext.Users.AnyAsync(u => u.UserName == candidate))
        {
            suffix++;
            candidate = $"{baseName}{suffix}";
        }

        return candidate;
    }

    private async Task<bool> SendOtpEmail(string email, string otp)
    {
        var host = _configuration["Smtp:Host"];
        var portValue = _configuration["Smtp:Port"];
        var username = _configuration["Smtp:Username"];
        var password = _configuration["Smtp:Password"];
        var from = _configuration["Smtp:From"];
        var enableSsl = _configuration["Smtp:EnableSsl"];

        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(portValue) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(from) ||
            !int.TryParse(portValue, out var port))
        {
            return false;
        }

        var ssl = string.Equals(enableSsl, "true", StringComparison.OrdinalIgnoreCase);

        using var message = new MailMessage(from, email)
        {
            Subject = "AquaFarm Password Reset OTP",
            Body = $"Your AquaFarm OTP is {otp}. It expires in 10 minutes."
        };

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = ssl,
            Credentials = new NetworkCredential(username, password)
        };

        await client.SendMailAsync(message);
        return true;
    }
}
