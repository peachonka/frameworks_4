namespace Practice4.Models;
public record ProcessRequest(
    ProcessEvent Event,
    string IdempotencyKey,
    string? CorrelationId = null
);