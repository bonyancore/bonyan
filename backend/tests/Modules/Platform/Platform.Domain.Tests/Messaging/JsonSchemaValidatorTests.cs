using Construction.Messaging;
using Xunit;

namespace Construction.Platform.Domain.Tests.Messaging;

public sealed class JsonSchemaValidatorTests
{
    private const string SampleSchema = """
    {
      "$schema": "http://json-schema.org/draft-07/schema#",
      "type": "object",
      "required": ["tenantId", "name", "amount"],
      "properties": {
        "tenantId": { "type": "string", "format": "uuid" },
        "name": { "type": "string" },
        "amount": { "type": "number" },
        "status": { "type": "string", "enum": ["draft", "active", "archived"] }
      },
      "additionalProperties": false
    }
    """;

    [Fact]
    public void Validate_ValidPayload_ReturnsSuccess()
    {
        var payload = """
        {
          "tenantId": "018f6c38-23bb-7cb3-8a3d-3df4c3e80001",
          "name": "Tower A Foundation",
          "amount": 150000.50,
          "status": "active"
        }
        """;

        var result = JsonSchemaValidator.Validate(payload, SampleSchema);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_MissingRequiredProperty_ReturnsFailure()
    {
        var payload = """
        {
          "tenantId": "018f6c38-23bb-7cb3-8a3d-3df4c3e80001",
          "name": "Tower A Foundation"
        }
        """;

        var result = JsonSchemaValidator.Validate(payload, SampleSchema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Required property 'amount' is missing"));
    }

    [Fact]
    public void Validate_TypeMismatch_ReturnsFailure()
    {
        var payload = """
        {
          "tenantId": "018f6c38-23bb-7cb3-8a3d-3df4c3e80001",
          "name": "Tower A Foundation",
          "amount": "not-a-number"
        }
        """;

        var result = JsonSchemaValidator.Validate(payload, SampleSchema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("amount") && e.Contains("Expected type 'number'"));
    }

    [Fact]
    public void Validate_InvalidUuidFormat_ReturnsFailure()
    {
        var payload = """
        {
          "tenantId": "not-a-valid-uuid",
          "name": "Tower A Foundation",
          "amount": 100.00
        }
        """;

        var result = JsonSchemaValidator.Validate(payload, SampleSchema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("not a valid UUID format"));
    }

    [Fact]
    public void Validate_DisallowedEnumValue_ReturnsFailure()
    {
        var payload = """
        {
          "tenantId": "018f6c38-23bb-7cb3-8a3d-3df4c3e80001",
          "name": "Tower A Foundation",
          "amount": 100.00,
          "status": "deleted"
        }
        """;

        var result = JsonSchemaValidator.Validate(payload, SampleSchema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("not in allowed enum values"));
    }

    [Fact]
    public void Validate_DisallowedAdditionalProperty_ReturnsFailure()
    {
        var payload = """
        {
          "tenantId": "018f6c38-23bb-7cb3-8a3d-3df4c3e80001",
          "name": "Tower A Foundation",
          "amount": 100.00,
          "unexpectedField": "intruder"
        }
        """;

        var result = JsonSchemaValidator.Validate(payload, SampleSchema);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Additional property 'unexpectedField' is not allowed"));
    }

    [Fact]
    public void Validate_StronglyTypedObject_ReturnsSuccess()
    {
        var obj = new
        {
            tenantId = Guid.NewGuid(),
            name = "Project Alpha",
            amount = 450000.00
        };

        var result = JsonSchemaValidator.Validate(obj, SampleSchema);

        Assert.True(result.IsValid);
    }
}
