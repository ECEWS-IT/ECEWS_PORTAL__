using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace EcewsPortal.Services;

public sealed class UserAppAssignmentStore : IUserAppAssignmentStore
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	private readonly object _lock = new object();

	private readonly string _path;

	private Dictionary<string, HashSet<int>> _map = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

	public UserAppAssignmentStore(IWebHostEnvironment env)
	{
		string text = Path.Combine(env.ContentRootPath, "App_Data");
		Directory.CreateDirectory(text);
		_path = Path.Combine(text, "user-app-assignments.json");
		Load();
	}

	public IReadOnlyCollection<int> GetAssignedAppIds(string email)
	{
		if (string.IsNullOrWhiteSpace(email))
		{
			return Array.Empty<int>();
		}
		lock (_lock)
		{
			HashSet<int> value;
			return _map.TryGetValue(email.Trim(), out value) ? value.ToArray() : Array.Empty<int>();
		}
	}

	public IReadOnlyDictionary<string, HashSet<int>> GetAllAssignments()
	{
		lock (_lock)
		{
			return _map.ToDictionary((KeyValuePair<string, HashSet<int>> kv) => kv.Key, (KeyValuePair<string, HashSet<int>> kv) => new HashSet<int>(kv.Value), StringComparer.OrdinalIgnoreCase);
		}
	}

	public void SetAssignments(string email, IEnumerable<int> appIds)
	{
		if (string.IsNullOrWhiteSpace(email))
		{
			return;
		}
		string key = email.Trim();
		HashSet<int> value = appIds.Where((int id) => id > 0).Distinct().ToHashSet();
		lock (_lock)
		{
			_map[key] = value;
			SaveUnlocked();
		}
	}

	public bool HasAnyAssignments(string email)
	{
		if (string.IsNullOrWhiteSpace(email))
		{
			return false;
		}
		lock (_lock)
		{
			return _map.ContainsKey(email.Trim());
		}
	}

	private void Load()
	{
		try
		{
			if (!File.Exists(_path))
			{
				return;
			}
			string json = File.ReadAllText(_path);
			Dictionary<string, List<int>> dictionary = JsonSerializer.Deserialize<Dictionary<string, List<int>>>(json);
			if (dictionary != null)
			{
				_map = dictionary.ToDictionary((KeyValuePair<string, List<int>> kv) => kv.Key, (KeyValuePair<string, List<int>> kv) => kv.Value.ToHashSet(), StringComparer.OrdinalIgnoreCase);
			}
		}
		catch
		{
			_map = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
		}
	}

	private void SaveUnlocked()
	{
		Dictionary<string, List<int>> value = _map.ToDictionary((KeyValuePair<string, HashSet<int>> kv) => kv.Key, (KeyValuePair<string, HashSet<int>> kv) => kv.Value.OrderBy((int x) => x).ToList(), StringComparer.OrdinalIgnoreCase);
		File.WriteAllText(_path, JsonSerializer.Serialize(value, JsonOptions));
	}
}
