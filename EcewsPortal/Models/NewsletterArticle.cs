namespace EcewsPortal.Models;

public sealed class NewsletterArticle
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string PdfUrl { get; set; } = string.Empty;
    public int PdfPage { get; set; }
    public List<string> Paragraphs { get; set; } = [];
}

public sealed class NewsletterArticleViewModel
{
    public NewsletterArticle Article { get; set; } = new();
    public IReadOnlyList<NewsletterArticle> Related { get; set; } = [];
}
