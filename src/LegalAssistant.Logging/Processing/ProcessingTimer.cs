using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Logging.Processing;

public sealed class ProcessingTimer : IProcessingTimer
{
    private readonly ILogger<ProcessingTimer> _logger;

    public ProcessingTimer(ILogger<ProcessingTimer> logger)
    {
        _logger = logger;
    }

    public async Task TimeAsync(Func<Task> action, string operationName)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await action();
        }
        finally
        {
            sw.Stop();
            _logger.LogInformation("Operation {Operation} completed in {Elapsed}ms", operationName, sw.Elapsed.TotalMilliseconds);
        }
    }

    public async Task<T> TimeAsync<T>(Func<Task<T>> action, string operationName)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return await action();
        }
        finally
        {
            sw.Stop();
            _logger.LogInformation("Operation {Operation} completed in {Elapsed}ms", operationName, sw.Elapsed.TotalMilliseconds);
        }
    }
}
