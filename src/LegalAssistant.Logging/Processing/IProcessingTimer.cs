using System;
using System.Threading.Tasks;

namespace LegalAssistant.Logging.Processing;

public interface IProcessingTimer
{
    Task TimeAsync(Func<Task> action, string operationName);

    Task<T> TimeAsync<T>(Func<Task<T>> action, string operationName);
}
