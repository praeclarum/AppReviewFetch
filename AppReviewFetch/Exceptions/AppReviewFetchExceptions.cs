namespace AppReviewFetch.Exceptions;

/// <summary>
/// Base exception for App Review Fetch operations.
/// </summary>
public class AppReviewFetchException : Exception
{
    public AppReviewFetchException(string message) : base(message)
    {
    }

    public AppReviewFetchException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when API returns an error response.
/// </summary>
public class ApiErrorException : AppReviewFetchException
{
    /// <summary>
    /// The HTTP status code returned by the API.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// The error code from the API response.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Detailed error information from the API.
    /// </summary>
    public string ErrorDetail { get; }

    public ApiErrorException(int statusCode, string errorCode, string title, string detail)
        : base($"API Error ({errorCode}): {title}")
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        ErrorDetail = detail;
    }

    public ApiErrorException(int statusCode, string message)
        : base($"API Error (HTTP {statusCode}): {message}")
    {
        StatusCode = statusCode;
        ErrorCode = statusCode.ToString();
        ErrorDetail = message;
    }
}

/// <summary>
/// Exception thrown when authentication fails.
/// </summary>
public class AuthenticationException : AppReviewFetchException
{
    public AuthenticationException(string message) : base(message)
    {
    }

    public AuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when credentials file cannot be found or loaded.
/// </summary>
public class CredentialsException : AppReviewFetchException
{
    public CredentialsException(string message) : base(message)
    {
    }

    public CredentialsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
