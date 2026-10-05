using System.Globalization;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace FieldSales.Identity.Pages.Shared;

/// <summary>
///     Renders a body string that is deliberately written as markup by the developer, while
///     keeping any runtime value out of the unencoded path.
/// </summary>
/// <remarks>
///     The body itself is trusted: it is a literal in a .cshtml file and is emitted through
///     Html.Raw so its tags render. Values that arrive at runtime are not trusted, so they are
///     passed separately as composite-format arguments, HTML-encoded here, and substituted in.
///     Interpolating a runtime value straight into the body string would bypass the encoder,
///     which is the mistake this shape exists to make hard.
/// </remarks>
public static class SafeMarkupBody
{
    public static IHtmlContent Render(string markup, IReadOnlyList<string> args, HtmlEncoder encoder)
    {
        if (args.Count == 0)
        {
            // No runtime values, so nothing to encode and no need to run composite formatting —
            // which also means a literal body may contain braces without being misread.
            return new HtmlString(markup);
        }

        object[] encoded = args.Select(arg => (object)encoder.Encode(arg ?? string.Empty)).ToArray();
        return new HtmlString(string.Format(CultureInfo.InvariantCulture, markup, encoded));
    }
}
