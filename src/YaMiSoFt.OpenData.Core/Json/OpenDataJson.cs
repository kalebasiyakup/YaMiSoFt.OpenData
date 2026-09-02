using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.Unicode;

namespace YaMiSoFt.OpenData.Core.Json;

/// <summary>
/// The serializer options every response is written with.
/// </summary>
/// <remarks>
/// The default encoder escapes everything outside ASCII, which would turn "Türkiye" into
/// "Türkiye" — larger on the wire and unreadable in a terminal, on an API whose whole
/// point is Turkish reference data. Allowing all Unicode ranges emits those characters
/// directly.
///
/// A handful of ASCII characters stay escaped no matter what the settings say — the angle
/// brackets, ampersand, apostrophe and plus — because <c>JavaScriptEncoder.Create</c> treats
/// them as unconditionally unsafe. So a UTC offset serializes as "+03:00". That is
/// correct JSON and every parser decodes it back to "+", and the alternative
/// (<c>UnsafeRelaxedJsonEscaping</c>) would also unescape the angle brackets — not worth it
/// on an API that echoes caller-supplied text back in its error messages.
///
/// Resolution stays on the source-generated context, so this is a change of encoding, not a
/// return to reflection.
/// </remarks>
public static class OpenDataJson
{
    /// <summary>Serializer options backed by the source-generated contracts.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = OpenDataJsonContext.Default,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    /// <summary>Resolves the generated contract for <typeparamref name="T"/>.</summary>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="T"/> is not declared on <see cref="OpenDataJsonContext"/>.
    /// </exception>
    public static JsonTypeInfo<T> TypeInfo<T>() =>
        Options.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
        ?? throw new InvalidOperationException(
            $"'{typeof(T)}' is not registered on {nameof(OpenDataJsonContext)}. "
            + "Add a [JsonSerializable] attribute for it.");
}
