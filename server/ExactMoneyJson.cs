using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Decont;
// Opt-in exact wire format retains the existing public numeric API for older integrations.
public sealed class ExactMoneyJson(IHttpContextAccessor context) : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) { if (reader.TokenType == JsonTokenType.Number) return reader.GetDecimal(); if (reader.TokenType == JsonTokenType.String && InvoiceArithmetic.Parse(reader.GetString()) is decimal value) return value; throw new JsonException("Use an invariant decimal string."); }
    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) { if (context.HttpContext?.Request.Headers["X-Money-Format"].ToString() == "exact-v1") writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture)); else writer.WriteNumberValue(value); }
}
