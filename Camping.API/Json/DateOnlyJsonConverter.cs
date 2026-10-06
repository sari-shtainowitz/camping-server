using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Camping.API.Json;

/// <summary>
/// <see cref="System.Text.Json"/> converter for <see cref="DateOnly"/>.
///
/// .NET 6's serializer has no built-in support for <see cref="DateOnly"/> - that
/// arrived in .NET 7. Without this converter, any request or response carrying a
/// <see cref="DateOnly"/> property (a reservation's <c>StartDate</c>/<c>EndDate</c>)
/// fails with <see cref="NotSupportedException"/> and the endpoint returns 500.
///
/// Values are read and written as ISO-8601 calendar dates (<c>yyyy-MM-dd</c>).
/// </summary>
public sealed class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    private const string DateFormat = "yyyy-MM-dd";

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new JsonException("Expected an ISO-8601 date (yyyy-MM-dd) but the value was empty.");
        }

        if (!DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new JsonException($"'{text}' is not a valid date. Expected the format yyyy-MM-dd.");
        }

        return date;
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(DateFormat, CultureInfo.InvariantCulture));
    }
}
