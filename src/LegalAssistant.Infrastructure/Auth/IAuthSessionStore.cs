using LegalAssistant.Application.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LegalAssistant.Infrastructure.Auth;

public interface IAuthSessionStore : ITicketStore, IUserSessionManager;
