using System.Net;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Npgsql;
using DataGateMonitor.Services.Others.Notifications;

namespace DataGateMonitor.Middlewares;

public class GlobalExceptionMiddleware(
    RequestDelegate next,
    IServiceProvider serviceProvider,
    ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException ex)
        {
            await HandleOperationCanceledAsync(context, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogExpectedUnauthorizedAccess(context, ex);
            await HandleExceptionAsync(context, ex, notifyAdmins: false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception occurred.");
            await HandleExceptionAsync(context, ex, notifyAdmins: true);
        }
    }

    private void LogExpectedUnauthorizedAccess(HttpContext context, UnauthorizedAccessException exception)
    {
        // Routine auth denial (idle timeout, invalid credentials, etc.) — Debug only to avoid Wazuh WRN alerts.
        logger.LogDebug(
            "Request was denied (401). Method: {Method}. Path: {Path}. QueryString: {QueryString}. " +
            "Message: {AuthMessage}. TraceId: {TraceId}",
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString.ToString(),
            exception.Message,
            context.TraceIdentifier);
    }

    private async Task HandleOperationCanceledAsync(HttpContext context, OperationCanceledException exception)
    {
        var method = context.Request.Method;
        var path = context.Request.Path;
        var queryString = context.Request.QueryString.ToString();
        var traceId = context.TraceIdentifier;
        var postgresCancelled = RequestCancellationLogging.FindPostgresQueryCancelled(exception);
        var wasAbortedByClient = RequestCancellationLogging.IsClientInitiatedCancellation(context, exception);

        if (postgresCancelled is not null && !wasAbortedByClient)
        {
            // Server-side statement timeout — real issue; structured only (no exception object → no Wazuh stack lines).
            logger.LogWarning(
                "PostgreSQL query was cancelled ({SqlState}). ClientAborted: {ClientAborted}. " +
                "Method: {Method}. Path: {Path}. QueryString: {QueryString}. Message: {PgMessage}. TraceId: {TraceId}",
                postgresCancelled.SqlState,
                wasAbortedByClient,
                method,
                path,
                queryString,
                postgresCancelled.MessageText,
                traceId);
        }
        else if (wasAbortedByClient)
        {
            logger.LogDebug(
                "Request was cancelled by client. Method: {Method}. Path: {Path}. QueryString: {QueryString}. TraceId: {TraceId}",
                method,
                path,
                queryString,
                traceId);
        }
        else
        {
            // e.g. HttpClient.Timeout during the request — keep visible, without stack trace.
            logger.LogWarning(
                "Operation was cancelled (not client-initiated). Method: {Method}. Path: {Path}. QueryString: {QueryString}. " +
                "Message: {CancelMessage}. TraceId: {TraceId}",
                method,
                path,
                queryString,
                exception.Message,
                traceId);
        }

        if (context.Response.HasStarted)
            return;

        try
        {
            context.Response.Clear();
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 499;

            var payload = new
            {
                statusCode = 499,
                message = "Request was cancelled.",
                detail = exception.Message,
                traceId
            };

            var json = JsonConvert.SerializeObject(payload);
            await context.Response.WriteAsync(json);
        }
        catch (OperationCanceledException)
        {
            // Client already disconnected — nothing to send.
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception, bool notifyAdmins = true)
    {
        if (context.Response.HasStarted)
            return;

        if (notifyAdmins)
        {
            // Notify admins (best-effort)
            try
            {
                using var scope = serviceProvider.CreateScope();
                var appNotifications = scope.ServiceProvider.GetRequiredService<IAppNotificationFacade>();
                await appNotifications.SystemException(exception, CancellationToken.None);
            }
            catch (Exception sendEx)
            {
                logger.LogError(sendEx, "Failed to send system exception notification.");
            }
        }

        var (statusCodeInt, responseMessage) = exception switch
        {
            UnauthorizedAccessException => ((int)HttpStatusCode.Unauthorized, exception.Message),
            ArgumentException => ((int)HttpStatusCode.BadRequest, exception.Message),
            InvalidOperationException ioe when IsRateLimitMessage(ioe.Message)
                => ((int)HttpStatusCode.TooManyRequests, ioe.Message),
            InvalidOperationException ioe when IsDuplicateOrConflictMessage(ioe.Message)
                => ((int)HttpStatusCode.Conflict, ioe.Message),
            InvalidOperationException ioe when IsClientFacingInvalidOperation(ioe.Message)
                => ((int)HttpStatusCode.BadRequest, ioe.Message),
            DbUpdateException when exception.InnerException is PostgresException pg && pg.SqlState == "23505"
                => ((int)HttpStatusCode.Conflict, "A resource already exists with the same key."),
            _ => ((int)HttpStatusCode.InternalServerError, "An unexpected error occurred. Please try again later.")
        };

        context.Response.Clear();
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCodeInt;

        var payload = new
        {
            statusCode = statusCodeInt,
            message = responseMessage,
            detail = GetExceptionDetails(exception),
            traceId = context.TraceIdentifier
        };

        var json = JsonConvert.SerializeObject(payload);
        await context.Response.WriteAsync(json);
    }

    private static bool IsRateLimitMessage(string message) =>
        message.Contains("Too many requests", StringComparison.OrdinalIgnoreCase);

    private static bool IsDuplicateOrConflictMessage(string message) =>
        message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
        || message.Contains("already in use", StringComparison.OrdinalIgnoreCase)
        || message.Contains("already has", StringComparison.OrdinalIgnoreCase);

    private static bool IsClientFacingInvalidOperation(string message) =>
        message.Contains("only supported for", StringComparison.OrdinalIgnoreCase)
        || message.Contains("is missing", StringComparison.OrdinalIgnoreCase)
        || message.Contains("not found", StringComparison.OrdinalIgnoreCase)
        || message.Contains("has expired", StringComparison.OrdinalIgnoreCase)
        || message.Contains("was denied", StringComparison.OrdinalIgnoreCase)
        || message.Contains("already completed", StringComparison.OrdinalIgnoreCase)
        || message.Contains("do not match", StringComparison.OrdinalIgnoreCase)
        || message.Contains("no longer pending", StringComparison.OrdinalIgnoreCase);

    private static string GetExceptionDetails(Exception ex)
    {
        if (ex is DbUpdateException dbEx)
        {
            if (dbEx.InnerException is PostgresException pg)
            {
                return $"{pg.MessageText} (SQLSTATE {pg.SqlState})";
            }

            if (dbEx.InnerException != null)
            {
                return dbEx.InnerException.Message;
            }
        }

        var current = ex;
        while (current.InnerException != null)
        {
            current = current.InnerException;
        }

        return current.Message;
    }
}