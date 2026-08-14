using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using DataGateMonitor.Services.Api.Auth.ForgotPassword;
using DataGateMonitor.Services.Api.Auth.Login;
using DataGateMonitor.Services.Api.Auth.EmailConfirmation;
using DataGateMonitor.Services.Api.Auth.TelegramLogin;
using DataGateMonitor.Services.Api.Auth.Totp;
using DataGateMonitor.Services.Api.Auth.TvLogin;
using DataGateMonitor.Services.Api.CurrentUser.Interfaces;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;
using DataGateMonitor.Services.Users.Interfaces;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Responses;
using DataGateMonitor.SharedModels.DataGateMonitor.User.Responses;
using DataGateMonitor.SharedModels.Responses;

namespace DataGateMonitor.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthController(
    IConfiguration config,
    IApplicationService appService,
    IMicroserviceTokenService microserviceTokenService,
    IUserRegistrationService userRegistrationService,
    IUserLoginService userLoginService,
    IUserQueryService userQueryService,
    IEmailConfirmationService emailConfirmationService,
    ITelegramAccountLinkService telegramAccountLinkService,
    IFreeTierAccessComplianceService freeTierAccessComplianceService,
    IGoogleAuthCodeExchangeService exchange,
    ITokenService tokenService,
    IAdminForgotPasswordService adminForgotPasswordService,
    ITelegramLoginCodeService telegramLoginCodeService,
    ITvLoginSessionService tvLoginSessionService,
    IAdminTotpService adminTotpService,
    ICurrentUserService currentUserService,
    IAdminIdleSessionTracker adminIdleSessionTracker,
    IAdminIdleTimeoutProvider adminIdleTimeoutProvider,
    IUserSessionService userSessionService) : BaseController
{
    [AllowAnonymous]
    [HttpGet("session-policy")]
    [ProducesResponseType(typeof(ApiResponse<AuthSessionPolicyResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AuthSessionPolicyResponse>>> GetSessionPolicy(
        CancellationToken cancellationToken)
    {
        var minutes = await adminIdleTimeoutProvider.GetMinutesAsync(cancellationToken);

        return Ok(ApiResponse<AuthSessionPolicyResponse>.SuccessResponse(new AuthSessionPolicyResponse
        {
            AdminIdleTimeoutMinutes = minutes,
        }));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("activity")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult RecordAdminActivity()
    {
        adminIdleSessionTracker.Touch(currentUserService.UserId);
        return NoContent();
    }

    [HttpPost("token")]
    public async Task<ActionResult<ApiResponse<TokenResponse>>> GenerateToken([FromBody] TokenRequest request,
        CancellationToken cancellationToken)
    {
        var app = await appService.GetApplicationByClientIdAsync(request.ClientId, cancellationToken);
        if (app == null)
        {
            return Unauthorized(ApiResponse<TokenResponse>.ErrorResponse("Invalid credentials"));
        }

        var isValid = app.IsSystem
            ? BCrypt.Net.BCrypt.Verify(request.ClientSecret, app.ClientSecret)
            : app.ClientSecret == request.ClientSecret;

        if (!isValid)
        {
            return Unauthorized(ApiResponse<TokenResponse>.ErrorResponse("Invalid credentials"));
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(config["Jwt:Secret"]
                                          ?? throw new InvalidOperationException("Jwt:Secret"));

        var now = DateTimeOffset.UtcNow;
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.Name, request.ClientId),
                new Claim(ClaimTypes.Role, "App")
            ]),
            Issuer = "OpenVPNGateBackend",
            Audience = "OpenVPNGateFrontend",
            NotBefore = now.UtcDateTime,
            IssuedAt = now.UtcDateTime,
            Expires = now.AddHours(1).UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return Ok(ApiResponse<TokenResponse>.SuccessResponse(
            new TokenResponse
            {
                Token = tokenHandler.WriteToken(token),
                Expiration = tokenDescriptor.Expires ?? DateTimeOffset.UtcNow
            }));
    }

    [HttpGet("public-key/{pin:int}")]
    public ActionResult<ApiResponse<string>> GetPublicKeyForMicroservice([FromRoute(Name = "pin")] int pin)
    {
        if (pin > 10000)
        {
            var key = microserviceTokenService.GetPublicKeyPem();
            return Ok(ApiResponse<string>.SuccessResponse(key));
        }

        return BadRequest(ApiResponse<string>.ErrorResponse("Invalid pin"));
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<RegisterUserResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RegisterUserResponse>>> Register(
        [FromBody] RegisterUserRequest request,
        CancellationToken ct)
    {
        var result = await userRegistrationService.RegisterAsync(request, ct);

        return Ok(ApiResponse<RegisterUserResponse>.SuccessResponse(result));
    }

    /// <summary>
    /// Client app (Google/local login): request a one-time code. Omit <see cref="RequestTelegramAccountLinkCodeRequest.TelegramId"/>
    /// — user enters the code in the Telegram bot (recommended on mobile). Optional TelegramId binds the code to one account (legacy).
    /// </summary>
    [Authorize]
    [HttpPost("telegram/request-account-link-code")]
    [ProducesResponseType(typeof(ApiResponse<RequestTelegramAccountLinkCodeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<RequestTelegramAccountLinkCodeResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<RequestTelegramAccountLinkCodeResponse>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RequestTelegramAccountLinkCodeResponse>>> RequestTelegramAccountLinkCode(
        [FromBody] RequestTelegramAccountLinkCodeRequest? request,
        CancellationToken ct)
    {
        try
        {
            var result = await telegramAccountLinkService.RequestLinkCodeAsync(
                currentUserService.UserId,
                request?.TelegramId,
                ct);
            return Ok(ApiResponse<RequestTelegramAccountLinkCodeResponse>.SuccessResponse(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<RequestTelegramAccountLinkCodeResponse>.ErrorResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<RequestTelegramAccountLinkCodeResponse>.ErrorResponse(ex.Message));
        }
    }

    /// <summary>Telegram bot: issue a link code for the sender. User enters it in the app (recommended Android flow).</summary>
    [Authorize(Roles = "Admin,App")]
    [HttpPost("telegram/request-account-link-code-for-bot")]
    [ProducesResponseType(typeof(ApiResponse<RequestTelegramAccountLinkCodeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<RequestTelegramAccountLinkCodeResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<RequestTelegramAccountLinkCodeResponse>>> RequestTelegramAccountLinkCodeForBot(
        [FromBody] RequestTelegramAccountLinkCodeForBotRequest request,
        CancellationToken ct)
    {
        if (request is null || request.TelegramId <= 0)
            return BadRequest(ApiResponse<RequestTelegramAccountLinkCodeResponse>.ErrorResponse("TelegramId is required."));

        try
        {
            var result = await telegramAccountLinkService.RequestLinkCodeFromBotAsync(request.TelegramId, ct);
            return Ok(ApiResponse<RequestTelegramAccountLinkCodeResponse>.SuccessResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<RequestTelegramAccountLinkCodeResponse>.ErrorResponse(ex.Message));
        }
    }

    /// <summary>Client app: complete linking with a code received from the Telegram bot.</summary>
    [Authorize]
    [HttpPost("telegram/complete-account-link")]
    [ProducesResponseType(typeof(ApiResponse<CompleteTelegramAccountLinkResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CompleteTelegramAccountLinkResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<CompleteTelegramAccountLinkResponse>>> CompleteTelegramAccountLinkFromApp(
        [FromBody] CompleteTelegramAccountLinkFromAppRequest request,
        CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(ApiResponse<CompleteTelegramAccountLinkResponse>.ErrorResponse("Code is required."));

        var result = await telegramAccountLinkService.CompleteLinkFromAppAsync(
            currentUserService.UserId,
            request.Code,
            ct);

        if (!result.Success)
            return BadRequest(ApiResponse<CompleteTelegramAccountLinkResponse>.ErrorResponse(result.Message));

        return Ok(ApiResponse<CompleteTelegramAccountLinkResponse>.SuccessResponse(result));
    }

    /// <summary>
    /// Client apps: read-only Free/Default access status (requires Telegram channel subscription).
    /// Does not notify admins or start a grace period.
    /// </summary>
    [Authorize]
    [HttpGet("free-tier-access/status")]
    [ProducesResponseType(typeof(ApiResponse<FreeTierAccessStatusResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<FreeTierAccessStatusResponse>>> GetFreeTierAccessStatus(
        CancellationToken ct)
    {
        var status = await freeTierAccessComplianceService.GetStatusAsync(currentUserService.UserId, ct);
        return Ok(ApiResponse<FreeTierAccessStatusResponse>.SuccessResponse(status));
    }

    /// <summary>
    /// Client apps: call right after establishing a VPN connection. Starts/refreshes the Free/Default
    /// grace window (if applicable) and returns the resulting status, so the client can show a
    /// "connected for N minutes" countdown via <see cref="FreeTierAccessStatusResponse.GraceExpiresAtUtc"/>.
    /// </summary>
    [Authorize]
    [HttpPost("free-tier-access/connect")]
    [ProducesResponseType(typeof(ApiResponse<FreeTierAccessStatusResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<FreeTierAccessStatusResponse>>> RegisterFreeTierAccessConnect(
        CancellationToken ct)
    {
        var status = await freeTierAccessComplianceService.RegisterConnectionAsync(
            currentUserService.UserId, "android-connect", ct);
        return Ok(ApiResponse<FreeTierAccessStatusResponse>.SuccessResponse(status));
    }

    [AllowAnonymous]
    [HttpPost("email/request-confirmation")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<string>>> RequestEmailConfirmation(
        [FromBody] RequestEmailConfirmationRequest request,
        CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(ApiResponse<string>.ErrorResponse("Email is required."));

        // Prevent account enumeration: always return success message.
        try
        {
            var user = await userQueryService.GetByEmail(request.Email.Trim(), ct);
            if (user is { IsEmailConfirmed: false } && !string.IsNullOrWhiteSpace(user.Email))
                await emailConfirmationService.SendConfirmationAsync(user.Id, user.Email, ct);
        }
        catch
        {
            // Intentionally ignored to avoid leaking account existence.
        }

        return Ok(ApiResponse<string>.SuccessResponse("If this email exists, confirmation code has been sent."));
    }

    [AllowAnonymous]
    [HttpPost("email/confirm")]
    [ProducesResponseType(typeof(ApiResponse<ConfirmEmailResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ConfirmEmailResponse>>> ConfirmEmail(
        [FromBody] ConfirmEmailRequest request,
        CancellationToken ct)
    {
        var result = await emailConfirmationService.ConfirmAsync(request.Email, request.Code, ct);
        return Ok(ApiResponse<ConfirmEmailResponse>.SuccessResponse(result));
    }

    [AllowAnonymous]
    [HttpPost("google-login")]
    [ProducesResponseType(typeof(ApiResponse<GoogleLoginResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GoogleLoginResponse>>> GoogleLogin(
        [FromBody] GoogleLoginRequest request,
        CancellationToken ct)
    {
        var result = await userLoginService.LoginWithGoogleAsync(request.IdToken, ct);
        return Ok(ApiResponse<GoogleLoginResponse>.SuccessResponse(result));
    }
    
    [AllowAnonymous]
    [HttpPost("google-code-login")]
    [ProducesResponseType(typeof(ApiResponse<GoogleLoginResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GoogleLoginResponse>>> GoogleCodeLogin(
        [FromBody] GoogleCodeLoginRequest request,
        CancellationToken ct)
    {
        if (request == null)
            return BadRequest(ApiResponse<GoogleLoginResponse>.ErrorResponse("Request body is required."));

        var idToken = await exchange.ExchangeCodeForIdTokenAsync(
            request.Code,
            request.CodeVerifier,
            request.RedirectUri,
            ct);

        var result = await userLoginService.LoginWithGoogleAsync(idToken, ct);

        return Ok(ApiResponse<GoogleLoginResponse>.SuccessResponse(result));
    }

    /// <summary>
    /// For the Telegram bot: request a one-time login code for a user. Bot shows this code to the user to enter on the dashboard.
    /// Requires App or Admin token.
    /// </summary>
    [Authorize(Roles = "Admin,App")]
    [HttpPost("telegram/request-login-code")]
    [ProducesResponseType(typeof(ApiResponse<TelegramRequestLoginCodeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TelegramRequestLoginCodeResponse>>> TelegramRequestLoginCode(
        [FromBody] TelegramRequestLoginCodeRequest request,
        CancellationToken ct)
    {
        if (request == null)
            return BadRequest(ApiResponse<TelegramRequestLoginCodeResponse>.ErrorResponse("Request body is required."));

        var result = await telegramLoginCodeService.RequestLoginCodeAsync(request, ct);
        if (result == null)
            return NotFound(ApiResponse<TelegramRequestLoginCodeResponse>.ErrorResponse("User not found or blocked."));

        return Ok(ApiResponse<TelegramRequestLoginCodeResponse>.SuccessResponse(result));
    }

    /// <summary>
    /// Log in on the dashboard using the code received from the Telegram bot.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("telegram-code-login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> TelegramCodeLogin(
        [FromBody] TelegramCodeLoginRequest request,
        CancellationToken ct)
    {
        var result = await telegramLoginCodeService.LoginWithCodeAsync(request, ct);
        return Ok(ApiResponse<LoginResponse>.SuccessResponse(result));
    }

    /// <summary>
    /// TV / device linking: create a short-lived session (QR payload + 6-digit user code). Public.
    /// Prefer SignalR <c>/api/hubs/tv-login</c> (<c>WatchSession</c>); fall back to polling GET tv/session/{id}.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("tv/session")]
    [ProducesResponseType(typeof(ApiResponse<CreateTvLoginSessionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CreateTvLoginSessionResponse>>> CreateTvLoginSession(
        [FromBody] CreateTvLoginSessionRequest? request,
        CancellationToken ct)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await tvLoginSessionService.CreateSessionAsync(
            request ?? new CreateTvLoginSessionRequest(),
            clientIp,
            ct);
        return Ok(ApiResponse<CreateTvLoginSessionResponse>.SuccessResponse(result));
    }

    /// <summary>
    /// TV status check / poll fallback. Same DB status as SignalR.
    /// Status: pending → viewed (phone opened code) → approved (tokens once) | denied | expired | consumed.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("tv/session/{sessionId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TvLoginSessionPollResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TvLoginSessionPollResponse>>> PollTvLoginSession(
        [FromRoute] Guid sessionId,
        CancellationToken ct)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await tvLoginSessionService.PollSessionAsync(sessionId, clientIp, ct);
        return Ok(ApiResponse<TvLoginSessionPollResponse>.SuccessResponse(result));
    }

    /// <summary>
    /// Authenticated phone/web preview of a pending TV session by user code.
    /// </summary>
    [Authorize]
    [HttpGet("tv/session/by-code/{userCode}")]
    [ProducesResponseType(typeof(ApiResponse<TvLoginSessionPreviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<ActionResult<ApiResponse<TvLoginSessionPreviewResponse>>> GetTvLoginSessionByCode(
        [FromRoute] string userCode,
        CancellationToken ct)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        try
        {
            var result = await tvLoginSessionService.GetByUserCodeAsync(
                userCode,
                currentUserService.UserId,
                clientIp,
                ct);
            return Ok(ApiResponse<TvLoginSessionPreviewResponse>.SuccessResponse(result));
        }
        catch (InvalidOperationException ex) when (
            ex.Message == TvLoginSessionService.RateLimitMessage)
        {
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                ApiResponse<TvLoginSessionPreviewResponse>.ErrorResponse(ex.Message));
        }
        catch (InvalidOperationException ex) when (
            ex.Message is TvLoginSessionService.SessionExpiredMessage
                or TvLoginSessionService.SessionDeniedMessage
                or TvLoginSessionService.SessionAlreadyCompletedMessage)
        {
            return StatusCode(
                StatusCodes.Status410Gone,
                ApiResponse<TvLoginSessionPreviewResponse>.ErrorResponse(ex.Message));
        }
        catch (InvalidOperationException ex) when (
            ex.Message == TvLoginSessionService.SessionNotFoundMessage)
        {
            return NotFound(ApiResponse<TvLoginSessionPreviewResponse>.ErrorResponse(ex.Message));
        }
    }

    /// <summary>
    /// Authenticated phone/web approves a pending TV session. TOTP must already be satisfied on this Bearer.
    /// </summary>
    [Authorize]
    [HttpPost("tv/session/approve")]
    [ProducesResponseType(typeof(ApiResponse<TvLoginSessionActionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TvLoginSessionActionResponse>>> ApproveTvLoginSession(
        [FromBody] ApproveTvLoginSessionRequest request,
        CancellationToken ct)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await tvLoginSessionService.ApproveAsync(
            request,
            currentUserService.UserId,
            clientIp,
            ct);
        return Ok(ApiResponse<TvLoginSessionActionResponse>.SuccessResponse(result));
    }

    /// <summary>
    /// Authenticated phone/web denies a pending TV session.
    /// </summary>
    [Authorize]
    [HttpPost("tv/session/deny")]
    [ProducesResponseType(typeof(ApiResponse<TvLoginSessionActionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TvLoginSessionActionResponse>>> DenyTvLoginSession(
        [FromBody] DenyTvLoginSessionRequest request,
        CancellationToken ct)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await tvLoginSessionService.DenyAsync(
            request,
            currentUserService.UserId,
            clientIp,
            ct);
        return Ok(ApiResponse<TvLoginSessionActionResponse>.SuccessResponse(result));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var result = await userLoginService.LoginAsync(request, ct);
        return Ok(ApiResponse<LoginResponse>.SuccessResponse(result));
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ApiResponse<AdminForgotPasswordResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AdminForgotPasswordResponse>>> ForgotPassword(
        [FromBody] AdminForgotPasswordRequest request,
        CancellationToken ct)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await adminForgotPasswordService.RequestResetCodeAsync(request, clientIp, ct);
        return Ok(ApiResponse<AdminForgotPasswordResponse>.SuccessResponse(result));
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ApiResponse<AdminResetPasswordResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AdminResetPasswordResponse>>> ResetPassword(
        [FromBody] AdminResetPasswordRequest request,
        CancellationToken ct)
    {
        var result = await adminForgotPasswordService.ResetPasswordAsync(request, ct);
        return Ok(ApiResponse<AdminResetPasswordResponse>.SuccessResponse(result));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(ApiResponse<GetUserSessionsResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GetUserSessionsResponse>>> GetSessions(CancellationToken ct)
    {
        var refreshToken = Request.Headers["X-Refresh-Token"].ToString();
        if (string.IsNullOrWhiteSpace(refreshToken))
            refreshToken = null;

        var sessions = await userSessionService.GetActiveSessionsAsync(
            currentUserService.UserId,
            refreshToken,
            ct);

        return Ok(ApiResponse<GetUserSessionsResponse>.SuccessResponse(sessions));
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("sessions/{sessionId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeSession([FromRoute] int sessionId, CancellationToken ct)
    {
        await userSessionService.RevokeSessionAsync(currentUserService.UserId, sessionId, ct);
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("sessions/revoke-all")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<int>>> RevokeAllSessions(
        [FromBody] RevokeUserSessionsRequest? request,
        CancellationToken ct)
    {
        var count = await userSessionService.RevokeSessionsAsync(
            currentUserService.UserId,
            keepRefreshToken: null,
            ct);

        return Ok(ApiResponse<int>.SuccessResponse(count));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("sessions/revoke-others")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<int>>> RevokeOtherSessions(
        [FromBody] RevokeUserSessionsRequest request,
        CancellationToken ct)
    {
        var count = await userSessionService.RevokeSessionsAsync(
            currentUserService.UserId,
            request.KeepRefreshToken,
            ct);

        return Ok(ApiResponse<int>.SuccessResponse(count));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest? request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request?.RefreshToken))
            await userSessionService.RevokeByRefreshTokenAsync(request.RefreshToken, ct);

        await HttpContext.SignOutAsync("UserCookie");
        return Ok();
    }

    [AllowAnonymous]
    [HttpPost("totp/verify-login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> VerifyTotpLogin(
        [FromBody] TotpVerifyLoginRequest request,
        CancellationToken ct)
    {
        var result = await adminTotpService.VerifyLoginChallengeAsync(request, ct);
        return Ok(ApiResponse<LoginResponse>.SuccessResponse(result));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("totp/status")]
    [ProducesResponseType(typeof(ApiResponse<TotpStatusResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TotpStatusResponse>>> GetTotpStatus(CancellationToken ct)
    {
        var status = await adminTotpService.GetStatusAsync(currentUserService.UserId, ct);
        return Ok(ApiResponse<TotpStatusResponse>.SuccessResponse(status));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("totp/setup")]
    [ProducesResponseType(typeof(ApiResponse<TotpSetupResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TotpSetupResponse>>> BeginTotpSetup(CancellationToken ct)
    {
        var setup = await adminTotpService.BeginSetupAsync(currentUserService.UserId, ct);
        return Ok(ApiResponse<TotpSetupResponse>.SuccessResponse(setup));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("totp/confirm")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<string>>> ConfirmTotpSetup(
        [FromBody] TotpConfirmRequest request,
        CancellationToken ct)
    {
        await adminTotpService.ConfirmSetupAsync(currentUserService.UserId, request, ct);
        return Ok(ApiResponse<string>.SuccessResponse("Two-factor authentication enabled."));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("totp/disable")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<string>>> DisableTotp(
        [FromBody] TotpDisableRequest request,
        CancellationToken ct)
    {
        await adminTotpService.DisableAsync(currentUserService.UserId, request, ct);
        return Ok(ApiResponse<string>.SuccessResponse("Two-factor authentication disabled."));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<RefreshResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RefreshResponse>>> Refresh([FromBody] RefreshRequest request,
        CancellationToken ct)
    {
        var tokens = await tokenService.RefreshAsync(
            request.RefreshToken,
            request.DeviceId,
            request.UserAgent,
            ct);

        var response = new RefreshResponse
        {
            Token = tokens.AccessToken,
            Expiration = tokens.AccessExpiresAt,
            RefreshToken = tokens.RefreshToken,
            RefreshExpiration = tokens.RefreshExpiresAt
        };

        return Ok(ApiResponse<RefreshResponse>.SuccessResponse(response));
    }
}