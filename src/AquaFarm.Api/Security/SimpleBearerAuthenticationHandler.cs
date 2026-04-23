using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AquaFarm.Api.Security;

public sealed class SimpleBearerAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly SimpleTokenService _tokenService;

    public SimpleBearerAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        SimpleTokenService tokenService)
        : base(options, logger, encoder)
    {
        _tokenService = tokenService;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = authorization["Bearer ".Length..].Trim();
        if (!_tokenService.TryValidateToken(token, out ClaimsPrincipal? principal) || principal is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid or expired token."));
        }

        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
