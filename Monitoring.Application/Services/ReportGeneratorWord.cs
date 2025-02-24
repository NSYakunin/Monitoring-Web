using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Monitoring.Domain.Entities;

namespace Monitoring.Application.Services
{
    public static class ReportGeneratorWord
    {
        public static byte[] GenerateWord(List<WorkItem> data, string title, string dep)
        {
            using (var mem = new MemoryStream())
            {
                // Создаём документ .docx
                using (WordprocessingDocument wordDocument =
                       WordprocessingDocument.Create(mem, WordprocessingDocumentType.Document, true))
                {
                    // Добавляем основную часть документа
                    MainDocumentPart mainPart = wordDocument.AddMainDocumentPart();
                    mainPart.Document = new Document();
                    Body body = new Body();

                    // --- Установка параметров секции (ориентация, размер, поля) ---
                    // A4 в ландшафте + отступы 720 (по ~2.54 см).
                    var sectionProperties = new SectionProperties(
                        new PageSize()
                        {
                            Width = (UInt32Value)16840U,
                            Height = (UInt32Value)11900U,
                            Orient = PageOrientationValues.Landscape
                        },
                        new PageMargin()
                        {
                            Top = 720,
                            Right = 720,
                            Bottom = 720,
                            Left = 720
                        }
                    );
                    body.Append(sectionProperties);

                    // --- Создаём заголовок (title) ---
                    Paragraph paragraphTitle = CreateParagraph(
                        text: title,
                        fontSizePt: 14,
                        isBold: true,
                        justification: JustificationValues.Center
                    );
                    body.Append(paragraphTitle);

                    // --- Подразделение (dep) ---
                    Paragraph paragraphDep = CreateParagraph(
                        text: "Подразделение: " + dep,
                        fontSizePt: 12,
                        isBold: true,
                        justification: JustificationValues.Center
                    );
                    body.Append(paragraphDep);

                    // --- Таблица ---
                    Table table = new Table();

                    // Свойства таблицы (рамки + фиксированная компоновка по заданным ширинам)
                    TableProperties tblProps = new TableProperties(
                        new TableBorders(
                            new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                            new LeftBorder { Val = BorderValues.Single, Size = 4 },
                            new RightBorder { Val = BorderValues.Single, Size = 4 },
                            new BottomBorder { Val = BorderValues.Single, Size = 4 },
                            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                            new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }
                        ),
                        new TableLayout { Type = TableLayoutValues.Fixed }
                    );
                    table.AppendChild(tblProps);

                    // В отчёте 13 столбцов
                    int[] columnWidths = new int[]
                    {
                        500,   // 1) №
                        1000,  // 2) Номер
                        2000,  // 3) Название документа
                        3200,  // 4) Название работы
                        1200,  // 5) Исполнитель
                        1200,  // 6) Контроль
                        1200,  // 7) Принимающий
                        900,   // 8) План
                        900,   // 9) Корр1
                        900,   // 10) Корр2
                        900,   // 11) Корр3
                        900,   // 12) Факт
                        900    // 13) Подпись
                    };

                    // Заголовки
                    string[] headers = {
                        "№","Номер","Название документа","Название работы",
                        "Исполнитель","Контроль","Принимающий",
                        "План","Корр1","Корр2","Корр3","Факт","Подпись"
                    };

                    // Строка заголовков
                    TableRow headerRow = new TableRow(
                        new TableRowProperties(new CantSplit())
                    );

                    for (int i = 0; i < headers.Length; i++)
                    {
                        // Все ячейки (включая заголовки) по центру
                        TableCell th = CreateCell(
                            text: headers[i],
                            width: columnWidths[i],
                            isBold: true,
                            justification: JustificationValues.Center
                        );
                        headerRow.Append(th);
                    }
                    table.Append(headerRow);

                    // Заполнение таблицы данными
                    int rowIndex = 1;
                    foreach (var item in data)
                    {
                        TableRow row = new TableRow(
                            new TableRowProperties(new CantSplit())
                        );

                        for (int colIndex = 0; colIndex < 13; colIndex++)
                        {
                            string cellValue = "";
                            switch (colIndex)
                            {
                                case 0:
                                    cellValue = rowIndex.ToString();
                                    break;
                                case 1:
                                    cellValue = item.DocumentNumber ?? "";
                                    break;
                                case 2:
                                    cellValue = item.DocumentName ?? "";
                                    break;
                                case 3:
                                    cellValue = item.WorkName ?? "";
                                    break;
                                case 4:
                                    cellValue = item.Executor ?? "";
                                    break;
                                case 5:
                                    cellValue = item.Controller ?? "";
                                    break;
                                case 6:
                                    cellValue = item.Approver ?? "";
                                    break;
                                case 7:
                                    cellValue = item.PlanDate?.ToString("dd.MM.yy") ?? "";
                                    break;
                                case 8:
                                    cellValue = item.Korrect1?.ToString("dd.MM.yy") ?? "";
                                    break;
                                case 9:
                                    cellValue = item.Korrect2?.ToString("dd.MM.yy") ?? "";
                                    break;
                                case 10:
                                    cellValue = item.Korrect3?.ToString("dd.MM.yy") ?? "";
                                    break;
                                case 11:
                                    cellValue = item.FactDate?.ToString("dd.MM.yy") ?? "";
                                    break;
                                case 12:
                                    cellValue = ""; // Подпись
                                    break;
                            }

                            // Всё по центру
                            TableCell cell = CreateCell(
                                text: cellValue,
                                width: columnWidths[colIndex],
                                isBold: false,
                                justification: JustificationValues.Center
                            );
                            row.Append(cell);
                        }

                        table.Append(row);
                        rowIndex++;
                    }

                    body.Append(table);

                    // --- Блок для подписей (слева и справа) ---
                    // Небольшой отступ
                    body.Append(CreateParagraph("", 8, false, JustificationValues.Left, beforeSpacing: 400));

                    // Создадим таблицу на всю ширину (100%), две ячейки
                    Table signTable = new Table();

                    var signTableProps = new TableProperties(
                        new TableWidth() { Type = TableWidthUnitValues.Pct, Width = "5000" },
                        new TableBorders(
                            new TopBorder { Val = BorderValues.None },
                            new LeftBorder { Val = BorderValues.None },
                            new RightBorder { Val = BorderValues.None },
                            new BottomBorder { Val = BorderValues.None },
                            new InsideHorizontalBorder { Val = BorderValues.None },
                            new InsideVerticalBorder { Val = BorderValues.None }
                        ),
                        new TableLayout { Type = TableLayoutValues.Autofit }
                    );
                    signTable.Append(signTableProps);

                    TableRow signRow = new TableRow(
                        new TableRowProperties(new CantSplit())
                    );

                    // Левая подпись
                    TableCell leftCell = new TableCell();
                    leftCell.Append(
                        new TableCellProperties(
                            new TableCellWidth() { Type = TableWidthUnitValues.Pct, Width = "3000" },
                            // Чтобы и в этих ячейках текст был по центру вертикально, можно добавить:
                            new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center }
                        )
                    );
                    var leftParagraph = new Paragraph(
                        new ParagraphProperties(new Justification() { Val = JustificationValues.Left }),
                        new Run(
                            new RunProperties(new FontSize() { Val = "16" }),
                            new Text("Ответственное лицо: ___________________________")
                        )
                    );
                    leftCell.Append(leftParagraph);

                    // Правая подпись
                    TableCell rightCell = new TableCell();
                    rightCell.Append(
                        new TableCellProperties(
                            new TableCellWidth() { Type = TableWidthUnitValues.Pct, Width = "3000" },
                            new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center }
                        )
                    );
                    var rightParagraph = new Paragraph(
                        new ParagraphProperties(new Justification() { Val = JustificationValues.Right }),
                        new Run(
                            new RunProperties(new FontSize() { Val = "16" }),
                            new Text("Ответственное лицо ИАЦ: __________________________")
                        )
                    );
                    rightCell.Append(rightParagraph);

