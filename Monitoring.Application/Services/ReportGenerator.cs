using Monitoring.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO;

namespace Monitoring.Application.Services
{
    /// <summary>
    /// Утилитный класс, который формирует PDF-файл (или другой отчет)
    /// на основе списка WorkItem.
    /// </summary>
    public static class ReportGenerator
    {

        public static byte[] GeneratePdf(List<WorkItem> data, string title, string Dep)
        {

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(5, Unit.Millimetre);
                    //page.MarginVertical(8);
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
                                columns.RelativeColumn(0.9f);  // Новый столбец: № (порядковый номер)
                                columns.RelativeColumn(3);    // Номер (DocumentNumber)
                                columns.RelativeColumn(7);    // Название документа (DocumentName)
                                columns.RelativeColumn(6.5f); // Название работы (WorkName)
                                columns.RelativeColumn(2.5f); // Исполнитель
                                columns.RelativeColumn(2.5f);    // Контроль
                                columns.RelativeColumn(2.5f); // Принимающий
                                columns.RelativeColumn(1.5f); // План
                                columns.RelativeColumn(1.5f); // Корр1
                                columns.RelativeColumn(1.5f); // Корр2
                                columns.RelativeColumn(1.5f); // Корр3
                                columns.RelativeColumn(1.5f); // Факт
                                columns.RelativeColumn(1.5f); // Подпись
                            });

                            table.Header(header =>
                            {
                                // 1) Новый столбец для порядкового номера
                                header.Cell().Background(Colors.Grey.Lighten2)
                                    .Element(Block)
                                    .AlignCenter()
                                    .Text("№");  // Можешь назвать "№ п/п" или "№ строки"

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

                            // Счётчик, чтобы нумеровать строки
                            int rowIndex = 1;

                            foreach (var item in data)
                            {
                                // 1) Выводим порядковый номер (счётчик)
                                table.Cell().Element(Block).AlignCenter().Text(rowIndex.ToString());

                                // 2) Старые столбцы
                                table.Cell().Element(Block).AlignCenter().Text(item.DocumentNumber);
                                table.Cell().Element(Block).Text(item.DocumentName);
                                table.Cell().Element(Block).Text(item.WorkName);
                                table.Cell().Element(Block).AlignCenter().Text(string.Join("\n", item.Executor.Split(',')));
                                table.Cell().Element(Block).AlignCenter().Text(string.Join("\n", item.Controller.Split(',')));
                                table.Cell().Element(Block).AlignCenter().Text(string.Join("\n", item.Approver.Split(',')));
                                table.Cell().Element(Block).AlignCenter().Text(item.PlanDate?.ToString("dd.MM.yy") ?? "");
                                table.Cell().Element(Block).AlignCenter().Text(item.Korrect1?.ToString("dd.MM.yy") ?? "");
                                table.Cell().Element(Block).AlignCenter().Text(item.Korrect2?.ToString("dd.MM.yy") ?? "");
                                table.Cell().Element(Block).AlignCenter().Text(item.Korrect3?.ToString("dd.MM.yy") ?? "");
                                table.Cell().Element(Block).AlignCenter().Text(item.FactDate?.ToString("dd.MM.yy") ?? "");
                                table.Cell().Element(Block).AlignCenter().Text("    ");

                                rowIndex++; // Увеличиваем счётчик
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

                            // 2) Блок подписей (только на последней странице)
                            column.Item()
                                .ShowIf(ctx => ctx.PageNumber == ctx.TotalPages)
                                //.PaddingTop(10)  // Добавим вертикальный отступ сверху
                                .Row(row =>
                                {
                                    row.AutoItem()
                                       .Element(x => x.PaddingRight(20))
                                       .Text("Ответственное лицо");

                                    row.RelativeItem()
                                       .Element(x => x.PaddingLeft(20))
                                       .AlignRight()
                                       .Text("Ответственное лицо ИАЦ");
                                });

                            column.Item()
                                .ShowIf(ctx => ctx.PageNumber == ctx.TotalPages)
                                .Row(row =>
                                {
                                    row.AutoItem()
                                       .Element(x => x.PaddingRight(20))
                                       .AlignLeft()
                                       .Text("                            ");

                                    row.RelativeItem()
                                       .Element(x => x.PaddingLeft(20))
                                       .AlignRight()
                                       .Text("                            ");
                                });

                            column.Item()
                                .ShowIf(ctx => ctx.PageNumber == ctx.TotalPages)
                                .Row(row =>
                                {
                                    row.AutoItem()
                                       .Element(x => x.PaddingRight(20))
                                       .AlignLeft()
                                       .Text("____________________________/");

                                    row.RelativeItem()
                                       .Element(x => x.PaddingLeft(20))
                                       .AlignRight()
                                       .Text("____________________________/");
                                });
                            // Номера страниц (на всех страницах) -- центр, но больше отступ снизу
                            column.Item().AlignCenter().PaddingBottom(1, Unit.Millimetre).Text(text =>
                            {
                                text.CurrentPageNumber();
                                text.Span(" / ");
                                text.TotalPages();
                            });
                        });
                });
            })
            .GeneratePdf();
        }
    }
}