namespace PAFA.Domain.Entities;

public class DimCalendar
{
    public string ReportMonth { get; set; } = string.Empty;
    public int Year { get; set; }
    public int MonthNum { get; set; }
    public string MonthLabel { get; set; } = string.Empty;
    public string Quarter { get; set; } = string.Empty;
}