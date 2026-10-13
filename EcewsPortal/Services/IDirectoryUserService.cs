using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcewsPortal.Models;

namespace EcewsPortal.Services;

public interface IDirectoryUserService
{
	Task<(IReadOnlyList<DirectoryUser> Users, string? Error)> GetUsersAsync(CancellationToken ct = default(CancellationToken));
}
