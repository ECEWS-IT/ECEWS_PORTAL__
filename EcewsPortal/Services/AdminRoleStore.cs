using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace EcewsPortal.Services;

public sealed class AdminRoleStore : IAdminRoleStore
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	private readonly object _lock = new object();

	private readonly string _path;

	private Dictionary<string, PortalAdminRecord> _admins = new Dictionary<string, PortalAdminRecord>(StringComparer.OrdinalIgnoreCase);

	public AdminRoleStore(IWebHostEnvironment env)
	{
		string text = Path.Combine(env.ContentRootPath, "App_Data");
		Directory.CreateDirectory(text);
		_path = Path.Combine(text, "admin-access.json");
		Load();
	}

	public IReadOnlyList<PortalAdminRecord> GetAll()
	{
		lock (_lock)
		{
			return _admins.Values.Select(Clone).OrderBy((PortalAdminRecord a) => a.Email, StringComparer.OrdinalIgnoreCase).ToList();
		}
	}

	public PortalAdminRecord? Get(string email)
	{
		if (string.IsNullOrWhiteSpace(email))
		{
			return null;
		}
		lock (_lock)
		{
			PortalAdminRecord value;
			return _admins.TryGetValue(email.Trim(), out value) ? Clone(value) : null;
		}
	}

	public void Set(string email, IEnumerable<string> pages)
	{
		string text = email.Trim();
		HashSet<string> pages2 = pages.Where((string page) => AdminPages.All.Contains(page, StringComparer.OrdinalIgnoreCase)).ToHashSet(StringComparer.OrdinalIgnoreCase);
		lock (_lock)
		{
			_admins[text] = new PortalAdminRecord
			{
				Email = text,
				Pages = pages2,
				UpdatedAt = DateTimeOffset.UtcNow
			};
			SaveUnlocked();
		}
	}

	public bool Remove(string email)
	{
		lock (_lock)
		{
			bool flag = _admins.Remove(email.Trim());
			if (flag)
			{
				SaveUnlocked();
			}
			return flag;
		}
	}

	private void Load()
	{
		try
		{
			if (File.Exists(_path))
			{
				List<PortalAdminRecord> source = JsonSerializer.Deserialize<List<PortalAdminRecord>>(File.ReadAllText(_path), JsonOptions) ?? new List<PortalAdminRecord>();
				_admins = source.Where((PortalAdminRecord a) => !string.IsNullOrWhiteSpace(a.Email)).ToDictionary((PortalAdminRecord a) => a.Email.Trim(), Clone, StringComparer.OrdinalIgnoreCase);
			}
		}
		catch
		{
			_admins = new Dictionary<string, PortalAdminRecord>(StringComparer.OrdinalIgnoreCase);
		}
	}

	private void SaveUnlocked()
	{
		File.WriteAllText(_path, JsonSerializer.Serialize(_admins.Values, JsonOptions));
	}

	private static PortalAdminRecord Clone(PortalAdminRecord source)
	{
		return new PortalAdminRecord
		{
			Email = source.Email,
			Pages = new HashSet<string>(source.Pages ?? new HashSet<string>(), StringComparer.OrdinalIgnoreCase),
			UpdatedAt = source.UpdatedAt
		};
	}
}
