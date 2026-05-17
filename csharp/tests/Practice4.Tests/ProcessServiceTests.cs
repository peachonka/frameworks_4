using Microsoft.Extensions.Logging;
using Practice4.Models;
using Practice4.Services;
using Xunit;

namespace Practice4.Tests;

public sealed class ProcessServiceTests
{
    private readonly ProcessService _processService;

    public ProcessServiceTests()
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<ProcessService>();
        var stateMachineLogger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<ProcessStateMachine>();
        
        var stateMachine = new ProcessStateMachine(stateMachineLogger);
        _processService = new ProcessService(stateMachine, logger);
    }

    [Fact]
    public void Полный_успешный_сценарий_проходит_все_состояния()
    {
        // Arrange
        var processKey = "room-success";

        // Act & Assert — шаг 1: Принять заявку
        var r1 = _processService.ProcessEvent(processKey,
            new ProcessRequest(ProcessEvent.AcceptApplication, "id-1", "corr-1"));
        Assert.Equal(ProcessState.ApplicationAccepted, r1.State);

        // Шаг 2: Забронировать ресурс
        var r2 = _processService.ProcessEvent(processKey,
            new ProcessRequest(ProcessEvent.BookResource, "id-2", "corr-2"));
        Assert.Equal(ProcessState.ResourceBooked, r2.State);

        // Шаг 3: Выдать доступ
        var r3 = _processService.ProcessEvent(processKey,
            new ProcessRequest(ProcessEvent.GrantAccess, "id-3", "corr-3"));
        Assert.Equal(ProcessState.AccessGranted, r3.State);

        // Шаг 4: Завершить
        var r4 = _processService.ProcessEvent(processKey,
            new ProcessRequest(ProcessEvent.Complete, "id-4", "corr-4"));
        Assert.Equal(ProcessState.Completed, r4.State);
    }

    [Fact]
    public void Идемпотентность_повторное_событие_не_меняет_состояние()
    {
        // Arrange
        var processKey = "room-idempotent";
        var request = new ProcessRequest(ProcessEvent.AcceptApplication, "id-1", "corr-1");

        // Act — первое событие
        var r1 = _processService.ProcessEvent(processKey, request);
        Assert.Equal(ProcessState.ApplicationAccepted, r1.State);

        // Повторное событие с тем же ключом идемпотентности
        var r2 = _processService.ProcessEvent(processKey, request);

        // Assert
        Assert.Equal(ProcessState.ApplicationAccepted, r2.State);
        Assert.Contains("Повторное событие проигнорировано", r2.Message);
    }

    [Fact]
    public void Недопустимый_переход_вызывает_ошибку()
    {
        // Arrange
        var processKey = "room-invalid";

        // Act — пытаемся забронировать без принятия заявки
        var response = _processService.ProcessEvent(processKey,
            new ProcessRequest(ProcessEvent.BookResource, "id-1", "corr-1"));

        // Assert
        Assert.Equal(ProcessState.Error, response.State);
        Assert.Contains("Ошибка", response.Message);
    }

    [Fact]
    public void Сквозной_идентификатор_корреляции_присутствует_в_ответе()
    {
        // Arrange
        var processKey = "room-correlation";
        var correlationId = "test-correlation-123";

        // Act
        var response = _processService.ProcessEvent(processKey,
            new ProcessRequest(ProcessEvent.AcceptApplication, "id-1", correlationId));

        // Assert
        Assert.Equal(correlationId, response.CorrelationId);
    }

    [Fact]
    public void Сквозной_идентификатор_создаётся_автоматически_если_не_передан()
    {
        // Arrange
        var processKey = "room-auto-corr";

        // Act
        var response = _processService.ProcessEvent(processKey,
            new ProcessRequest(ProcessEvent.AcceptApplication, "id-1", null));

        // Assert
        Assert.NotNull(response.CorrelationId);
        Assert.NotEmpty(response.CorrelationId);
    }

    [Fact]
    public void Разные_процессы_не_влияют_друг_на_друга()
    {
        // Arrange
        var key1 = "room-1";
        var key2 = "room-2";

        // Act
        _processService.ProcessEvent(key1,
            new ProcessRequest(ProcessEvent.AcceptApplication, "id-1", "corr-1"));
        _processService.ProcessEvent(key1,
            new ProcessRequest(ProcessEvent.BookResource, "id-2", "corr-2"));

        _processService.ProcessEvent(key2,
            new ProcessRequest(ProcessEvent.AcceptApplication, "id-3", "corr-3"));

        // Assert
        Assert.Equal(ProcessState.ResourceBooked, _processService.GetState(key1));
        Assert.Equal(ProcessState.ApplicationAccepted, _processService.GetState(key2));
    }
}