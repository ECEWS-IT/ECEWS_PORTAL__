using System.Collections.Generic;
using EcewsPortal.Models;

namespace EcewsPortal.Services;

public interface INewsletterArticleStore
{
	NewsletterArticle? GetBySlug(string slug);

	IReadOnlyList<NewsletterArticle> GetAll();
}
