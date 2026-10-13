using System.Collections.Generic;
using EcewsPortal.Models;

namespace EcewsPortal.Services;

public interface INotificationStore
{
	IReadOnlyList<PortalNotification> GetAll();

	IReadOnlyList<PortalNotification> GetUnread(string email);

	int GetUnreadCount(string email);

	void MarkAllRead(string email);
}
