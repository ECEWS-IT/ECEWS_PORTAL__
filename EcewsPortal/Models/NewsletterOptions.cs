namespace EcewsPortal.Models;

public sealed class NewsletterOptions
{
    public const string SectionName = "Newsletter";

    public string FilePattern { get; set; } = "ECEWS-Rising*.pdf";

    public List<string> WatchFolders { get; set; } =
    [
        "../Newsletter",
        "wwwroot/newsletters"
    ];

    public string PublishedFileName { get; set; } = "latest.pdf";

    public string PublicPath => "/newsletters/" + PublishedFileName.TrimStart('/');
}
