using System.Text.Json;
using Construction.Procurement.Contracts.Events;
using Xunit;

namespace Construction.Procurement.Contract.Tests;

public class PurchaseOrderEventContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("procurement.purchase-order-issued.v1.schema.json", typeof(PurchaseOrderIssuedV1))]
    [InlineData("procurement.purchase-order-modified.v1.schema.json", typeof(PurchaseOrderModifiedV1))]
    [InlineData("procurement.purchase-order-cancelled.v1.schema.json", typeof(PurchaseOrderCancelledV1))]
    [InlineData("procurement.supplier-payment-scheduled.v1.schema.json", typeof(SupplierPaymentScheduledV1))]
    public void EventPayload_ContainsEveryRequiredSchemaField(string schemaFile, Type eventType)
    {
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "schemas", schemaFile)));
        var required = schema.RootElement.GetProperty("required").EnumerateArray()
            .Select(property => property.GetString()!)
            .ToArray();
        var sample = CreateSample(eventType);
        var payload = JsonDocument.Parse(JsonSerializer.Serialize(sample, eventType, JsonOptions));
        var schemaProperties = schema.RootElement.GetProperty("properties").EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);

        Assert.Equal("object", schema.RootElement.GetProperty("type").GetString());
        Assert.False(schema.RootElement.GetProperty("additionalProperties").GetBoolean());
        foreach (var property in required)
            Assert.True(payload.RootElement.TryGetProperty(property, out _), $"Payload is missing schema field '{property}'.");
        Assert.Equal(schemaProperties.Keys.Order(StringComparer.Ordinal),
            payload.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        foreach (var payloadProperty in payload.RootElement.EnumerateObject())
        {
            var schemaProperty = schemaProperties[payloadProperty.Name];
            var expectedType = schemaProperty.GetProperty("type").GetString();
            var actualType = GetJsonType(payloadProperty.Value.ValueKind);
            if (expectedType == "integer")
            {
                Assert.Equal("number", actualType);
                Assert.True(payloadProperty.Value.TryGetInt64(out _), $"Payload field '{payloadProperty.Name}' is not an integer.");
            }
            else
            {
                Assert.Equal(expectedType, actualType);
            }
            if (schemaProperty.TryGetProperty("format", out var format) && format.GetString() == "uuid")
                Assert.True(Guid.TryParse(payloadProperty.Value.GetString(), out _));
        }
        var procurementEvent = Assert.IsAssignableFrom<IProcurementEvent>(sample);
        Assert.Equal("procurement." + schemaFile.Replace("procurement.", "", StringComparison.Ordinal)
            .Replace(".schema.json", "", StringComparison.Ordinal), procurementEvent.EventType);
        Assert.Equal(procurementEvent.TenantId, JsonSerializer.Deserialize<JsonElement>(
            JsonSerializer.Serialize(sample, eventType, JsonOptions)).GetProperty("tenantId").GetGuid());
    }

    private static string GetJsonType(JsonValueKind kind) => kind switch
    {
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Object => "object",
        JsonValueKind.Array => "array",
        JsonValueKind.Null => "null",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static object CreateSample(Type eventType)
    {
        var id = Guid.Parse("019a4d4e-91f7-7cf9-b7b9-c859a1516f66");
        var tenantId = Guid.Parse("019a4d4e-91f7-7cf9-b7b9-c859a1516f67");
        var orderId = Guid.Parse("019a4d4e-91f7-7cf9-b7b9-c859a1516f68");
        var supplierId = Guid.Parse("019a4d4e-91f7-7cf9-b7b9-c859a1516f69");
        var projectId = Guid.Parse("019a4d4e-91f7-7cf9-b7b9-c859a1516f70");
        var at = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

        if (eventType == typeof(PurchaseOrderIssuedV1))
            return new PurchaseOrderIssuedV1(id, tenantId, orderId, "PO-DEV-0001", 1, supplierId, projectId, 1200m, "EGP", at);
        if (eventType == typeof(PurchaseOrderModifiedV1))
            return new PurchaseOrderModifiedV1(id, tenantId, orderId, "PO-DEV-0001", 2, supplierId, projectId, 1250m, "EGP", at);
        if (eventType == typeof(PurchaseOrderCancelledV1))
            return new PurchaseOrderCancelledV1(id, tenantId, orderId, "PO-DEV-0001", 2, "Supplier requested cancellation", at);
        if (eventType == typeof(SupplierPaymentScheduledV1))
            return new SupplierPaymentScheduledV1(id, tenantId, Guid.NewGuid(), orderId, supplierId, "PAY-DEV-0001", 1250m, "EGP", DateOnly.FromDateTime(at.Date), at);

        throw new ArgumentOutOfRangeException(nameof(eventType));
    }
}
