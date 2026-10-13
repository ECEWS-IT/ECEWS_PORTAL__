using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EcewsPortal.Models;
using Microsoft.AspNetCore.Hosting;

namespace EcewsPortal.Services;

public sealed class NotificationStore : INotificationStore
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	private readonly object _lock = new object();

	private readonly string _path;

	private readonly string _readsPath;

	private List<PortalNotification> _items = new List<PortalNotification>();

	private Dictionary<string, DateTimeOffset> _reads = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);

	public NotificationStore(IWebHostEnvironment env)
	{
		string text = Path.Combine(env.ContentRootPath, "App_Data");
		Directory.CreateDirectory(text);
		_path = Path.Combine(text, "notifications.json");
		_readsPath = Path.Combine(text, "notification-reads.json");
		Load();
	}

	public IReadOnlyList<PortalNotification> GetAll()
	{
		lock (_lock)
		{
			return _items.OrderByDescending((PortalNotification n) => n.CreatedAt).Select(Clone).ToList();
		}
	}

	public IReadOnlyList<PortalNotification> GetUnread(string email)
	{
		DateTimeOffset lastRead = GetLastRead(email);
		return (from n in GetAll()
			where n.CreatedAt > lastRead
			select n).ToList();
	}

	public int GetUnreadCount(string email)
	{
		return GetUnread(email).Count;
	}

	public void MarkAllRead(string email)
	{
		if (string.IsNullOrWhiteSpace(email))
		{
			return;
		}
		lock (_lock)
		{
			_reads[email.Trim()] = DateTimeOffset.UtcNow;
			SaveReadsUnlocked();
		}
	}

	private DateTimeOffset GetLastRead(string email)
	{
		if (string.IsNullOrWhiteSpace(email))
		{
			return DateTimeOffset.MinValue;
		}
		lock (_lock)
		{
			DateTimeOffset value;
			return _reads.TryGetValue(email.Trim(), out value) ? value : DateTimeOffset.MinValue;
		}
	}

	private void Load()
	{
		try
		{
			if (File.Exists(_path))
			{
				string json = File.ReadAllText(_path);
				_items = JsonSerializer.Deserialize<List<PortalNotification>>(json) ?? new List<PortalNotification>();
			}
		}
		catch
		{
			_items = new List<PortalNotification>();
		}
		try
		{
			if (File.Exists(_readsPath))
			{
				string json2 = File.ReadAllText(_readsPath);
				_reads = JsonSerializer.Deserialize<Dictionary<string, DateTimeOffset>>(json2) ?? new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
			}
		}
		catch
		{
			_reads = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
		}
	}

	private void SaveReadsUnlocked()
	{
		File.WriteAllText(_readsPath, JsonSerializer.Serialize(_reads, JsonOptions));
	}

	private static PortalNotification Clone(PortalNotification n)
	{
		return new PortalNotification
		{
			Id = n.Id,
			Title = n.Title,
			Body = n.Body,
			Source = n.Source,
			Url = n.Url,
			CreatedAt = n.CreatedAt,
			IconClass = n.IconClass
		};
	}
}
