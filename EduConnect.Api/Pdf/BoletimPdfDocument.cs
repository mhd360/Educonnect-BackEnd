using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using EduConnect.Api.DTOs;

namespace EduConnect.Api.Pdf;

public class BoletimPdfDocument
{
    private readonly string _nome;
    private readonly string _matricula;
    private readonly string _turma;
    private readonly DateTime _geradoEm;
    private readonly List<BoletimOfertaDto> _ofertas;

    public BoletimPdfDocument(string nome, string matricula, string turma, List<BoletimOfertaDto> ofertas)
    {
        _nome = nome;
        _matricula = matricula;
        _turma = string.IsNullOrWhiteSpace(turma) ? "-" : turma;
        _geradoEm = DateTime.Now;
        _ofertas = ofertas ?? new List<BoletimOfertaDto>();
    }

    public byte[] GeneratePdf()
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("EduConnect - Boletim").SemiBold().FontSize(16);

                    col.Item().Text($"{_nome} - {_matricula}").FontSize(11);
                    col.Item().Text($"Turma: {_turma}").FontSize(11);
                    col.Item().Text($"Gerado em: {_geradoEm:dd/MM/yyyy HH:mm}").FontSize(10).FontColor(Colors.Grey.Darken1);

                    col.Item().PaddingTop(10).LineHorizontal(1);
                });

                page.Content().PaddingTop(10).Element(BuildTabela);

                page.Footer().AlignRight().Text(x =>
                {
                    x.Span("Página ").FontColor(Colors.Grey.Darken1);
                    x.CurrentPageNumber().FontColor(Colors.Grey.Darken1);
                    x.Span(" / ").FontColor(Colors.Grey.Darken1);
                    x.TotalPages().FontColor(Colors.Grey.Darken1);
                });
            });
        });

        return doc.GeneratePdf();
    }

    private void BuildTabela(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3); // Disciplina
                columns.RelativeColumn(2); // Código
                columns.RelativeColumn(2); // Carga Horária
                columns.RelativeColumn(1); // Faltas
                columns.RelativeColumn(2); // Frequência
                columns.RelativeColumn(2); // Nota final
                columns.RelativeColumn(2); // Status
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Disciplina");
                header.Cell().Element(HeaderCell).Text("Código");
                header.Cell().Element(HeaderCell).Text("Carga Horária");
                header.Cell().Element(HeaderCell).Text("Faltas");
                header.Cell().Element(HeaderCell).Text("Frequência");
                header.Cell().Element(HeaderCell).Text("Nota final");
                header.Cell().Element(HeaderCell).Text("Status");
            });

            foreach (var o in _ofertas.OrderBy(x => x.DisciplinaNome))
            {
                var carga = $"{o.CargaHoraria} horas";
                var freq = $"{o.FrequenciaPct:0.##}%";
                var notaFinal = o.NotaFinal.HasValue ? o.NotaFinal.Value.ToString("0.##") : "-";
                var status = o.Status.ToString();

                table.Cell().Element(BodyCell).Text(o.DisciplinaNome);
                table.Cell().Element(BodyCell).Text(o.DisciplinaCodigo);
                table.Cell().Element(BodyCell).Text(carga);
                table.Cell().Element(BodyCell).Text(o.Faltas.ToString());
                table.Cell().Element(BodyCell).Text(freq);
                table.Cell().Element(BodyCell).Text(notaFinal);
                table.Cell().Element(BodyCell).Text(status);
            }

            if (_ofertas.Count == 0)
            {
                table.Cell().ColumnSpan(7).Element(BodyCell)
                    .Text("Nenhuma disciplina encontrada para este aluno.");
            }
        });
    }

    private static IContainer HeaderCell(IContainer container) =>
        container.DefaultTextStyle(x => x.SemiBold().FontColor(Colors.White))
                 .PaddingVertical(6)
                 .PaddingHorizontal(6)
                 .Background(Colors.Grey.Darken3)
                 .Border(1)
                 .BorderColor(Colors.Grey.Darken4);

    private static IContainer BodyCell(IContainer container) =>
        container.PaddingVertical(5)
                 .PaddingHorizontal(6)
                 .BorderBottom(1)
                 .BorderColor(Colors.Grey.Lighten2);
}
