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
                    page.DefaultTextStyle(x => x.FontSize(8));

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
                                columns.RelativeColumn(6); // Название документа (шире)
                                columns.RelativeColumn(5.5f); // Название работы (шире)
                                columns.RelativeColumn(2.2f); // Исполнитель
                                columns.RelativeColumn(2); // Контролирующий
                                columns.RelativeColumn(2.5f); // Принимающий
                                columns.RelativeColumn(1.8f); // Планонвая дата
                                columns.RelativeColumn(1.8f); // Корр1
                                columns.RelativeColumn(1.8f); // Корр2
                                columns.RelativeColumn(1.8f); // Корр3
                                columns.RelativeColumn(1.8f); // Факт
                                columns.RelativeColumn(1.8f); // Подпись
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Lighten2).Element(Block).AlignCenter().Text("Номер");
                                header.Cell().Background(Colors.Grey.Lighten2).Element(Block).AlignCenter().Text("Название документа");
                                header.Cell().Background(Colors.Grey.Lighten2).Element(Block).AlignCenter().Text("Название работы");
                                header.Cell().Background(Colors.Grey.Lighten2).Element(Block).AlignCenter().Text("Исполнитель");
                                header.Cell().Background(Colors.Grey.Lighten2).Element(Block).AlignCenter().Text("Контроль");
                                header.Cell().Background(Colors.Grey.Lighten2).Element(Block).AlignCenter().Text("Принимающий");
                                header.Cell().Background(Colors.Grey.Lighten2).Element(Block).AlignCenter().Text("План");
                                header.Cell().Background(Colors.Grey.Lighten2).Element(Block).AlignCenter().Text("Корр1");
                                header.Cell().Background(Colors.Grey.Lighten2).Element(Block).AlignCenter().Text("Корр2");
                                header.Cell().Background(Colors.Grey.Lighten2).Element(Block).AlignCenter().Text("Корр3");
                                header.Cell().Background(Colors.Grey.Lighten2).Element(Block).AlignCenter().Text("Факт");
                                header.Cell().Background(Colors.Grey.Lighten2).Element(Block).AlignCenter().Text("Подпись");
                            });
                            foreach (var item in data)
                            {
                                table.Cell().Element(Block).Text(item.DocumentNumber);
                                table.Cell().Element(Block).Text(item.DocumentName);
                                table.Cell().Element(Block).Text(item.WorkName);
                                table.Cell().Element(Block).Text(item.Executor);
                                table.Cell().Element(Block).Text(item.Controller);
                                table.Cell().Element(Block).Text(item.Approver);
                                table.Cell().Element(Block).Text(item.PlanDate?.ToString("dd.MM.yyyy") ?? "");
                                table.Cell().Element(Block).Text(item.Korrect1?.ToString("dd.MM.yyyy") ?? "");
                                table.Cell().Element(Block).Text(item.Korrect2?.ToString("dd.MM.yyyy") ?? "");
                                table.Cell().Element(Block).Text(item.Korrect3?.ToString("dd.MM.yyyy") ?? "");
                                table.Cell().Element(Block).Text(item.FactDate?.ToString("dd.MM.yyyy") ?? "");
                                table.Cell().Element(Block).Text("    ");
                            }
                        });

                    static IContainer Block(IContainer container)
                    {
                        return container
                            .Border(0.5f)
                            .ShowOnce()
                            .MinWidth(20)
                            .MinHeight(20)
                            .AlignMiddle()
                            .ScaleHorizontal(1)
                            .PaddingHorizontal(1)
                            .PaddingLeft(2)
                            ;
                    }
                });
            })
            .GeneratePdf(filePath); // Сохраняем файл в wwwroot/report.pdf
        }
    }
}