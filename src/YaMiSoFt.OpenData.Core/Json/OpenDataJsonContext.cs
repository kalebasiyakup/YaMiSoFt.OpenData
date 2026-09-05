using System.Text.Json.Serialization;
using YaMiSoFt.OpenData.Core.Models;
using YaMiSoFt.OpenData.Core.Querying;

namespace YaMiSoFt.OpenData.Core.Json;

/// <summary>
/// Source-generated serialization metadata. Reflection-free so the API stays AOT-compatible
/// even though AOT publishing is deferred to Faz 2 (PLAN.md 3.9).
/// </summary>
// Enums are written as names, not ordinals. A "kind": 1 in a public response tells a caller
// nothing and silently changes meaning if a member is ever inserted; "village" does neither.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true,
    WriteIndented = false)]
[JsonSerializable(typeof(Province))]
[JsonSerializable(typeof(IReadOnlyList<Province>))]
[JsonSerializable(typeof(PagedResult<Province>))]
[JsonSerializable(typeof(DataSet<Province>))]
[JsonSerializable(typeof(District))]
[JsonSerializable(typeof(IReadOnlyList<District>))]
[JsonSerializable(typeof(PagedResult<District>))]
[JsonSerializable(typeof(DataSet<District>))]
[JsonSerializable(typeof(Quarter))]
[JsonSerializable(typeof(IReadOnlyList<Quarter>))]
[JsonSerializable(typeof(PagedResult<Quarter>))]
[JsonSerializable(typeof(DataSet<Quarter>))]
[JsonSerializable(typeof(Currency))]
[JsonSerializable(typeof(IReadOnlyList<Currency>))]
[JsonSerializable(typeof(PagedResult<Currency>))]
[JsonSerializable(typeof(DataSet<Currency>))]
[JsonSerializable(typeof(LanguageInfo))]
[JsonSerializable(typeof(IReadOnlyList<LanguageInfo>))]
[JsonSerializable(typeof(PagedResult<LanguageInfo>))]
[JsonSerializable(typeof(DataSet<LanguageInfo>))]
[JsonSerializable(typeof(Country))]
[JsonSerializable(typeof(IReadOnlyList<Country>))]
[JsonSerializable(typeof(PagedResult<Country>))]
[JsonSerializable(typeof(DataSet<Country>))]
[JsonSerializable(typeof(Holiday))]
[JsonSerializable(typeof(IReadOnlyList<Holiday>))]
[JsonSerializable(typeof(PagedResult<Holiday>))]
[JsonSerializable(typeof(DataSet<Holiday>))]
[JsonSerializable(typeof(MobilePrefix))]
[JsonSerializable(typeof(IReadOnlyList<MobilePrefix>))]
[JsonSerializable(typeof(DataSet<MobilePrefix>))]
[JsonSerializable(typeof(MobileOperator))]
[JsonSerializable(typeof(IReadOnlyList<MobileOperator>))]
[JsonSerializable(typeof(PagedResult<MobileOperator>))]
[JsonSerializable(typeof(DataSet<MobileOperator>))]
[JsonSerializable(typeof(Neighborhood))]
[JsonSerializable(typeof(IReadOnlyList<Neighborhood>))]
[JsonSerializable(typeof(PagedResult<Neighborhood>))]
[JsonSerializable(typeof(DataSet<Neighborhood>))]
public sealed partial class OpenDataJsonContext : JsonSerializerContext;
