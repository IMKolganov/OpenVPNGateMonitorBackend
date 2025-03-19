namespace OpenVPNGateMonitor.SharedModels.Responses;

public class ApiResponse<T>
{
    public bool Success { get; set; } = false;
    public string Message { get; set; } =  string.Empty;
    public T Data { get; set; } =  default(T) ?? throw new InvalidOperationException();

    public static ApiResponse<T> SuccessResponse(T data, string message = "Success") =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T?> ErrorResponse(string message) =>
        new() { Success = false, Message = message, Data = default };
}