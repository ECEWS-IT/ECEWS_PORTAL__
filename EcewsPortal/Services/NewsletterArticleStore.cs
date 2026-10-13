using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EcewsPortal.Models;
using Microsoft.AspNetCore.Hosting;

namespace EcewsPortal.Services;

public sealed class NewsletterArticleStore : INewsletterArticleStore
{
	private readonly Dictionary<string, NewsletterArticle> _articles;

	private readonly INewsletterPdfService _pdf;

	public NewsletterArticleStore(IWebHostEnvironment env, INewsletterPdfService pdf)
	{
		_pdf = pdf;
		_articles = new Dictionary<string, NewsletterArticle>(StringComparer.OrdinalIgnoreCase);
		string path = Path.Combine(env.ContentRootPath, "App_Data", "newsletter-articles.json");
		if (!File.Exists(path))
		{
			return;
		}
		try
		{
			string json = File.ReadAllText(path);
			Dictionary<string, NewsletterArticle> dictionary = JsonSerializer.Deserialize<Dictionary<string, NewsletterArticle>>(json, new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});
			if (dictionary == null)
			{
				return;
			}
			foreach (KeyValuePair<string, NewsletterArticle> item in dictionary)
			{
				item.Value.Slug = (string.IsNullOrWhiteSpace(item.Value.Slug) ? item.Key : item.Value.Slug);
				_articles[item.Value.Slug] = item.Value;
			}
		}
		catch
		{
		}
	}

	public NewsletterArticle? GetBySlug(string slug)
	{
		if (string.IsNullOrWhiteSpace(slug))
		{
			return null;
		}
		if (!_articles.TryGetValue(slug.Trim(), out NewsletterArticle value))
		{
			return null;
		}
		return WithLivePdf(value);
	}

	public IReadOnlyList<NewsletterArticle> GetAll()
	{
		return (from a in _articles.Values.Select(WithLivePdf)
			orderby a.Title
			select a).ToList();
	}

	private NewsletterArticle WithLivePdf(NewsletterArticle source)
	{
		return new NewsletterArticle
		{
			Slug = source.Slug,
			Title = source.Title,
			Source = source.Source,
			Summary = source.Summary,
			ImageUrl = source.ImageUrl,
			PdfPage = source.PdfPage,
			Paragraphs = source.Paragraphs,
			PdfUrl = _pdf.GetPdfUrlForPage(source.PdfPage)
		};
	}
}
