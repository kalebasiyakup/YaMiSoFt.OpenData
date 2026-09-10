using YaMiSoFt.OpenData.Api.Http;
using YaMiSoFt.OpenData.Api.OpenApi;
using YaMiSoFt.OpenData.Core.Models;
using YaMiSoFt.OpenData.Core.Validation;

namespace YaMiSoFt.OpenData.Api.Endpoints;

/// <summary>
/// Format/checksum validation endpoints (BRD §3.1 Faz 3: IBAN, T.C. Kimlik No).
/// </summary>
/// <remarks>
/// Unlike every other endpoint in this API, these are not backed by a committed dataset — they
/// are pure calculation, so there is no <c>IReferenceStore</c>, no dataset version and no
/// <c>DataResult</c> here; a plain 200 with the outcome is the whole contract.
/// </remarks>
public static class ValidationEndpoints
{
    /// <summary>Maps the validation routes onto <paramref name="group"/>.</summary>
    public static IEndpointRouteBuilder MapValidationEndpoints(this IEndpointRouteBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var validate = group.MapGroup("/validate").WithTags("Validation");

        validate.MapGet("/iban/{iban}", (string iban) =>
            {
                var isValid = IbanValidator.Validate(iban, out var normalized, out var reason);

                return Results.Ok(new ValidationResult
                {
                    Value = EchoedInput.Clip(normalized),
                    IsValid = isValid,
                    Reason = reason,
                });
            })
            .WithName("ValidateIban")
            .WithBilingualSummary(
                "Validates an IBAN's format and checksum. Calculation only — no bank data is looked up.",
                "Bir IBAN'ın formatını ve doğrulama rakamını kontrol eder. Sadece hesaplama — banka verisi sorgulanmaz.");

        validate.MapGet("/tc-kimlik/{no}", (string no) =>
            {
                var isValid = TcKimlikValidator.Validate(no, out var normalized, out var reason);

                return Results.Ok(new ValidationResult
                {
                    Value = EchoedInput.Clip(normalized),
                    IsValid = isValid,
                    Reason = reason,
                });
            })
            .WithName("ValidateTcKimlik")
            .WithBilingualSummary(
                "Validates a T.C. Kimlik No's checksum. Format only — this does not confirm the "
                + "number belongs to a real, registered person.",
                "Bir T.C. Kimlik Numarasının doğrulama rakamlarını kontrol eder. Sadece format — "
                + "numaranın gerçek, kayıtlı bir kişiye ait olduğunu doğrulamaz.");

        return group;
    }
}
