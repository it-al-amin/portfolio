using System.Text;
using System.Text.Encodings.Web;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portfolio.Api.Data;
using Portfolio.Api.Dtos;
using Portfolio.Api.Entities;

namespace Portfolio.Api.Controllers;

/// <summary>
/// Serves the HTML shells. The public shell gets server-rendered title, description, canonical and
/// Open Graph tags from the database so search engines and link previews see real content.
/// </summary>
[ApiExplorerSettings(IgnoreApi = true)]
public class SiteController(PortfolioDbContext db, IWebHostEnvironment env, IConfiguration config) : ControllerBase
{
    [HttpGet("/")]
    public Task<IActionResult> Home() => RenderPublic(null);

    [HttpGet("/projects/{slug}")]
    public Task<IActionResult> Project(string slug) => RenderPublic(slug);

    [HttpGet("/admin")]
    [HttpGet("/admin/{**path}")]
    public IActionResult Admin()
    {
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        Response.Headers.CacheControl = "no-cache";
        return PhysicalFile(Path.Combine(env.ContentRootPath, "Shell", "admin.html"), "text/html; charset=utf-8");
    }

    [HttpGet("/robots.txt")]
    public async Task<IActionResult> Robots()
    {
        var baseUrl = await BaseUrlAsync();
        return Content($"User-agent: *\nAllow: /\nDisallow: /admin\nDisallow: /api/admin\n\nSitemap: {baseUrl}/sitemap.xml\n", "text/plain");
    }

    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Sitemap()
    {
        var baseUrl = await BaseUrlAsync();
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var profileUpdated = (await db.Profiles.AsNoTracking().FirstOrDefaultAsync())?.UpdatedAt ?? DateTime.UtcNow;
        var projects = await db.Projects.AsNoTracking().Where(p => p.IsPublished).Select(p => new { p.Slug, p.UpdatedAt }).ToListAsync();

        var urls = new List<XElement> { SitemapUrl(ns, baseUrl + "/", profileUpdated) };
        urls.AddRange(projects.Select(p => SitemapUrl(ns, $"{baseUrl}/projects/{p.Slug}", p.UpdatedAt)));
        var xml = new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement(ns + "urlset", urls));
        return Content(xml.Declaration + "\n" + xml.Root, "application/xml", Encoding.UTF8);
    }

    /// <summary>Lowest-priority catch-all: unknown API paths get a plain 404, anything else the public shell with a 404 status.</summary>
    [HttpGet("/{**path}", Order = int.MaxValue)]
    public async Task<IActionResult> NotFoundPage(string? path) =>
        path is not null && path.StartsWith("api/", StringComparison.OrdinalIgnoreCase) ? NotFound() : await RenderPublic(null, notFound: true);

    private static XElement SitemapUrl(XNamespace ns, string loc, DateTime updated) =>
        new(ns + "url", new XElement(ns + "loc", loc), new XElement(ns + "lastmod", updated.ToString("yyyy-MM-dd")));

    private async Task<IActionResult> RenderPublic(string? projectSlug, bool notFound = false)
    {
        var profile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync() ?? new Profile { FullName = "Portfolio", Headline = "Software Engineer" };
        var baseUrl = await BaseUrlAsync(profile);

        var title = profile.SeoTitle ?? $"{profile.FullName} — {profile.Headline}";
        var description = profile.SeoDescription ?? profile.Tagline ?? profile.Summary ?? $"Portfolio of {profile.FullName}";
        var path = "/";
        var ogType = "profile";
        var image = profile.ProfileImageId is { } imageId ? $"{baseUrl}{PublicProfile.MediaUrl(imageId)}" : $"{baseUrl}/img/og-default.png";
        var status = notFound ? 404 : 200;

        if (projectSlug is not null)
        {
            var project = await db.Projects.AsNoTracking().Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Slug == projectSlug && p.IsPublished);
            if (project is null)
            {
                status = 404;
            }
            else
            {
                title = $"{project.Title} — {profile.FullName}";
                description = project.Summary;
                path = $"/projects/{project.Slug}";
                ogType = "article";
                if (project.Images.OrderBy(i => i.DisplayOrder).FirstOrDefault() is { } cover)
                    image = $"{baseUrl}{PublicProfile.MediaUrl(cover.MediaId)}";
            }
        }

        if (status == 404) title = $"Page not found — {profile.FullName}";
        if (description.Length > 170) description = description[..167].TrimEnd() + "...";

        var html = await System.IO.File.ReadAllTextAsync(Path.Combine(env.ContentRootPath, "Shell", "index.html"));
        var enc = HtmlEncoder.Default;
        html = html
            .Replace("%%TITLE%%", enc.Encode(title))
            .Replace("%%DESCRIPTION%%", enc.Encode(description))
            .Replace("%%CANONICAL%%", enc.Encode(baseUrl + path))
            .Replace("%%OG_TYPE%%", ogType)
            .Replace("%%OG_IMAGE%%", enc.Encode(image))
            .Replace("%%SITE_NAME%%", enc.Encode(profile.FullName))
            .Replace("%%ROBOTS%%", status == 404 ? "noindex" : "index, follow");

        Response.Headers.CacheControl = "no-cache";
        return new ContentResult { Content = html, ContentType = "text/html; charset=utf-8", StatusCode = status };
    }

    private async Task<string> BaseUrlAsync(Profile? profile = null)
    {
        profile ??= await db.Profiles.AsNoTracking().FirstOrDefaultAsync();
        var configured = profile?.SiteUrl ?? config["Site:PublicUrl"];
        return string.IsNullOrWhiteSpace(configured) ? $"{Request.Scheme}://{Request.Host}" : configured.TrimEnd('/');
    }
}


