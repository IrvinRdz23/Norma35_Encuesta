using Microsoft.AspNetCore.Html;

namespace MahleSurvey;

/// <summary>Íconos SVG en línea. Usar en Razor: @Icons.Printer</summary>
public static class Icons
{
    private static HtmlString Svg(string body, string css = "") =>
        new($"<svg class=\"ico {css}\" xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" aria-hidden=\"true\">{body}</svg>");

    public static HtmlString ArrowLeft => Svg("<path d=\"M19 12H5M12 19l-7-7 7-7\"/>");
    public static HtmlString Printer => Svg("<path d=\"M6 9V3h12v6M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2\"/><rect x=\"6\" y=\"14\" width=\"12\" height=\"8\"/>");
    public static HtmlString Download => Svg("<path d=\"M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3\"/>");
    public static HtmlString Upload => Svg("<path d=\"M12 16V4m0 0L8 8m4-4 4 4M4 16v3a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-3\"/>");
    public static HtmlString User => Svg("<circle cx=\"12\" cy=\"8\" r=\"4\"/><path d=\"M4 21a8 8 0 0 1 16 0\"/>");
    public static HtmlString CheckCircle => Svg("<circle cx=\"12\" cy=\"12\" r=\"10\"/><path d=\"M8 12.5l3 3 5-6\"/>", "done-icon");
    public static HtmlString CheckCircleSmall => Svg("<circle cx=\"12\" cy=\"12\" r=\"10\"/><path d=\"M8 12.5l3 3 5-6\"/>", "done-icon sm");
}
