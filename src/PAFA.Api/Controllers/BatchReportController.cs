using Microsoft.AspNetCore.Mvc;

namespace PAFA.Api.Controllers;

[ApiController]
[Route("api/batch")]
public class BatchReportController : ControllerBase
{
    private readonly ILogger<BatchReportController> _log;

    public BatchReportController(ILogger<BatchReportController> log)
    {
        _log = log;
    }
    [HttpPost("trigger")]
    public async Task<IActionResult> TriggerBatchReports([FromQuery] int year , [FromQuery] int month,
        CancellationToken ct = default)
    {
        _log.LogInformation("Déclenchement batch — {Year}-{Month:D2}", year, month);

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project ../PAFA.BatchReports -- --once",
            WorkingDirectory = Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        var process = System.Diagnostics.Process.Start(psi);
        if (process == null)
            return StatusCode(500, "Impossible de démarrer le batch.");

        await process.WaitForExitAsync(ct);

        return process.ExitCode == 0
            ? Accepted(new { message = "Batch terminé avec succès.", year, month })
            : StatusCode(500, new { message = "Batch terminé avec erreurs.", exitCode = process.ExitCode });
    }
}
