namespace ComplianceScannerPro.Shared.DTOs;

public class ApiResponse<T>
{
    public bool Success { get; set; } = true;
    public T? Data { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static ApiResponse<T> SuccessResult(T data, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Message = message
        };
    }

    public static ApiResponse<T> ErrorResult(string error, T? data = default)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Data = data,
            Errors = new List<string> { error }
        };
    }

    public static ApiResponse<T> ErrorResult(List<string> errors, T? data = default)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Data = data,
            Errors = errors
        };
    }
}

public class PaginatedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}