                    signRow.Append(leftCell);
                    signRow.Append(rightCell);
                    signTable.Append(signRow);

                    body.Append(signTable);

                    // Привязываем body к основному документу
                    mainPart.Document.Append(body);

                    // --- Добавляем нижний колонтитул с нумерацией страниц ---
                    AddFooterWithPageNumbers(wordDocument);

                    // Сохраняем документ
                    mainPart.Document.Save();
                }

                return mem.ToArray();
            }
        }

        /// <summary>
        /// Создание параграфа с заданным текстом и настройками.
        /// fontSizePt - в пунктах (pt). В OpenXML = в half-points (&times;2).
        /// </summary>
        private static Paragraph CreateParagraph(
            string text,
            int fontSizePt,
            bool isBold,
            JustificationValues justification,
            int beforeSpacing = 200,
            int afterSpacing = 200)
        {
            Paragraph p = new Paragraph();

            var pp = new ParagraphProperties(
                new SpacingBetweenLines() { Before = beforeSpacing.ToString(), After = afterSpacing.ToString() },
                new Justification() { Val = justification }
            );
            p.Append(pp);

            Run r = new Run();
            RunProperties rp = new RunProperties();
            if (isBold)
                rp.Append(new Bold());

            rp.Append(new FontSize() { Val = (fontSizePt * 2).ToString() });
            r.Append(rp);

            r.Append(new Text(text ?? ""));
            p.Append(r);

            return p;
        }

        /// <summary>
        /// Создание ячейки таблицы с фиксированной шириной, шрифтом 8 pt (FontSize=16), указанным выравниванием (justification) 
        /// и вертикальным центрированием.
        /// </summary>
        private static TableCell CreateCell(string text, int width, bool isBold, JustificationValues justification)
        {
            TableCell cell = new TableCell();

            // Свойства ячейки
            TableCellProperties cellProps = new TableCellProperties(
                new TableCellWidth() { Type = TableWidthUnitValues.Dxa, Width = width.ToString() },
                // Вертикальное выравнивание по центру
                new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center }
            );
            cell.Append(cellProps);

            // Создаём параграф (с горизонтальным выравниванием)
            Paragraph paragraph = new Paragraph();
            paragraph.ParagraphProperties = new ParagraphProperties(
                new Justification() { Val = justification }
            );

            // Создаём run
            Run run = new Run();
            RunProperties runProps = new RunProperties();
            if (isBold)
            {
                runProps.Append(new Bold());
            }
            // 8 pt => "16" half-points
            runProps.Append(new FontSize() { Val = "16" });

            run.Append(runProps);
            run.Append(new Text(text ?? ""));
            paragraph.Append(run);

            cell.Append(paragraph);
            return cell;
        }

        /// <summary>
        /// Добавляет нижний колонтитул (FooterPart) с нумерацией страниц вида "Стр. X из Y".
        /// </summary>
        private static void AddFooterWithPageNumbers(WordprocessingDocument document)
        {
            // Создаём часть для футера
            FooterPart footerPart = document.MainDocumentPart.AddNewPart<FooterPart>();
            string footerPartId = document.MainDocumentPart.GetIdOfPart(footerPart);

            // Параграф с выравниванием по правому краю
            ParagraphProperties paragraphProperties = new ParagraphProperties(
                new Justification() { Val = JustificationValues.Right }
            );

            // Содержимое: "Стр. {PAGE} из {NUMPAGES}"
            Run run = new Run();
            RunProperties rp = new RunProperties(new FontSize() { Val = "10" }); // 8 pt
            run.Append(rp);

            run.Append(new Text("Стр. "));

            // Поле PAGE
            run.Append(new FieldChar() { FieldCharType = FieldCharValues.Begin });
            run.Append(new FieldCode(" PAGE ") { Space = SpaceProcessingModeValues.Preserve });
            run.Append(new FieldChar() { FieldCharType = FieldCharValues.Separate });
            run.Append(new Text("1"));
            run.Append(new FieldChar() { FieldCharType = FieldCharValues.End });

            run.Append(new Text(" из "));

            // Поле NUMPAGES
            run.Append(new FieldChar() { FieldCharType = FieldCharValues.Begin });
            run.Append(new FieldCode(" NUMPAGES ") { Space = SpaceProcessingModeValues.Preserve });
            run.Append(new FieldChar() { FieldCharType = FieldCharValues.Separate });
            run.Append(new Text("1"));
            run.Append(new FieldChar() { FieldCharType = FieldCharValues.End });

            // Собираем параграф
            Paragraph paragraph = new Paragraph(paragraphProperties, run);

            // Формируем сам Footer и сохраняем его в FooterPart
            Footer footer = new Footer(paragraph);
            footerPart.Footer = footer;
            footerPart.Footer.Save();

            // Добавляем ссылку на футер в SectionProperties    
            SectionProperties sectionProperties = document.MainDocumentPart.Document.Body.Elements<SectionProperties>().LastOrDefault();
            if (sectionProperties != null)
            {
                FooterReference footerReference = new FooterReference()
                {
                    Type = HeaderFooterValues.Default,
                    Id = footerPartId
                };
                sectionProperties.Append(footerReference);
            }
        }
    }
}