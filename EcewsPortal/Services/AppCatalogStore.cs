using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EcewsPortal.Models;
using Microsoft.AspNetCore.Hosting;

namespace EcewsPortal.Services;

public class AppCatalogStore : IAppCatalogStore
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	private readonly string _filePath;

	private readonly string _removedPath;

	private readonly object _lock = new object();

	public AppCatalogStore(IWebHostEnvironment env)
	{
		string text = Path.Combine(env.ContentRootPath, "App_Data");
		Directory.CreateDirectory(text);
		_filePath = Path.Combine(text, "apps.json");
		_removedPath = Path.Combine(text, "removed-apps.json");
		if (!File.Exists(_filePath))
		{
			File.WriteAllText(_filePath, "[]");
		}
		if (!File.Exists(_removedPath))
		{
			File.WriteAllText(_removedPath, "[]");
		}
	}

	public IReadOnlyList<AppItem> GetCustomApps()
	{
		lock (_lock)
		{
			HashSet<int> removed = ReadRemovedIds();
			return (from a in ReadAll()
				where a.IsCustom && !removed.Contains(a.Id)
				select a).ToList();
		}
	}

	public IReadOnlyDictionary<int, AppItem> GetOverrides()
	{
		lock (_lock)
		{
			return (from a in ReadAll()
				where !a.IsCustom
				group a by a.Id).ToDictionary((IGrouping<int, AppItem> g) => g.Key, (IGrouping<int, AppItem> g) => g.Last());
		}
	}

	public IReadOnlySet<int> GetRemovedIds()
	{
		lock (_lock)
		{
			return ReadRemovedIds();
		}
	}

	public AppItem AddApp(AppItem app)
	{
		lock (_lock)
		{
			List<AppItem> list = ReadAll();
			int num = (from a in list
				where a.IsCustom
				select a.Id).DefaultIfEmpty(999).Max() + 1;
			if (num < 1000)
			{
				num = 1000;
			}
			app.Id = num;
			app.IsCustom = true;
			list.Add(app);
			HashSet<int> hashSet = ReadRemovedIds();
			if (hashSet.Remove(app.Id))
			{
				WriteRemovedIds(hashSet);
			}
			WriteAll(list);
			return app;
		}
	}

	public bool UpdateApp(
		int id,
		string name,
		string? url,
		string description,
		string? logoUrl = null,
		string? visibility = null,
		IEnumerable<string>? allowedEmails = null)
	{
		lock (_lock)
		{
			List<AppItem> list = ReadAll();
			AppItem appItem = list.FirstOrDefault((AppItem a) => a.Id == id);
			string name2 = name.Trim();
			var mode = AppVisibility.IsSelected(visibility) ? AppVisibility.Selected : AppVisibility.All;
			var emails = NormalizeEmails(allowedEmails);
			if (mode != AppVisibility.Selected)
				emails = [];

			if (appItem != null && appItem.IsCustom)
			{
				appItem.Name = name2;
				appItem.Url = url;
				appItem.Description = description;
				appItem.Tag = (string.IsNullOrWhiteSpace(description) ? appItem.Tag : description);
				if (!string.IsNullOrEmpty(logoUrl))
				{
					appItem.LogoUrl = logoUrl;
				}
				appItem.IsExternal = !string.IsNullOrWhiteSpace(url);
				appItem.Visibility = mode;
				appItem.AllowedEmails = emails;
				WriteAll(list);
				return true;
			}

			var existingOverride = list.FirstOrDefault((AppItem a) => a.Id == id && !a.IsCustom);
			list.RemoveAll((AppItem a) => a.Id == id && !a.IsCustom);
			list.Add(new AppItem
			{
				Id = id,
				Name = name2,
				IsCustom = false,
				Url = url,
				Description = description,
				Tag = description,
				LogoUrl = logoUrl ?? existingOverride?.LogoUrl,
				IsExternal = !string.IsNullOrWhiteSpace(url),
				Visibility = mode,
				AllowedEmails = emails
			});
			WriteAll(list);
			return true;
		}
	}

	internal static List<string> NormalizeEmails(IEnumerable<string>? emails)
	{
		if (emails is null)
			return [];

		return emails
			.SelectMany(ParseEmailTokens)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static IEnumerable<string> ParseEmailTokens(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
			yield break;

		foreach (var part in raw.Split([',', ';', '\n', '\r', '\t', ' '], StringSplitOptions.RemoveEmptyEntries))
		{
			var email = part.Trim().Trim('<', '>', '"', '\'');
			if (email.Contains('@') && email.Length >= 5)
				yield return email;
		}
	}

	public bool DeleteApp(int id)
	{
		lock (_lock)
		{
			List<AppItem> list = ReadAll();
			int num = list.RemoveAll((AppItem a) => a.Id == id && a.IsCustom);
			if (num > 0)
			{
				list.RemoveAll((AppItem a) => a.Id == id);
				WriteAll(list);
				return true;
			}
			HashSet<int> hashSet = ReadRemovedIds();
			if (hashSet.Add(id))
			{
				list.RemoveAll((AppItem a) => a.Id == id && !a.IsCustom);
				WriteAll(list);
				WriteRemovedIds(hashSet);
				return true;
			}
			return false;
		}
	}

	private List<AppItem> ReadAll()
	{
		try
		{
			string json = File.ReadAllText(_filePath);
			List<AppItem> list = JsonSerializer.Deserialize<List<AppItem>>(json, JsonOptions) ?? new List<AppItem>();
			foreach (AppItem item in list)
			{
				if (!item.IsCustom && item.Id >= 1000)
				{
					item.IsCustom = true;
				}
			}
			return list;
		}
		catch
		{
			return new List<AppItem>();
		}
	}

	private void WriteAll(List<AppItem> apps)
	{
		File.WriteAllText(_filePath, JsonSerializer.Serialize(apps, JsonOptions));
	}

	private HashSet<int> ReadRemovedIds()
	{
		try
		{
			string json = File.ReadAllText(_removedPath);
			return JsonSerializer.Deserialize<HashSet<int>>(json, JsonOptions) ?? new HashSet<int>();
		}
		catch
		{
			return new HashSet<int>();
		}
	}

	private void WriteRemovedIds(HashSet<int> ids)
	{
		File.WriteAllText(_removedPath, JsonSerializer.Serialize(ids.OrderBy((int i) => i).ToList(), JsonOptions));
	}
}
