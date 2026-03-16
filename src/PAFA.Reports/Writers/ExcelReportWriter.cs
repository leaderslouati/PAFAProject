using ClosedXML.Excel;
using PAFA.Domain.Enums;
using PAFA.Extraction.Reports.Interfaces;

namespace PAFA.Reports.Writers;

public class ExcelReportWriter : IReportWriter
{
    public ExportFormat Format => ExportFormat.Excel;

    public Task<Stream> WriteAsync<TDto>(IEnumerable<TDto> data, CancellationToken ct = default)
    {
        var rows = data.ToList();
        var props = typeof(TDto).GetProperties();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("PAFA Report");

        // En-têtes
        for (int i = 0; i < props.Length; i++)
        {
            ws.Cell(1, i + 1).Value = props[i].Name;
            ws.Cell(1, i + 1).Style.Font.Bold = true;
        }

        // Données
        for (int r = 0; r < rows.Count; r++)
            for (int c = 0; c < props.Length; c++)
                ws.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(props[c].GetValue(rows[r]));

        ws.Columns().AdjustToContents();

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return Task.FromResult<Stream>(ms);
    }
}