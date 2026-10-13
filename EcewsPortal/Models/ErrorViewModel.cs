namespace EcewsPortal.Models;

public class ErrorViewModel
{
	public string? RequestId { get; set; }

	public string? Message { get; set; }

	public string? Detail { get; set; }

	public bool ShowDetails { get; set; }

	public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
