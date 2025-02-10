using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO;
using web_test;

namespace web_test
{
    public class ReportGenerator
    {
        public static byte[] GeneratePdf(List<WorkItem> data, string title, string Dep)
        {

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(7));

                    // Шапка - только на первой странице
                    page.Header()
                        .ShowOnce()
                        .Text(title + '\n' + "Подразделение: " + Dep)
                        .FontSize(15)
                        .Bold()
                        .FontColor(Colors.Blue.Darken4)
                        .AlignCenter();


                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Table(table =>
                        {

                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3); // Номер работы
                                columns.RelativeColumn(7); // Название документа (шире)
                                columns.RelativeColumn(6.5f); // Название работы (шире)
                                columns.RelativeColumn(2.2f); // Исполнитель
                                columns.RelativeColumn(2); // Контролирующий
                                columns.RelativeColumn(2.2f); // Принимающий
                                columns.RelativeColumn(1.5f); // Планонвая дата
                                columns.RelativeColumn(1.5f); // Корр1
                                columns.RelativeColumn(1.5f); // Корр2
                                columns.RelativeColumn(1.5f); // Корр3
                                columns.RelativeColumn(1.5f); // Факт
                                columns.RelativeColumn(1.5f); // Подпись
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
                                table.Cell().Element(Block).AlignCenter().Text(item.DocumentNumber);
                                table.Cell().Element(Block).Text(item.DocumentName);
                                table.Cell().Element(Block).Text(item.WorkName);
                                table.Cell().Element(Block).AlignCenter().Text(item.Executor);
                                table.Cell().Element(Block).AlignCenter().Text(item.Controller);
                                table.Cell().Element(Block).AlignCenter().Text(item.Approver);
                                table.Cell().Element(Block).AlignCenter().Text(item.PlanDate?.ToString("dd.MM.yy") ?? "");
                                table.Cell().Element(Block).AlignCenter().Text(item.Korrect1?.ToString("dd.MM.yy") ?? "");
                                table.Cell().Element(Block).AlignCenter().Text(item.Korrect2?.ToString("dd.MM.yy") ?? "");
                                table.Cell().Element(Block).AlignCenter().Text(item.Korrect3?.ToString("dd.MM.yy") ?? "");
                                table.Cell().Element(Block).AlignCenter().Text(item.FactDate?.ToString("dd.MM.yy") ?? "");
                                table.Cell().Element(Block).AlignCenter().Text("    ");
                            }
                        });
                        static IContainer Block(IContainer container)
                        {
                        return container
                            .Border(0.5f)
                            .ShowEntire() // Запрещаем разрыв содержимого
                            .MinWidth(20)
                            .MinHeight(20)
                            .AlignMiddle()
                            .PaddingHorizontal(1)
                            .PaddingLeft(2);
                        }
                    // Footer с номерами страниц
                    page.Footer()
                        .Column(column =>
                        {
                            // Номера страниц (на всех страницах)
                            column.Item().AlignCenter().Text(text =>
                            {
                                text.CurrentPageNumber();
                                text.Span(" / ");
                                text.TotalPages();
                            });

                            // Блок подписей (только на последней странице)
                            column.Item()
                                .ShowIf(ctx => ctx.PageNumber == ctx.TotalPages)
                                .PaddingTop(0)
                                .Row(row =>
                                {
                                    row.AutoItem().AlignLeft().Text("Ответственное лицо");
                                    row.RelativeItem().AlignRight().Text("Ответственное лицо ИАЦ");
                                });

                            column.Item()
                                .ShowIf(ctx => ctx.PageNumber == ctx.TotalPages)
                                .Row(row =>
                                {
                                    row.AutoItem().AlignLeft().Text("_______________________/");
                                    row.RelativeItem().AlignRight().Text("____________________________/");
                                });
                        });
                });
            })
            .GeneratePdf(); // Сохраняем файл в wwwroot/report.pdf
        }
    }
}