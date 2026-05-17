// Services/ProcessStateMachine.cs
using Practice4.Models;

namespace Practice4.Services;

public class ProcessStateMachine
{
    private readonly ILogger<ProcessStateMachine> _logger;

    public ProcessStateMachine(ILogger<ProcessStateMachine> logger)
    {
        _logger = logger;
    }

    public (ProcessState NewState, bool CompensationRequired) ApplyEvent(
        ProcessState currentState, ProcessEvent @event, string processKey)
    {
        switch (currentState, @event)
        {
            case (ProcessState.New, ProcessEvent.AcceptApplication):
                if (Random.Shared.NextDouble() < 0.1)
                    throw new InvalidOperationException("Ошибка приёма заявки");
                return (ProcessState.ApplicationAccepted, false);

            case (ProcessState.ApplicationAccepted, ProcessEvent.BookResource):
                if (Random.Shared.NextDouble() < 0.1)
                    throw new InvalidOperationException("Ошибка бронирования ресурса");
                return (ProcessState.ResourceBooked, false);

            case (ProcessState.ResourceBooked, ProcessEvent.GrantAccess):
                if (Random.Shared.NextDouble() < 0.2)
                {
                    _logger.LogWarning(
                        "Сбой при выдаче доступа для процесса {ProcessKey} — требуется компенсация бронирования",
                        processKey);
                    return (ProcessState.CompensationDone, true);
                }
                return (ProcessState.AccessGranted, false);

            case (ProcessState.AccessGranted, ProcessEvent.Complete):
                if (Random.Shared.NextDouble() < 0.05)
                    throw new InvalidOperationException("Ошибка завершения");
                return (ProcessState.Completed, false);

            case (ProcessState.CompensationDone, _):
                return (ProcessState.Error, false);

            default:
                throw new InvalidOperationException(
                    $"Недопустимый переход: {currentState} -> {@event}");
        }
    }
}