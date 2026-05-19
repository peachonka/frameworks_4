// Services/ProcessService.cs
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Practice4.Models;

namespace Practice4.Services;

public class ProcessService
{
    private readonly ConcurrentDictionary<string, ProcessInstance> _processes = new();
    private readonly ProcessStateMachine _stateMachine;
    private readonly ILogger<ProcessService> _logger;

    private readonly Meter _meter;
    private readonly Counter<int> _successCounter;
    private readonly Counter<int> _errorCounter;
    private readonly Counter<int> _duplicateCounter;
    private readonly Counter<int> _compensationCounter;
    private readonly Histogram<double> _stepLatency;

    private volatile bool _isDegraded = false;
    public bool IsDegraded => _isDegraded;

    public ProcessService(ProcessStateMachine stateMachine, ILogger<ProcessService> logger)
    {
        _stateMachine = stateMachine;
        _logger = logger;

        _meter = new Meter("ProcessService", "1.0.0");
        _successCounter = _meter.CreateCounter<int>("process.success.transitions",
            description: "Число успешных переходов");
        _errorCounter = _meter.CreateCounter<int>("process.error.transitions",
            description: "Число ошибочных переходов");
        _duplicateCounter = _meter.CreateCounter<int>("process.duplicate.deliveries",
            description: "Число повторных доставок событий");
        _compensationCounter = _meter.CreateCounter<int>("process.compensations",
            description: "Число выполненных компенсаций");
        _stepLatency = _meter.CreateHistogram<double>("process.step.latency.ms",
            description: "Задержка обработки шага в миллисекундах");
    }

    public ProcessResponse ProcessEvent(string processKey, ProcessRequest request)
    {
        var correlationId = request.CorrelationId
                            ?? Activity.Current?.TraceId.ToString()
                            ?? Guid.NewGuid().ToString();

        var instance = _processes.GetOrAdd(processKey, _ => new ProcessInstance());

        if (!instance.ProcessedIdempotencyKeys.Add(request.IdempotencyKey))
        {
            _logger.LogInformation(
                "[{CorrelationId}] Повторная доставка события {Event} для процесса {ProcessKey}. Состояние: {State}",
                correlationId, request.Event, processKey, instance.State);
            _duplicateCounter.Add(1);
            return new ProcessResponse(processKey, instance.State, correlationId,
                "Повторное событие проигнорировано");
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var oldState = instance.State;
            var (newState, needCompensation) = _stateMachine.ApplyEvent(
                instance.State, request.Event, processKey);

            if (needCompensation)
            {
                _logger.LogWarning(
                    "[{CorrelationId}] Выполняется компенсация для процесса {ProcessKey}: отмена бронирования",
                    correlationId, processKey);
                _compensationCounter.Add(1);

                instance.State = ProcessState.CompensationDone;
                instance.State = ProcessState.Error;

                _logger.LogInformation(
                    "[{CorrelationId}] Компенсация выполнена, процесс {ProcessKey} переведён в Error",
                    correlationId, processKey);
                _errorCounter.Add(1);
            }
            else
            {
                instance.State = newState;
                _logger.LogInformation(
                    "[{CorrelationId}] Переход {OldState} -> {NewState} по событию {Event} для процесса {ProcessKey}",
                    correlationId, oldState, newState, request.Event, processKey);
                _successCounter.Add(1);
            }

            sw.Stop();
            _stepLatency.Record(sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("step", request.Event.ToString()));

            return new ProcessResponse(processKey, instance.State, correlationId, "Обработано");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "[{CorrelationId}] Ошибка при обработке события {Event} для процесса {ProcessKey}",
                correlationId, request.Event, processKey);
            instance.State = ProcessState.Error;
            _errorCounter.Add(1);
            _stepLatency.Record(sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("step", request.Event.ToString()));

            if (_errorCounter is { } counter)
            {
                // Можно добавить логику критической деградации
            }

            return new ProcessResponse(processKey, instance.State, correlationId,
                $"Ошибка: {ex.Message}");
        }
    }

    public ProcessState? GetState(string processKey)
    {
        if (_processes.TryGetValue(processKey, out var instance))
            return instance.State;
        return null;
    }

    private class ProcessInstance
    {
        public ProcessState State { get; set; } = ProcessState.New;
        public HashSet<string> ProcessedIdempotencyKeys { get; } = new();
    }

    // Services/ProcessService.cs
public void ForceSetState(string processKey, ProcessState state)
{
    var instance = _processes.GetOrAdd(processKey, _ => new ProcessInstance());
    instance.State = state;
}
    
}