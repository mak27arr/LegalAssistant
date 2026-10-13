using System;

namespace LegalAssistant.Application.Common;

public interface IClock
{
    DateTime UtcNow { get; }
}
