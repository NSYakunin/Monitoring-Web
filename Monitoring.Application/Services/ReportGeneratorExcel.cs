using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using Monitoring.Domain.Entities;

namespace Monitoring.Application.Services
{
    public static class ReportGeneratorExcel
    {
        public static byte[] GenerateExcel(List<WorkItem> data, string title, string dep)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Отчет");

                // 1) Общие настройки шрифта и полей при печати
                worksheet.Style.Font.FontSize = 8;
                worksheet.PageSetup.Margins.Top = 0;
                worksheet.PageSetup.Margins.Bottom = 0;
                worksheet.PageSetup.Margins.Left = 0;
                worksheet.PageSetup.Margins.Right = 0;
                worksheet.PageSetup.Margins.Header = 0;
                worksheet.PageSetup.Margins.Footer = 0;

                // 2) Заголовок
                worksheet.Cell(1, 1).Value = title;
                //var titleRange = worksheet.Range(1, 1, 1, 13);
                //titleRange.Merge();
                //titleRange.Style.Font.Bold = true;
                //titleRange.Style.Font.FontSize = 14;
                //titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                //titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                // 3) Подразделение
                worksheet.Cell(2, 1).Value = "Подразделение: " + dep;
                //var depRange = worksheet.Range(2, 1, 2, 13);
                //depRange.Merge();
                //depRange.Style.Font.Bold = true;
                //depRange.Style.Font.FontSize = 12;
                //depRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                //depRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                // 4) Начало таблицы с 4-й строки
                int headerRow = 4;

                // Заголовки
                string[] headers = {
                    "№","Номер","Название документа","Название работы",
                    "Исполнитель","Контроль","Принимающий",
                    "План","Корр1","Корр2","Корр3","Факт","Подпись"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(headerRow, i + 1).Value = headers[i];
                }

                // 5) Настраиваем большой диапазон
                //    Важно: Выключим ShrinkToFit и включим WrapText
                var bigRange = worksheet.Range(headerRow, 1, headerRow + 5000, 13);
                bigRange.Style.Alignment.WrapText = true;
                bigRange.Style.Alignment.ShrinkToFit = false;
                bigRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                bigRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                bigRange.Style.Font.FontSize = 8;

                // 6) Заполняем данные
                int currentRow = headerRow + 1;
                int rowIndex = 1;
                foreach (var item in data)
                {
                    int col = 1;
                    worksheet.Cell(currentRow, col++).Value = rowIndex;
                    worksheet.Cell(currentRow, col++).Value = item.DocumentNumber;
                    worksheet.Cell(currentRow, col++).Value = item.DocumentName;
                    worksheet.Cell(currentRow, col++).Value = item.WorkName;
                    worksheet.Cell(currentRow, col++).Value = item.Executor;
                    worksheet.Cell(currentRow, col++).Value = item.Controller;
                    worksheet.Cell(currentRow, col++).Value = item.Approver;
                    worksheet.Cell(currentRow, col++).Value = item.PlanDate?.ToString("dd.MM.yy");
                    worksheet.Cell(currentRow, col++).Value = item.Korrect1?.ToString("dd.MM.yy");
                    worksheet.Cell(currentRow, col++).Value = item.Korrect2?.ToString("dd.MM.yy");
                    worksheet.Cell(currentRow, col++).Value = item.Korrect3?.ToString("dd.MM.yy");
                    worksheet.Cell(currentRow, col++).Value = item.FactDate?.ToString("dd.MM.yy");
                    worksheet.Cell(currentRow, col++).Value = ""; // Подпись

                    currentRow++;
                    rowIndex++;
                }

                // 7) Рамки для всей таблицы
                var tableRange = worksheet.Range(headerRow, 1, currentRow - 1, 13);
                tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thick;
                tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                // 8) Стиль заголовка
                var headerRange = worksheet.Range(headerRow, 1, headerRow, 13);
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                // 9) Ширины столбцов
                worksheet.Column(1).Width = 2.7;   // №
                worksheet.Column(2).Width = 15;    // Номер
                worksheet.Column(3).Width = 53;    // Название документа
                worksheet.Column(4).Width = 66;    // Название работы
                worksheet.Column(5).Width = 13;    // Исполнитель
                worksheet.Column(6).Width = 13;    // Контроль
                worksheet.Column(7).Width = 13;    // Принимающий
                worksheet.Column(8).Width = 7.5;   // План
                worksheet.Column(9).Width = 7.5;   // Корр1
                worksheet.Column(10).Width = 7.5;  // Корр2
                worksheet.Column(11).Width = 7;    // Корр3
                worksheet.Column(12).Width = 7;    // Факт
                worksheet.Column(13).Width = 7;    // Подпись

                // 10) Подгоняем высоту строк
                //     Убедимся, что вызываем это после заполнения!
                //     Можно ещё точечно: worksheet.RangeUsed().Rows().AdjustToContents();
                worksheet.RowsUsed().AdjustToContents();


                // 11) Подписи внизу
                currentRow += 2;
                worksheet.Cell(currentRow, 1).Value = "Ответственное лицо: _____________";
                var leftSignRange = worksheet.Range(currentRow, 1, currentRow, 6);
                //leftSignRange.Merge();
                leftSignRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                leftSignRange.Style.Alignment.WrapText = false;

                worksheet.Cell(currentRow, 8).Value = "Ответственное лицо ИАЦ: _____________";
                var rightSignRange = worksheet.Range(currentRow, 8, currentRow, 13);
                //rightSignRange.Merge();
                rightSignRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                rightSignRange.Style.Alignment.WrapText = false;

                // 12) Параметры печати
                worksheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
                worksheet.PageSetup.FitToPages(1, 999);
                worksheet.PageSetup.Footer.Right.AddText("Стр. &P из &N");

                // 13) Сохраняем в байтовый массив
                using (var ms = new MemoryStream())
                {
                    workbook.SaveAs(ms);
                    return ms.ToArray();
                }
            }
        }
    }
}