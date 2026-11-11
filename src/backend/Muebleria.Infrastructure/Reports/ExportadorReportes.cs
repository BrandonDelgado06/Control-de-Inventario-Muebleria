using System.Globalization;
using ClosedXML.Excel;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Application.Features.Reportes;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Muebleria.Infrastructure.Reports;

/// <summary>RF-35: el mismo reporte que se ve en pantalla, exportado a PDF (QuestPDF) o a Excel (ClosedXML).</summary>
public class ExportadorReportes : IExportadorReportes
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-CR");
    private const string Verde = "#1F6F5C";

    static ExportadorReportes()
    {
        // Licencia comunitaria: gratuita para proyectos académicos y empresas pequeñas.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Pdf(ReporteDto r)
    {
        var horizontal = r.Columnas.Count > 6;
        return Document.Create(doc => doc.Page(p =>
        {
            p.Size(horizontal ? PageSizes.Letter.Landscape() : PageSizes.Letter);
            p.Margin(36);
            p.DefaultTextStyle(t => t.FontSize(9));

            p.Header().PaddingBottom(10).Column(c =>
            {
                c.Item().Text("Control de Inventario Mueblería").FontSize(9).FontColor(Colors.Grey.Darken1);
                c.Item().Text(r.Titulo).FontSize(16).Bold().FontColor(Verde);
                c.Item().Text(r.Descripcion);
                foreach (var f in r.Filtros) c.Item().Text(f).FontColor(Colors.Grey.Darken2);
            });

            p.Content().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    foreach (var col in r.Columnas)
                        if (col.Tipo == TipoColumna.Texto) cd.RelativeColumn(col.Titulo is "Artículo" or "Herramienta" or "Motivo" ? 3 : 2);
                        else cd.RelativeColumn(1.4f);
                });
                t.Header(h =>
                {
                    foreach (var col in r.Columnas)
                    {
                        var celda = h.Cell().BorderBottom(1).BorderColor(Colors.Grey.Darken2).PaddingVertical(4).PaddingHorizontal(3);
                        if (col.Tipo == TipoColumna.Numero) celda = celda.AlignRight();
                        celda.Text(col.Titulo).Bold();
                    }
                });
                var par = false;
                foreach (var fila in r.Filas)
                {
                    par = !par;
                    for (var i = 0; i < r.Columnas.Count; i++)
                    {
                        var celda = t.Cell().Background(par ? Colors.White : Colors.Grey.Lighten4).PaddingVertical(3).PaddingHorizontal(3);
                        if (r.Columnas[i].Tipo == TipoColumna.Numero) celda = celda.AlignRight();
                        celda.Text(Texto(fila[i], r.Columnas[i].Tipo));
                    }
                }
                if (r.Filas.Count == 0)
                    t.Cell().ColumnSpan((uint)r.Columnas.Count).PaddingVertical(10).AlignCenter().Text("No hay datos para los filtros elegidos.").Italic();
            });

            p.Footer().Row(row =>
            {
                row.RelativeItem().Text($"Generado el {r.Generado:dd/MM/yyyy HH:mm} por {r.GeneradoPor}").FontColor(Colors.Grey.Darken1);
                row.AutoItem().Text(x => { x.Span("Página "); x.CurrentPageNumber(); x.Span(" de "); x.TotalPages(); });
            });
        })).GeneratePdf();
    }

    public byte[] Excel(ReporteDto r)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add(r.Titulo.Length > 31 ? r.Titulo[..31] : r.Titulo);
        var fila = 1;
        hoja.Cell(fila++, 1).SetValue(r.Titulo).Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(XLColor.FromHtml(Verde));
        hoja.Cell(fila++, 1).SetValue(r.Descripcion);
        foreach (var f in r.Filtros) hoja.Cell(fila++, 1).SetValue(f);
        hoja.Cell(fila++, 1).SetValue($"Generado el {r.Generado:dd/MM/yyyy HH:mm} por {r.GeneradoPor}").Style.Font.SetFontColor(XLColor.Gray);
        fila++;

        var inicio = fila;
        for (var i = 0; i < r.Columnas.Count; i++) hoja.Cell(fila, i + 1).SetValue(r.Columnas[i].Titulo);
        foreach (var datos in r.Filas)
        {
            fila++;
            for (var i = 0; i < r.Columnas.Count; i++)
            {
                var c = hoja.Cell(fila, i + 1);
                switch (datos[i])
                {
                    case null: break;
                    case decimal d: c.SetValue(d); c.Style.NumberFormat.Format = "#,##0.##"; break;
                    case int n: c.SetValue(n); break;
                    case DateTime dt: c.SetValue(dt); c.Style.DateFormat.Format = r.Columnas[i].Tipo == TipoColumna.Fecha ? "dd/mm/yyyy" : "dd/mm/yyyy hh:mm"; break;
                    case DateOnly d0: c.SetValue(d0.ToDateTime(TimeOnly.MinValue)); c.Style.DateFormat.Format = "dd/mm/yyyy"; break;
                    default: c.SetValue(datos[i]!.ToString()); break;
                }
            }
        }
        var rango = hoja.Range(inicio, 1, Math.Max(fila, inicio), r.Columnas.Count);
        var tabla = rango.CreateTable("Reporte");
        tabla.Theme = XLTableTheme.TableStyleLight9;
        hoja.Columns().AdjustToContents(8, 60);
        hoja.SheetView.FreezeRows(inicio);

        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return ms.ToArray();
    }

    private static string Texto(object? v, TipoColumna tipo) => v switch
    {
        null => "",
        decimal d => d.ToString("#,##0.##", Es),
        int n => n.ToString("#,##0", Es),
        DateTime dt => tipo == TipoColumna.Fecha ? dt.ToString("dd/MM/yyyy") : dt.ToString("dd/MM/yyyy HH:mm"),
        DateOnly d0 => d0.ToString("dd/MM/yyyy"),
        _ => v.ToString() ?? "",
    };
}
