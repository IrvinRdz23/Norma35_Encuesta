using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MahleSurvey.Services;

/// <summary>
/// Unifica los formatos históricos de respuesta (texto "Siempre", enteros 1-7, "question[12]") en etiquetas legibles.
/// </summary>
public static class AnswerLabels
{
    public static readonly string[] Order = ["Siempre", "Casi siempre", "Algunas veces", "Casi nunca", "Nunca", "Sí", "No"];
    private static readonly string[] Likert = Order[..5];

    private static string Fold(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in d)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return Regex.Replace(sb.ToString().Trim().ToLowerInvariant(), @"\s+", " ");
    }

    public static string? Label(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = Fold(raw);
        if (int.TryParse(t, out var n))
            return n is >= 1 and <= 5 ? Likert[n - 1] : n == 6 ? "Sí" : n == 7 ? "No" : null;
        switch (t)
        {
            case "siempre": return "Siempre";
            case "casi siempre": return "Casi siempre";
            case "algunas veces": return "Algunas veces";
            case "casi nunca": return "Casi nunca";
            case "nunca": return "Nunca";
        }
        if (t.StartsWith("si")) return "Sí";
        if (t.StartsWith("no")) return "No";
        return null;
    }

    /// <summary>Código numérico histórico (1-5 escala, 6 Sí, 7 No). 0 = sin respuesta / no reconocida.</summary>
    public static int Code(string? raw, out bool skipRest)
    {
        skipRest = false;
        var label = Label(raw);
        if (label is null) return 0;
        var t = Fold(raw!);
        skipRest = t.Contains("pasa a la siguiente") || t.Contains("has concluido");
        return label switch
        {
            "Siempre" => 1, "Casi siempre" => 2, "Algunas veces" => 3, "Casi nunca" => 4, "Nunca" => 5, "Sí" => 6, _ => 7
        };
    }

    public static int? QuestionNumber(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var m = Regex.Match(raw, @"\d+");
        return m.Success && int.TryParse(m.Value, out var n) ? n : null;
    }
}
