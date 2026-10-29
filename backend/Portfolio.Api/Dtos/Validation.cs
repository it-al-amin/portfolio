using System.ComponentModel.DataAnnotations;

namespace Portfolio.Api.Dtos;

/// <summary>Allows empty values; otherwise requires an absolute http(s) URL.</summary>
[AttributeUsage(AttributeTargets.Property)]
public class HttpUrlAttribute : ValidationAttribute
{
    public HttpUrlAttribute() : base("{0} must be a valid http:// or https:// URL.") { }

    public override bool IsValid(object? value) =>
        value is not string s || string.IsNullOrWhiteSpace(s) || IsHttpUrl(s.Trim());

    public static bool IsHttpUrl(string s) =>
        s.Length <= 500 && Uri.TryCreate(s, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

/// <summary>Limits the number of items in a string list and the length of each item.</summary>
[AttributeUsage(AttributeTargets.Property)]
public class ListItemsAttribute(int maxCount, int maxItemLength) : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not IEnumerable<string> items) return ValidationResult.Success;
        var list = items.ToList();
        if (list.Count > maxCount)
            return new ValidationResult($"{context.DisplayName} can contain at most {maxCount} items.", [context.MemberName!]);
        if (list.Any(i => i is not null && i.Length > maxItemLength))
            return new ValidationResult($"Each {context.DisplayName} item must be at most {maxItemLength} characters.", [context.MemberName!]);
        return ValidationResult.Success;
    }
}

/// <summary>Rejects carriage returns and line feeds (prevents header injection in names and subjects).</summary>
[AttributeUsage(AttributeTargets.Property)]
public class SingleLineAttribute : ValidationAttribute
{
    public SingleLineAttribute() : base("{0} must be a single line.") { }
    public override bool IsValid(object? value) => value is not string s || !s.Any(c => c is '\r' or '\n');
}

public static class Clean
{
    public static string Text(string? value) => (value ?? "").Trim();
    public static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    public static List<string> List(IEnumerable<string>? values) =>
        (values ?? []).Select(v => v?.Trim() ?? "").Where(v => v.Length > 0).ToList();

    /// <summary>Normalizes line endings and strips control characters other than newline and tab.</summary>
    public static string Multiline(string? value) =>
        new string((value ?? "").Replace("\r\n", "\n").Replace('\r', '\n')
            .Where(c => c == '\n' || c == '\t' || !char.IsControl(c)).ToArray()).Trim();
}
