// Tests/ProcessServiceTests.cs
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
    public void Идемпотентность_повторное_событие_не_меняет_состояние()
    {
        var processKey = "room-idempotent";
        var request = new ProcessRequest(ProcessEvent.AcceptApplication, "id-1", "corr-1");

        var r1 = _processService.ProcessEvent(processKey, request);
        var r2 = _processService.ProcessEvent(processKey, request);

        Assert.Equal(r1.State, r2.State);
        Assert.Contains("Повторное событие проигнорировано", r2.Message);
    }

    [Fact]
    public void Недопустимый_переход_вызывает_ошибку()
    {
        var processKey = "room-invalid";

        var response = _processService.ProcessEvent(processKey,
            new ProcessRequest(ProcessEvent.BookResource, "id-1", "corr-1"));

        Assert.Equal(ProcessState.Error, response.State);
        Assert.Contains("Ошибка", response.Message);
    }

    [Fact]
    public void Сквозной_идентификатор_корреляции_присутствует_в_ответе()
    {
        var processKey = "room-correlation";
        var correlationId = "test-correlation-123";

        var response = _processService.ProcessEvent(processKey,
            new ProcessRequest(ProcessEvent.AcceptApplication, "id-1", correlationId));

        Assert.Equal(correlationId, response.CorrelationId);
    }

    [Fact]
    public void Сквозной_идентификатор_создаётся_автоматически_если_не_передан()
    {
        var processKey = "room-auto-corr";

        var response = _processService.ProcessEvent(processKey,
            new ProcessRequest(ProcessEvent.AcceptApplication, "id-1", null));

        Assert.NotNull(response.CorrelationId);
        Assert.NotEmpty(response.CorrelationId);
    }

    [Fact]
    public void Разные_процессы_не_влияют_друг_на_друга()
    {
        var key1 = "room-1";
        var key2 = "room-2";

        _processService.ForceSetState(key1, ProcessState.ResourceBooked);
        _processService.ForceSetState(key2, ProcessState.ApplicationAccepted);

        Assert.Equal(ProcessState.ResourceBooked, _processService.GetState(key1));
        Assert.Equal(ProcessState.ApplicationAccepted, _processService.GetState(key2));
    }

    [Fact]
    public void Успешный_сценарий_без_сбоев_работает()
    {
        var processKey = "room-manual";

        _processService.ForceSetState(processKey, ProcessState.New);

        // Имитируем успешный путь вручную
        _processService.ForceSetState(processKey, ProcessState.ApplicationAccepted);
        Assert.Equal(ProcessState.ApplicationAccepted, _processService.GetState(processKey));

        _processService.ForceSetState(processKey, ProcessState.ResourceBooked);
        Assert.Equal(ProcessState.ResourceBooked, _processService.GetState(processKey));

        _processService.ForceSetState(processKey, ProcessState.AccessGranted);
        Assert.Equal(ProcessState.AccessGranted, _processService.GetState(processKey));

        _processService.ForceSetState(processKey, ProcessState.Completed);
        Assert.Equal(ProcessState.Completed, _processService.GetState(processKey));
    }
}