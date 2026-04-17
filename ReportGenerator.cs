namespace SNUS_K1;
using System.Xml.Linq;

public static class ReportGenerator
{
    private const string ReportsDir = "reports";
    private const int MaxReports = 10;

    public static void Generate(JobExecutionRecord[] records)
    {
        try
        {
            var xml = BuildReportXml(records);
            Save(xml);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Report error: {ex.Message}");
        }
    }

    private static XDocument BuildReportXml(JobExecutionRecord[] records)
    {
        var completed = records.Where(r => r.Status == "COMPLETED").ToArray();
        var aborted = records.Where(r => r.Status == "ABORT").ToArray();

        var completedByType = completed
            .GroupBy(r => r.Type)
            .Select(g => new XElement("Entry",
                new XAttribute("Type", g.Key),
                new XAttribute("Count", g.Count())));

        var avgTimeByType = completed
            .GroupBy(r => r.Type)
            .Select(g => new XElement("Entry",
                new XAttribute("Type", g.Key),
                new XAttribute("AvgMs", g.Average(r => r.ExecutionTime.TotalMilliseconds).ToString("F2"))));

        var failedByType = aborted
            .GroupBy(r => r.Type)
            .OrderBy(g => g.Key)
            .Select(g => new XElement("Entry",
                new XAttribute("Type", g.Key),
                new XAttribute("Count", g.Count())));

        return new XDocument(
            new XElement("Report",
                new XAttribute("GeneratedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                new XElement("CompletedByType", completedByType),
                new XElement("AverageExecutionTime", avgTimeByType),
                new XElement("FailedByType", failedByType)
            )
        );
    }

    private static void Save(XDocument xml)
    {
        Directory.CreateDirectory(ReportsDir);

        var existing = Directory.GetFiles(ReportsDir, "report_*.xml")
            .Select(f => new FileInfo(f))
            .OrderBy(fi => fi.CreationTime)
            .ToList();

        if (existing.Count >= MaxReports)
        {
            existing.First().Delete();
        }

        string fileName = $"report_{DateTime.Now:yyyyMMdd_HHmmss}.xml";
        string path = Path.Combine(ReportsDir, fileName);
        xml.Save(path);
    }
}