using DataGateMonitor.Serialization;
using DataGateMonitor.SharedModels.Responses;
using Newtonsoft.Json.Linq;

namespace DataGateMonitor.Services.Helpers;

/// <summary>Reads <see cref="ApiResponse{T}"/> envelopes from DataGate OpenVPN / Xray manager HTTP APIs.</summary>
public static class MicroserviceApiResponseHelper
{
    public static async Task<T> ReadSuccessDataAsync<T>(HttpResponseMessage response, CancellationToken ct)
        where T : class
    {
        var wrapped = await ProjectJson.ReadContentAsync<ApiResponse<T>>(response.Content, ct).ConfigureAwait(false);
        if (wrapped is not { Success: true, Data: not null })
        {
            var message = wrapped?.Message is { Length: > 0 } msg
                ? msg
                : "Microservice returned unsuccessful response.";
            throw new InvalidOperationException(message);
        }

        return wrapped.Data;
    }

    public static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var raw = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw))
            return $"{(int)response.StatusCode} {response.ReasonPhrase}";

        try
        {
            var root = JObject.Parse(raw);
            var message = root["message"]?.ToString();
            if (!string.IsNullOrWhiteSpace(message))
                return message;

            var detail = root["detail"]?.ToString() ?? root["Detail"]?.ToString();
            if (!string.IsNullOrWhiteSpace(detail))
                return detail;

            var error = root["error"]?.ToString();
            if (!string.IsNullOrWhiteSpace(error))
                return error;
        }
        catch (Newtonsoft.Json.JsonException)
        {
            // fall through
        }

        return raw;
    }

    /// <summary>Returns JSON payload of <c>data</c> when body is an <see cref="ApiResponse{T}"/> envelope; otherwise the original JSON.</summary>
    public static string UnwrapSuccessPayloadJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return json;

        try
        {
            var root = JObject.Parse(json);
            if (root["success"] != null
                && root["data"] is { Type: not JTokenType.Null and not JTokenType.Undefined } dataEl)
                return dataEl.ToString(Newtonsoft.Json.Formatting.None);
        }
        catch (Newtonsoft.Json.JsonException)
        {
            // legacy bare DTO
        }

        return json;
    }
}
