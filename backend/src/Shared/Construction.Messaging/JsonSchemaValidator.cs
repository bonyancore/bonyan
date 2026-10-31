using System.Text.Json;

namespace Construction.Messaging;

/// <summary>
/// Result of a JSON schema validation.
/// </summary>
public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Success { get; } = new(true, Array.Empty<string>());

    public static ValidationResult Failure(IEnumerable<string> errors) =>
        new(false, errors.ToList());

    public static ValidationResult Failure(string error) =>
        new(false, new[] { error });

    public void EnsureValid()
    {
        if (!IsValid)
        {
            throw new InvalidOperationException(
                $"Schema validation failed:\n{string.Join("\n", Errors)}");
        }
    }
}

/// <summary>
/// Lightweight JSON schema validation helper for event contract tests.
/// Built using System.Text.Json with zero third-party dependencies.
/// </summary>
public static class JsonSchemaValidator
{
    private static readonly JsonSerializerOptions DefaultSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Loads and parses a JSON schema from a string.
    /// </summary>
    public static JsonDocument LoadSchema(string schemaJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaJson);
        return JsonDocument.Parse(schemaJson);
    }

    /// <summary>
    /// Loads and parses a JSON schema from a file path.
    /// </summary>
    public static JsonDocument LoadSchemaFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"JSON schema file not found at '{filePath}'.", filePath);
        }

        using var stream = File.OpenRead(filePath);
        return JsonDocument.Parse(stream);
    }

    /// <summary>
    /// Loads and parses a JSON schema from a stream.
    /// </summary>
    public static JsonDocument LoadSchemaFromStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return JsonDocument.Parse(stream);
    }

    /// <summary>
    /// Validates a payload object against a JSON schema string.
    /// </summary>
    public static ValidationResult Validate<T>(T payload, string schemaJson, JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var json = JsonSerializer.Serialize(payload, options ?? DefaultSerializerOptions);
        return Validate(json, schemaJson);
    }

    /// <summary>
    /// Validates a JSON string payload against a JSON schema string.
    /// </summary>
    public static ValidationResult Validate(string payloadJson, string schemaJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaJson);

        using var payloadDoc = JsonDocument.Parse(payloadJson);
        using var schemaDoc = JsonDocument.Parse(schemaJson);

        return Validate(payloadDoc.RootElement, schemaDoc.RootElement);
    }

    /// <summary>
    /// Validates a JsonElement payload against a JsonElement schema.
    /// </summary>
    public static ValidationResult Validate(JsonElement payload, JsonElement schema)
    {
        var errors = new List<string>();
        ValidateElement(payload, schema, "$", errors);

        return errors.Count == 0
            ? ValidationResult.Success
            : ValidationResult.Failure(errors);
    }

    private static void ValidateElement(
        JsonElement element,
        JsonElement schema,
        string path,
        List<string> errors)
    {
        // 1. Check type
        if (schema.TryGetProperty("type", out var typeProp))
        {
            if (!ValidateType(element, typeProp, path, errors))
            {
                return; // Type mismatch, skip further checks on this element
            }
        }

        // 2. Check enum
        if (schema.TryGetProperty("enum", out var enumProp) && enumProp.ValueKind == JsonValueKind.Array)
        {
            bool matches = false;
            foreach (var allowed in enumProp.EnumerateArray())
            {
                if (JsonElementsEqual(element, allowed))
                {
                    matches = true;
                    break;
                }
            }

            if (!matches)
            {
                errors.Add($"Value at '{path}' is '{element.GetRawText()}', which is not in allowed enum values: {enumProp.GetRawText()}.");
            }
        }

        // 3. String format check
        if (element.ValueKind == JsonValueKind.String && schema.TryGetProperty("format", out var formatProp))
        {
            var format = formatProp.GetString();
            var strVal = element.GetString() ?? string.Empty;

            if (format == "uuid" && !Guid.TryParse(strVal, out _))
            {
                errors.Add($"Value at '{path}' is not a valid UUID format: '{strVal}'.");
            }
            else if (format == "date-time" && !DateTimeOffset.TryParse(strVal, out _))
            {
                errors.Add($"Value at '{path}' is not a valid date-time format: '{strVal}'.");
            }
        }

        // 4. Object validation (required properties, properties, additionalProperties)
        if (element.ValueKind == JsonValueKind.Object)
        {
            var allowedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (schema.TryGetProperty("properties", out var propertiesSchema) &&
                propertiesSchema.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in propertiesSchema.EnumerateObject())
                {
                    allowedProperties.Add(prop.Name);

                    if (element.TryGetProperty(prop.Name, out var childElement))
                    {
                        ValidateElement(childElement, prop.Value, $"{path}.{prop.Name}", errors);
                    }
                }
            }

            // Check required fields
            if (schema.TryGetProperty("required", out var requiredProp) &&
                requiredProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var req in requiredProp.EnumerateArray())
                {
                    var reqName = req.GetString();
                    if (!string.IsNullOrEmpty(reqName) && !element.TryGetProperty(reqName, out _))
                    {
                        errors.Add($"Required property '{reqName}' is missing at '{path}'.");
                    }
                }
            }

            // Check additionalProperties: false
            if (schema.TryGetProperty("additionalProperties", out var additionalProp) &&
                additionalProp.ValueKind == JsonValueKind.False)
            {
                foreach (var prop in element.EnumerateObject())
                {
                    if (!allowedProperties.Contains(prop.Name))
                    {
                        errors.Add($"Additional property '{prop.Name}' is not allowed at '{path}'.");
                    }
                }
            }
        }

        // 5. Array validation (items)
        if (element.ValueKind == JsonValueKind.Array &&
            schema.TryGetProperty("items", out var itemsSchema))
        {
            int index = 0;
            foreach (var item in element.EnumerateArray())
            {
                ValidateElement(item, itemsSchema, $"{path}[{index}]", errors);
                index++;
            }
        }
    }

    private static bool ValidateType(
        JsonElement element,
        JsonElement typeProp,
        string path,
        List<string> errors)
    {
        var expectedTypes = new List<string>();
        if (typeProp.ValueKind == JsonValueKind.String)
        {
            var t = typeProp.GetString();
            if (t is not null) expectedTypes.Add(t);
        }
        else if (typeProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in typeProp.EnumerateArray())
            {
                var s = t.GetString();
                if (s is not null) expectedTypes.Add(s);
            }
        }

        bool match = false;
        foreach (var expected in expectedTypes)
        {
            match = expected switch
            {
                "object" => element.ValueKind == JsonValueKind.Object,
                "array" => element.ValueKind == JsonValueKind.Array,
                "string" => element.ValueKind == JsonValueKind.String,
                "number" => element.ValueKind == JsonValueKind.Number,
                "integer" => element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out _),
                "boolean" => element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False,
                "null" => element.ValueKind == JsonValueKind.Null,
                _ => true
            };

            if (match) break;
        }

        if (!match)
        {
            errors.Add($"Expected type '{string.Join(" or ", expectedTypes)}' at '{path}', but found '{element.ValueKind}'.");
            return false;
        }

        return true;
    }

    private static bool JsonElementsEqual(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind) return false;

        return a.ValueKind switch
        {
            JsonValueKind.String => string.Equals(a.GetString(), b.GetString(), StringComparison.Ordinal),
            JsonValueKind.Number => a.GetRawText() == b.GetRawText(),
            JsonValueKind.True => true,
            JsonValueKind.False => true,
            JsonValueKind.Null => true,
            _ => a.GetRawText() == b.GetRawText()
        };
    }
}
