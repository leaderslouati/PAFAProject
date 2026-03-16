
namespace PAFA.Extraction.Commands.Export; 

public record DashboardSummaryDto(
   int TotalShippers,
   int CompliantCount,
   int NonCompliantCount,
   decimal AvgReadPerformance

);
