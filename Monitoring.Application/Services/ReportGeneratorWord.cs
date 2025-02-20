using System;
using System.Collections.Generic;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Monitoring.Domain.Entities;

namespace Monitoring.Application.Services
{
    public static class ReportGeneratorWord
    {
        public static byte[] GenerateWord(List<WorkItem> data, string title, string dep)
        {
            using (MemoryStream mem = new MemoryStream())
            {
                using (WordprocessingDocument wordDocument =
                    WordprocessingDocument.Create(mem, WordprocessingDocumentType.Document, true))
                {
                    MainDocumentPart mainPart = wordDocument.AddMainDocumentPart();
                    mainPart.Document = new Document();
                    var body = new Body();

                    // Заголовок
                    Paragraph titleParagraph = new Paragraph(new Run(new Text(title + "\nПодразделение: " + dep)));
                    var titleProps = new ParagraphProperties();
                    titleProps.Justification = new Justification() { Val = JustificationValues.Center };
                    titleParagraph.PrependChild(titleProps);
                    body.Append(titleParagraph);

                    // Создаём таблицу
                    Table table = new Table();

                    // Свойства таблицы (рамки)
                    TableProperties tblProps = new TableProperties(
                        new TableBorders(
                            new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                            new LeftBorder { Val = BorderValues.Single, Size = 4 },
                            new RightBorder { Val = BorderValues.Single, Size = 4 },
                            new BottomBorder { Val = BorderValues.Single, Size = 4 },
                            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                            new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }
                        )
                    );
                    table.AppendChild(tblProps);

                    // Заголовок таблицы (первая строка)
                    TableRow headerRow = new TableRow();
                    string[] headers = {
                        "№","Номер","Название документа","Название работы",
                        "Исполнитель","Контроль","Принимающий",
                        "План","Корр1","Корр2","Корр3","Факт","Подпись"
                    };
                    foreach (var h in headers)
                    {
                        TableCell th = new TableCell(new Paragraph(new Run(new Text(h))));
                        headerRow.Append(th);
                    }
                    table.Append(headerRow);

                    // Данные
                    int rowIndex = 1;
                    foreach (var item in data)
                    {
                        TableRow row = new TableRow();

                        // №
                        row.Append(new TableCell(new Paragraph(new Run(new Text(rowIndex.ToString())))));
                        // Номер
                        row.Append(new TableCell(new Paragraph(new Run(new Text(item.DocumentNumber ?? "")))));
                        // Название документа
                        row.Append(new TableCell(new Paragraph(new Run(new Text(item.DocumentName ?? "")))));
                        // Название работы
                        row.Append(new TableCell(new Paragraph(new Run(new Text(item.WorkName ?? "")))));
                        // Исполнитель
                        row.Append(new TableCell(new Paragraph(new Run(new Text(item.Executor ?? "")))));
                        // Контроль
                        row.Append(new TableCell(new Paragraph(new Run(new Text(item.Controller ?? "")))));
                        // Принимающий
                        row.Append(new TableCell(new Paragraph(new Run(new Text(item.Approver ?? "")))));
                        // План
                        row.Append(new TableCell(new Paragraph(new Run(new Text(
                            item.PlanDate?.ToString("dd.MM.yy") ?? "")))));
                        // Корр1
                        row.Append(new TableCell(new Paragraph(new Run(new Text(
                            item.Korrect1?.ToString("dd.MM.yy") ?? "")))));
                        // Корр2
                        row.Append(new TableCell(new Paragraph(new Run(new Text(
                            item.Korrect2?.ToString("dd.MM.yy") ?? "")))));
                        // Корр3
                        row.Append(new TableCell(new Paragraph(new Run(new Text(
                            item.Korrect3?.ToString("dd.MM.yy") ?? "")))));
                        // Факт
                        row.Append(new TableCell(new Paragraph(new Run(new Text(
                            item.FactDate?.ToString("dd.MM.yy") ?? "")))));
                        // Подпись
                        row.Append(new TableCell(new Paragraph(new Run(new Text("    ")))));

                        table.Append(row);
                        rowIndex++;
                    }

                    body.Append(table);
                    mainPart.Document.Append(body);
                    mainPart.Document.Save();
                }

                return mem.ToArray();
            }
        }
    }
}