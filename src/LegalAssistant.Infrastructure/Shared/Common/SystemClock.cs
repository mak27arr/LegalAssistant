using System;
using LegalAssistant.Application.Common;

namespace LegalAssistant.Infrastructure.Common;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
