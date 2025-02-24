using Monitoring.Application.Interfaces;
using Monitoring.Domain.Entities;
using Microsoft.Extensions.Configuration;
using System.Data.SqlClient;
using System.Collections.Generic;

namespace Monitoring.Infrastructure.Services
{
    public class WorkRequestService : IWorkRequestService
    {
        private readonly IConfiguration _config;
        private readonly string _connectionString;

        public WorkRequestService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                        ?? throw new ArgumentNullException("Connection string not found");
        }

        public async Task CreateRequestAsync(WorkRequest request)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                INSERT INTO Requests
                    (WorkDocumentNumber, RequestType, Sender, Receiver, RequestDate,
                     ProposedDate, Status, IsDone, Note)
                VALUES
                    (@DocNum, @Type, @Sender, @Receiver, @ReqDate,
                     @PropDate, @Status, @IsDone, @Note)
            ";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@DocNum", request.WorkDocumentNumber);
            cmd.Parameters.AddWithValue("@Type", request.RequestType);
            cmd.Parameters.AddWithValue("@Sender", request.Sender);
            cmd.Parameters.AddWithValue("@Receiver", request.Receiver);
            cmd.Parameters.AddWithValue("@ReqDate", request.RequestDate);
            cmd.Parameters.AddWithValue("@PropDate", (object?)request.ProposedDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", request.Status);
            cmd.Parameters.AddWithValue("@IsDone", request.IsDone);
            cmd.Parameters.AddWithValue("@Note", (object?)request.Note ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<WorkRequest>> GetRequestsByDocumentNumberAsync(string docNumber)
        {
            var list = new List<WorkRequest>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                SELECT Id, WorkDocumentNumber, RequestType, Sender, Receiver,
                       RequestDate, ProposedDate, Status, IsDone, Note
                FROM Requests
                WHERE WorkDocumentNumber = @DocNum
            ";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@DocNum", docNumber);

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                var wr = new WorkRequest
                {
                    Id = rdr.GetInt32(rdr.GetOrdinal("Id")),
                    WorkDocumentNumber = rdr.GetString(rdr.GetOrdinal("WorkDocumentNumber")),
                    RequestType = rdr.GetString(rdr.GetOrdinal("RequestType")),
                    Sender = rdr.GetString(rdr.GetOrdinal("Sender")),
                    Receiver = rdr.GetString(rdr.GetOrdinal("Receiver")),
                    RequestDate = rdr.GetDateTime(rdr.GetOrdinal("RequestDate")),
                    ProposedDate = rdr["ProposedDate"] as DateTime?,
                    Status = rdr.GetString(rdr.GetOrdinal("Status")),
                    IsDone = rdr.GetBoolean(rdr.GetOrdinal("IsDone")),
                    Note = rdr["Note"] as string
                };
                list.Add(wr);
            }
            return list;
        }

        public async Task SetRequestStatusAsync(int requestId, string newStatus)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Если принимаем/отклоняем — заявка становится "Done"
            // (IsDone = 1)
            string sql = @"
                UPDATE Requests
                SET Status = @Status,
                    IsDone = 1
                WHERE Id = @Id
            ";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Status", newStatus);
            cmd.Parameters.AddWithValue("@Id", requestId);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Возвращает все заявки, у которых Status = 'Pending' AND IsDone = 0.
        /// Теперь в этом же запросе возвращаются DocumentName и WorkName,
        /// вычисленные через связь (Documents / Works / TypeDocs).
        /// Новый комментарий (на русском) для пояснения:
        /// Мы берем Requests.WorkDocumentNumber (d.Number + '/' + w.id),
        /// находим в таблицах Documents + Works соответствие и берём названия.
        /// </summary>
        public async Task<List<WorkRequest>> GetAllRequestsAsync()
        {
            var listRequest = new List<WorkRequest>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Старый запрос был:
            //   SELECT * FROM Requests WHERE Status = 'Pending' AND IsDone = 0
            //
            // Теперь добавляем подзапросы для DocumentName и WorkName:
            string sql = @"
                SELECT
                    r.Id,
                    r.WorkDocumentNumber,
                    r.RequestType,
                    r.Sender,
                    r.Receiver,
                    r.RequestDate,
                    r.ProposedDate,
                    r.Status,
                    r.IsDone,
                    r.Note,

                    -- Подзапрос для Наименования документа (DocumentName):
                    (SELECT TOP 1 td.Name + ' ' + d.Name
                     FROM Documents d
                     JOIN TypeDocs td ON td.id = d.idTypeDoc
                     JOIN Works w ON w.idDocuments = d.id
                     WHERE d.Number + '/' + CAST(w.id as VARCHAR(10)) = r.WorkDocumentNumber
                    ) AS DocumentName,

                    -- Подзапрос для Наименования работы (WorkName):
                    (SELECT TOP 1 w2.Name
                     FROM Documents d2
                     JOIN Works w2 ON w2.idDocuments = d2.id
                     WHERE d2.Number + '/' + CAST(w2.id as VARCHAR(10)) = r.WorkDocumentNumber
                    ) AS WorkName

                FROM Requests r
                WHERE r.Status = 'Pending' AND r.IsDone = 0
            ";

            using var cmd = new SqlCommand(sql, conn);

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                var wr = new WorkRequest
                {
                    Id = rdr.GetInt32(rdr.GetOrdinal("Id")),
                    WorkDocumentNumber = rdr.GetString(rdr.GetOrdinal("WorkDocumentNumber")),
                    RequestType = rdr.GetString(rdr.GetOrdinal("RequestType")),
                    Sender = rdr.GetString(rdr.GetOrdinal("Sender")),
                    Receiver = rdr.GetString(rdr.GetOrdinal("Receiver")),
                    RequestDate = rdr.GetDateTime(rdr.GetOrdinal("RequestDate")),
                    ProposedDate = rdr["ProposedDate"] as DateTime?,
                    Status = rdr.GetString(rdr.GetOrdinal("Status")),
                    IsDone = rdr.GetBoolean(rdr.GetOrdinal("IsDone")),
                    Note = rdr["Note"] as string,

                    // Новые поля - считываем из подзапросов:
                    DocumentName = rdr["DocumentName"] as string,
                    WorkName = rdr["WorkName"] as string
                };
                listRequest.Add(wr);
            }
            return listRequest;
        }
    }
}