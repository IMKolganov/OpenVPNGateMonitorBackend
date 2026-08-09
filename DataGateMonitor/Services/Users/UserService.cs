using Mapster;
using DataGateMonitor.DataBase.Services.Command;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.TelegramBotUserTable;
using DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Others.Notifications;
using DataGateMonitor.Services.Users.Interfaces;
using DataGateMonitor.SharedModels.DataGateMonitor.User.Requests;
using DataGateMonitor.SharedModels.DataGateMonitor.User.Responses;
using DataGateMonitor.SharedModels.DataGateMonitor.User.Responses.Dto;

namespace DataGateMonitor.Services.Users;

public class UserService(
    IUserQueryService userQueryService,
    ITelegramBotUserQueryService telegramBotUserQueryService,
    IUserIdentityLinkQueryService userIdentityLinkQueryService,
    IQuotaPlanQueryService quotaPlanQueryService,
    ICommandService<User, int> userCommandService,
    ICommandService<UserIdentityLink, int> userIdentityLinkCommandService,
    ICommandService<UserQuotaPlan, int> userQuotaPlanCommandService,
    ICommandService<TelegramBotUser, int> telegramBotUserCommandService,
    IAppNotificationFacade appNotificationFacade,
    ILogger<UserService> logger
) : IUserService
{
    private const string TelegramProvider = "telegram";

    public async Task<User?> GetOrCreateDashboardUserForTelegramAsync(long telegramId,
        CancellationToken cancellationToken)
    {
        var telegramBotUser = await telegramBotUserQueryService.GetByTelegramId(telegramId, cancellationToken);
        if (telegramBotUser == null)
            return null;

        var link = await userIdentityLinkQueryService.GetByProviderAndExternalId(
            TelegramProvider, telegramId.ToString(), cancellationToken);

        if (link != null)
        {
            var user = await userQueryService.GetById(link.UserId, cancellationToken);
            return user;
        }

        var now = DateTimeOffset.UtcNow;
        var displayName = !string.IsNullOrWhiteSpace(telegramBotUser.Username)
            ? telegramBotUser.Username
            : !string.IsNullOrWhiteSpace(telegramBotUser.FirstName)
                ? $"{telegramBotUser.FirstName} {telegramBotUser.LastName}".Trim()
                : $"tg_{telegramId}";

        var userNew = new User
        {
            DisplayName = displayName ?? $"tg_{telegramId}",
            Email = null,
            IsAdmin = false,
            IsBlocked = false,
            HasDashboardAccess = false,
            CreateDate = now,
            LastUpdate = now
        };

        userNew = await userCommandService.Add(userNew, saveChanges: true, cancellationToken);
        logger.LogInformation("Dashboard user created for Telegram {TelegramId} (login by code)", telegramId);

        var identityLink = new UserIdentityLink
        {
            UserId = userNew.Id,
            Provider = TelegramProvider,
            ExternalId = telegramId.ToString(),
            ProviderRowId = telegramBotUser.Id,
            CreateDate = now,
            LastUpdate = now
        };

        await userIdentityLinkCommandService.Add(identityLink, saveChanges: true, cancellationToken);

        var defaultPlan = await quotaPlanQueryService.GetDefault(cancellationToken);
        if (defaultPlan != null)
        {
            await userQuotaPlanCommandService.Add(new UserQuotaPlan
            {
                UserId = userNew.Id,
                QuotaPlanId = defaultPlan.Id,
                EffectiveFrom = now,
                EffectiveTo = null,
                Note = "Auto-assigned on Telegram login by code",
                CreateDate = now,
                LastUpdate = now
            }, saveChanges: true, cancellationToken);
        }

        await TryNotifyAdminsNewUserAsync(userNew, login: null, "Telegram login code", cancellationToken);

        return userNew;
    }

    public async Task<UsersResponse> RegisterUserFromTgBot(
        RegisterUserFromTgBotRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        // 1) Ensure TelegramBotUser exists (read via query, mutate via command)
        var telegramBotUser = await telegramBotUserQueryService
            .GetByTelegramId(request.TelegramId, cancellationToken);

        if (telegramBotUser == null)
        {
            telegramBotUser = request.Adapt<TelegramBotUser>();
            telegramBotUser.CreateDate = now;
            telegramBotUser.LastUpdate = now;
            telegramBotUser.IsBlocked = false;
            telegramBotUser.IsAdmin = false;

            telegramBotUser = await telegramBotUserCommandService.Add(telegramBotUser, saveChanges: true,
                cancellationToken);
            logger.LogInformation("Telegram user {TelegramId} created", request.TelegramId);
        }
        else
        {
            request.Adapt(telegramBotUser);
            telegramBotUser.LastUpdate = now;

            await telegramBotUserCommandService.Update(telegramBotUser, saveChanges: true, cancellationToken);
            logger.LogInformation("Telegram user {TelegramId} updated", request.TelegramId);
        }

        // 2) Resolve dashboard User via identity link (read via query)
        var link = await userIdentityLinkQueryService.GetByProviderAndExternalId(
            "telegram", request.TelegramId.ToString(), cancellationToken);

        User user;

        if (link == null)
        {
            // Create dashboard User
            var displayName =
                !string.IsNullOrWhiteSpace(request.Username)
                    ? request.Username!
                    : !string.IsNullOrWhiteSpace(request.FirstName)
                        ? $"{request.FirstName} {request.LastName}".Trim()
                        : $"tg_{request.TelegramId}";

            user = new User
            {
                DisplayName = displayName,
                Email = null,
                IsAdmin = false,
                IsBlocked = false,
                HasDashboardAccess = false,
                CreateDate = now,
                LastUpdate = now
            };

            user = await userCommandService.Add(user, saveChanges: true, cancellationToken);
            logger.LogInformation("Dashboard user created for Telegram {TelegramId}", request.TelegramId);

            // Create identity link (mutate via command)
            var identityLink = new UserIdentityLink
            {
                UserId = user.Id,
                Provider = "telegram",
                ExternalId = request.TelegramId.ToString(),
                ProviderRowId = telegramBotUser.Id,
                CreateDate = now,
                LastUpdate = now
            };

            await userIdentityLinkCommandService.Add(identityLink, saveChanges: true, cancellationToken);
            logger.LogInformation("UserIdentityLink created for user {UserId}", user.Id);

            // Assign default quota (read default plan via query, mutate via command)
            var defaultPlan = await quotaPlanQueryService.GetDefault(cancellationToken);
            if (defaultPlan != null)
            {
                var quotaPlan = new UserQuotaPlan
                {
                    UserId = user.Id,
                    QuotaPlanId = defaultPlan.Id,
                    EffectiveFrom = now,
                    EffectiveTo = null,
                    Note = "Auto-assigned default plan on Telegram registration",
                    CreateDate = now,
                    LastUpdate = now
                };

                await userQuotaPlanCommandService.Add(quotaPlan, saveChanges: true, cancellationToken);
                logger.LogInformation("Default quota plan {PlanId} assigned to user {UserId}", defaultPlan.Id, user.Id);
            }

            await TryNotifyAdminsNewUserAsync(user, login: null, "Telegram bot", cancellationToken);
        }
        else
        {
            // Load user via query by link.UserId
            user = await userQueryService.GetById(link.UserId, cancellationToken)
                   ?? throw new InvalidOperationException($"Linked user not found: {link.UserId}");
        }

        // 3) Build response
        var dto = await BuildUserDtoAsync(user, cancellationToken);
        // override with telegram context for this specific flow
        dto.Provider = "telegram";
        dto.ExternalId = request.TelegramId.ToString();
        dto.ProviderRowId = telegramBotUser.Id;

        return new UsersResponse { User = dto };
    }

    public async Task<GetAllUsersResponse> GetUsersPage(GetAllUsersRequest request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : request.PageSize;
        if (pageSize > 500)
            pageSize = 500;

        request.Page = page;
        request.PageSize = pageSize;

        var paged = await userQueryService.GetPage(request, cancellationToken);

        var userIds = paged.Items.Select(u => u.Id).ToList();
        var linksByUserId = await userIdentityLinkQueryService.GetFirstByUserIds(userIds, cancellationToken);

        var dtos = new List<UserDto>(paged.Items.Count);
        foreach (var u in paged.Items)
        {
            linksByUserId.TryGetValue(u.Id, out var link);
            dtos.Add(BuildUserDto(u, link));
        }

        return new GetAllUsersResponse
        {
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            Users = dtos
        };
    }

    public async Task<UsersResponse> GetUserById(GetUserByIdRequest request, CancellationToken cancellationToken)
    {
        // Load user by id or fail fast
        var user = await userQueryService.GetById(request.Id, cancellationToken)
                   ?? throw new KeyNotFoundException($"User {request.Id} not found");

        var dto = await BuildUserDtoAsync(user, cancellationToken);
        return new UsersResponse { User = dto };
    }

    public async Task<UsersResponse> GetUserByExternalId(GetUserByExternalIdRequest request,
        CancellationToken cancellationToken)
    {
        // Resolve link first (provider + external id)
        var link = await userIdentityLinkQueryService
            .GetByExternalId(request.ExternalId, cancellationToken);

        if (link == null)
            throw new KeyNotFoundException(
                $"Identity link not found for externalId '{request.ExternalId}'");

        // Load user by link
        var user = await userQueryService.GetById(link.UserId, cancellationToken)
                   ?? throw new InvalidOperationException($"Linked user not found: {link.UserId}");

        var dto = await BuildUserDtoAsync(user, cancellationToken);
        return new UsersResponse { User = dto };
    }

    public async Task<GetUserEmailConfirmationStatusResponse> GetEmailConfirmationStatus(
        GetUserEmailConfirmationStatusRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userQueryService.GetById(request.Id, cancellationToken)
                   ?? throw new KeyNotFoundException($"User {request.Id} not found");
        return new GetUserEmailConfirmationStatusResponse { IsEmailConfirmed = user.IsEmailConfirmed };
    }

    public async Task<ConfirmUserEmailResponse> ConfirmEmailManually(
        ConfirmUserEmailRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userQueryService.GetById(request.Id, cancellationToken)
                   ?? throw new KeyNotFoundException($"User {request.Id} not found");

        if (string.IsNullOrWhiteSpace(user.Email))
            throw new InvalidOperationException("User has no email.");

        if (user.IsEmailConfirmed)
            return new ConfirmUserEmailResponse { IsEmailConfirmed = true };

        user.IsEmailConfirmed = true;
        user.LastUpdate = DateTimeOffset.UtcNow;
        await userCommandService.Update(user, saveChanges: true, cancellationToken);
        return new ConfirmUserEmailResponse { IsEmailConfirmed = true };
    }

    // === Helpers ===

    private async Task TryNotifyAdminsNewUserAsync(User user, string? login, string registrationSource, CancellationToken ct)
    {
        try
        {
            await appNotificationFacade.UserRegistered(
                user.Id,
                user.DisplayName ?? "",
                login,
                user.Email,
                registrationSource,
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to notify admins about new user {UserId}", user.Id);
        }
    }

    private async Task<UserDto> BuildUserDtoAsync(User user, CancellationToken ct)
    {
        var link = await userIdentityLinkQueryService.GetByUserId(user.Id, ct);
        return BuildUserDto(user, link);
    }

    private static UserDto BuildUserDto(User user, UserIdentityLink? link)
    {
        var dto = user.Adapt<UserDto>();

        if (link != null)
        {
            dto.Provider = link.Provider;
            dto.ExternalId = link.ExternalId;
            dto.ProviderRowId = link.ProviderRowId;
        }
        else
        {
            dto.Provider = string.Empty;
            dto.ExternalId = string.Empty;
            dto.ProviderRowId = null;
        }

        return dto;
    }
}
