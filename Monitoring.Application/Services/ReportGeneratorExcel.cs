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
                // Создаём лист "Report"
                var worksheet = workbook.Worksheets.Add("Report");

                // Пишем заголовок и подразделение (просто в ячейки)
                worksheet.Cell(1, 1).Value = title;
                worksheet.Cell(2, 1).Value = "Подразделение: " + dep;

                // Заголовки таблицы со строки №4 (пример)
                int headerRow = 4;
                string[] headers = {
                    "№","Номер","Название документа","Название работы",
                    "Исполнитель","Контроль","Принимающий",
                    "План","Корр1","Корр2","Корр3","Факт","Подпись"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(headerRow, i + 1).Value = headers[i];
                }

                // Заполнение таблицы данными
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
                    worksheet.Cell(currentRow, col++).Value = ""; // Подпись (пусто)

                    currentRow++;
                    rowIndex++;
                }

                // Стили: закрашиваем заголовок таблицы и делаем жирным
                var headerRange = worksheet.Range(headerRow, 1, headerRow, headers.Length);
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                headerRange.Style.Font.Bold = true;

                // Автоматическая подгонка ширины столбцов
                worksheet.Columns().AdjustToContents();

                // Возвращаем результат как массив байт
                using (var ms = new MemoryStream())
                {
                    workbook.SaveAs(ms);
                    return ms.ToArray();
                }
            }
        }
    }
}