namespace PdfSpecDiffReporter.Helpers;

public static class PageReferenceFormatter
{
    public static string Format(int startPage, int endPage)
    {
        if (startPage <= 0 && endPage <= 0)
        {
            return string.Empty;
        }

        if (startPage <= 0)
        {
            startPage = endPage;
        }

        if (endPage <= 0)
        {
            endPage = startPage;
        }

        if (endPage < startPage)
        {
            (startPage, endPage) = (endPage, startPage);
        }

        return startPage == endPage
            ? $"p.{startPage}"
            : $"pp.{startPage}-{endPage}";
    }
}
