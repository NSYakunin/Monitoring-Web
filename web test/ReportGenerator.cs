using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO;
using web_test;

namespace web_test
{
    public class ReportGenerator
    {
        public static void GeneratePdf(List<WorkItem> data, string title)
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "report.pdf");

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape()); // Горизонтальная ориентация
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header()
                        .Text(title)
                        .FontSize(18)
                        .Bold()
                        .FontColor(Colors.Blue.Darken4)
                        .SemiBold().FontSize(18).AlignCenter();

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2); // Номер работы
                                columns.RelativeColumn(8); // Название документа (шире)
                                columns.RelativeColumn(7); // Название работы (шире)
                                columns.RelativeColumn(2); // Исполнитель
                                columns.RelativeColumn(2); // Контролирующий
                                columns.RelativeColumn(2); // Принимающий
                                columns.RelativeColumn(); // Планонвая дата
                                columns.RelativeColumn(); // Корр1
                                columns.RelativeColumn(); // Корр2
                                columns.RelativeColumn(); // Корр3
                                columns.RelativeColumn(); // Факт
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Lighten2).Text("Номер");
                                header.Cell().Background(Colors.Grey.Lighten2).Text("Название документа");
                                header.Cell().Background(Colors.Grey.Lighten2).Text("Название работы");
                                header.Cell().Background(Colors.Grey.Lighten2).Text("Исполнитель");
                                header.Cell().Background(Colors.Grey.Lighten2).Text("Контроль");
                                header.Cell().Background(Colors.Grey.Lighten2).Text("Принимающий");
                                header.Cell().Background(Colors.Grey.Lighten2).Text("План");
                                header.Cell().Background(Colors.Grey.Lighten2).Text("Корр1");
                                header.Cell().Background(Colors.Grey.Lighten2).Text("Корр2");
                                header.Cell().Background(Colors.Grey.Lighten2).Text("Корр3");
                                header.Cell().Background(Colors.Grey.Lighten2).Text("Факт");
                            });

                            foreach (var item in data)
                            {
                                table.Cell().Text(item.DocumentNumber);
                                table.Cell().Text(item.DocumentName);
                                table.Cell().Text(item.WorkName);
                                table.Cell().Text(item.Executor);
                                table.Cell().Text(item.Controller);
                                table.Cell().Text(item.Approver);
                                table.Cell().Text(item.PlanDate.Value);
                                table.Cell().Text("        ");
                                table.Cell().Text("        ");
                                table.Cell().Text("        ");
                                table.Cell().Text("        ");
                            }
                        });
                });
            })
            .GeneratePdf(filePath); // Сохраняем файл в wwwroot/report.pdf
        }
    }
}