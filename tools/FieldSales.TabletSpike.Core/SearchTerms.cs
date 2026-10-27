using System.Globalization;
using System.Text;

namespace FieldSales.TabletSpike.Core;

public static class SearchTerms
{
    public static readonly string[] Benchmark = ["Murphy's", "MURPHY’S", "mur", "Ó Briain", "o briain",
        "Ní Mhurchú", "ni mhurchu", "Quinn’s", "quinn'", "Carey's", "Walsh", "shop", "RATHDRUM",
        "aug", "Arklow", "Dún Laoghaire", "dun lao", "Bray", "Wicklow", "no-such-place-xyz"];

    public static string Normalize(string value)
    {
        var text = new StringBuilder();
        foreach (char c in value.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                text.Append(c is '\u2019' or '\u2018' ? '\'' : char.ToLowerInvariant(c));
        return text.ToString().Normalize(NormalizationForm.FormC);
    }
}
