using EcewsPortal.Models;

namespace EcewsPortal.Services;

public interface INewsletterPdfService
{
	NewsletterEdition? Current { get; }

	string PublicPdfUrl { get; }

	string GetPdfUrlForPage(int page);

	NewsletterEdition? Refresh(bool forceCopy = false);
}
