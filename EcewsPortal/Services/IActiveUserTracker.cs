using System.Security.Claims;
using EcewsPortal.Models;

namespace EcewsPortal.Services;

public interface IActiveUserTracker
{
    TimeSpan IdleTimeout { get; }
    TimeSpan IdleWarnAfter { get; }
    void Touch(ClaimsPrincipal user, HttpContext httpContext, bool isNewSignIn = false);
    void Remove(ClaimsPrincipal user);
    void RemoveByEmail(string email);
    bool ForceSignOut(string email);
    bool IsRevoked(string email);
    void ClearRevocation(string email);
    IReadOnlyList<ActiveUserSession> GetActiveUsers();
    ActiveUsersSnapshot GetSnapshot();
    IReadOnlyList<SignInHistoryEntry> GetHistory(int take = 100);
}
