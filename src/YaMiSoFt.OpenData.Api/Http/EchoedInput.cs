namespace YaMiSoFt.OpenData.Api.Http;

/// <summary>
/// Bounds caller-supplied text before it is quoted back in an error message.
/// </summary>
/// <remarks>
/// Problem details name the value that was rejected, which is what makes them useful to
/// debug against. But that reflects whatever the caller sent, so a request can otherwise
/// decide the size of a response the server produces — a kilobyte of query string becoming a
/// kilobyte of error body, for free, on every rejected request. Truncating caps that, and the
/// first few dozen characters are all anyone needs to recognise their own typo.
/// </remarks>
public static class EchoedInput
{
    private const int MaxLength = 64;

    /// <summary>Returns <paramref name="value"/> shortened to a length safe to echo.</summary>
    public static string Clip(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= MaxLength ? value : value[..MaxLength] + "…";
    }
}
