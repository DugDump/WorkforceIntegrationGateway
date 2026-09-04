using System.Text.Json;
using WorkforceIntegrationGateway.Api.Contracts;
using WorkforceIntegrationGateway.Application.VerificationRequests;

namespace WorkforceIntegrationGateway.Api.Requests;

public sealed class VerificationRequestRequestReader
{
    private const int MaximumBodyBytes = 16 * 1024;

    private static readonly string[] ExpectedProperties =
    [
        "clientReference",
        "employeeReference",
        "employerReference",
        "requestedData"
    ];

    internal async ValueTask<VerificationRequestReadResult> ReadAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync(request.Body, cancellationToken);

        if (body is null)
        {
            return Failure("body", "invalid_json_shape", "The JSON request body is too large.");
        }

        return Parse(body);
    }

    private static VerificationRequestReadResult Parse(ReadOnlySpan<byte> body)
    {
        if (body.IsEmpty)
        {
            return Failure("body", "malformed_json", "The JSON request body is empty.");
        }

        try
        {
            var reader = new Utf8JsonReader(body, new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                AllowMultipleValues = true
            });

            if (!reader.Read())
            {
                return Failure("body", "malformed_json", "The JSON request body is malformed.");
            }

            using var document = JsonDocument.ParseValue(ref reader);

            if (reader.Read())
            {
                return Failure("body", "trailing_content", "The JSON request body has trailing content.");
            }

            return ReadObject(document.RootElement);
        }
        catch (JsonException)
        {
            return Failure("body", "malformed_json", "The JSON request body is malformed.");
        }
    }

    private static VerificationRequestReadResult ReadObject(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Failure("body", "invalid_json_shape", "The JSON request body must be an object.");
        }

        var errors = new List<ApiValidationError>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        string? clientReference = null;
        string? employeeReference = null;
        string? employerReference = null;
        IReadOnlyList<string?>? requestedData = null;

        foreach (var property in root.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                errors.Add(new(property.Name, "duplicate_property", "A JSON property is duplicated."));
                continue;
            }

            if (!ExpectedProperties.Contains(property.Name, StringComparer.Ordinal))
            {
                errors.Add(new(SafeFieldIdentifier(property.Name), "unknown_property", "The JSON property is not supported."));
                continue;
            }

            switch (property.Name)
            {
                case "clientReference":
                    clientReference = ReadString(property, errors);
                    break;
                case "employeeReference":
                    employeeReference = ReadString(property, errors);
                    break;
                case "employerReference":
                    employerReference = ReadString(property, errors);
                    break;
                case "requestedData":
                    requestedData = ReadRequestedData(property, errors);
                    break;
            }
        }

        AddMissingErrors(seen, errors);
        var command = new CreateVerificationRequestCommand(
            clientReference,
            employeeReference,
            employerReference,
            requestedData);
        return new(command, errors);
    }

    private static string? ReadString(JsonProperty property, ICollection<ApiValidationError> errors)
    {
        if (property.Value.ValueKind == JsonValueKind.Null)
        {
            errors.Add(new(property.Name, "required", "A required value is missing."));
            return null;
        }

        if (property.Value.ValueKind != JsonValueKind.String)
        {
            errors.Add(new(property.Name, "invalid_json_shape", "The JSON value must be a string."));
            return null;
        }

        return property.Value.GetString();
    }

    private static IReadOnlyList<string?>? ReadRequestedData(
        JsonProperty property,
        ICollection<ApiValidationError> errors)
    {
        if (property.Value.ValueKind == JsonValueKind.Null)
        {
            errors.Add(new(property.Name, "at_least_one_required", "At least one requested-data value is required."));
            return null;
        }

        if (property.Value.ValueKind != JsonValueKind.Array)
        {
            errors.Add(new(property.Name, "invalid_json_shape", "The JSON value must be an array of strings."));
            return null;
        }

        var values = new List<string?>();

        foreach (var item in property.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                errors.Add(new(property.Name, "invalid_json_shape", "Each requested-data value must be a string."));
                continue;
            }

            values.Add(item.GetString());
        }

        return values;
    }

    private static void AddMissingErrors(
        IReadOnlySet<string> seen,
        ICollection<ApiValidationError> errors)
    {
        foreach (var property in ExpectedProperties)
        {
            if (seen.Contains(property))
            {
                continue;
            }

            var code = property == "requestedData" ? "at_least_one_required" : "required";
            errors.Add(new(property, code, "A required value is missing."));
        }
    }

    private static async ValueTask<byte[]?> ReadBodyAsync(Stream body, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaximumBodyBytes + 1];
        var length = 0;

        while (length < buffer.Length)
        {
            var read = await body.ReadAsync(buffer.AsMemory(length, buffer.Length - length), cancellationToken);

            if (read == 0)
            {
                return buffer[..length];
            }

            length += read;
        }

        return null;
    }

    private static VerificationRequestReadResult Failure(string field, string code, string message) =>
        new(null, [new ApiValidationError(field, code, message)]);

    private static string SafeFieldIdentifier(string propertyName) =>
        propertyName.Length is > 0 and <= 100 && propertyName.All(IsSafeFieldCharacter)
            ? propertyName
            : "body";

    private static bool IsSafeFieldCharacter(char value) =>
        value is >= 'A' and <= 'Z'
        or >= 'a' and <= 'z'
        or >= '0' and <= '9'
        or '.' or '_' or '-';
}
