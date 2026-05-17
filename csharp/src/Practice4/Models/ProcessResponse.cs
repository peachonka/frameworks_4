namespace Practice4.Models;
public record ProcessResponse(
    string ProcessKey,
    ProcessState State,
    string CorrelationId,
    string Message
);