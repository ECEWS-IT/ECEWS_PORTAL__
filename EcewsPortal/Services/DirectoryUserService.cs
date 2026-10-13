using Azure.Identity;
using EcewsPortal.Models;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace EcewsPortal.Services;

public sealed class DirectoryUserService(IConfiguration config, ILogger<DirectoryUserService> logger) : IDirectoryUserService
{
    public async Task<(IReadOnlyList<DirectoryUser> Users, string? Error)> GetUsersAsync(CancellationToken ct = default)
    {
        var tenantId = config["AzureAd:TenantId"];
        var clientId = config["AzureAd:ClientId"];
        var clientSecret = config["AzureAd:ClientSecret"];
        if (string.IsNullOrWhiteSpace(tenantId) ||
            string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(clientSecret) ||
            clientSecret.StartsWith("REPLACE_", StringComparison.OrdinalIgnoreCase))
        {
            return (
                Users: Array.Empty<DirectoryUser>(),
                Error: "Directory sync is unavailable. Staff still appear from portal sign-ins. Admin access uses Admin:AdminEmails in appsettings.");
        }

        try
        {
            var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            var graph = new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
            var results = new List<DirectoryUser>();

            var response = await graph.Users.GetAsync(request =>
            {
                request.QueryParameters.Select =
                [
                    "id", "displayName", "mail", "userPrincipalName", "jobTitle", "department", "accountEnabled"
                ];
                request.QueryParameters.Top = 100;
                request.QueryParameters.Orderby = ["displayName"];
                request.QueryParameters.Count = true;
                request.Headers.Add("ConsistencyLevel", "eventual");
            }, ct);

            while (response is not null)
            {
                foreach (var u in response.Value ?? [])
                {
                    var email = FirstEmail(u);
                    if (string.IsNullOrWhiteSpace(email) ||
                        !email.EndsWith("@ecews.org", StringComparison.OrdinalIgnoreCase))
                        continue;

                    results.Add(new DirectoryUser
                    {
                        Id = u.Id ?? email,
                        DisplayName = string.IsNullOrWhiteSpace(u.DisplayName) ? email : u.DisplayName!,
                        Email = email,
                        JobTitle = u.JobTitle,
                        Department = u.Department,
                        AccountEnabled = u.AccountEnabled ?? true
                    });
                }

                if (string.IsNullOrEmpty(response.OdataNextLink))
                    break;

                response = await graph.Users
                    .WithUrl(response.OdataNextLink)
                    .GetAsync(cancellationToken: ct);
            }

            return (Users: results.OrderBy(u => u.DisplayName).ToList(), Error: null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to list directory users from Microsoft Graph");
            var hint =
                "Could not load Active Directory users. In Entra, open this app registration → API permissions → add Application permission Microsoft Graph → User.Read.All → Grant admin consent.";
            return (Users: Array.Empty<DirectoryUser>(), Error: hint + " Details: " + ex.Message);
        }
    }

    private static string FirstEmail(User u)
    {
        if (!string.IsNullOrWhiteSpace(u.Mail))
            return u.Mail.Trim();
        if (!string.IsNullOrWhiteSpace(u.UserPrincipalName))
            return u.UserPrincipalName.Trim();
        return string.Empty;
    }
}
