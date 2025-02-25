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
        /// <summary>
        /// Возвращает все заявки, у которых Status='Pending' AND IsDone=0.
        /// Подгружаем ВСЕ поля, чтобы можно было построить большую таблицу,
        /// аналогичную WorkItem: DocumentName, WorkName, Executor, Controller, Approver, PlanDate, Korrect1..3.
        /// </summary>
        public async Task<List<WorkRequest>> GetAllRequestsAsync()
        {
            var listRequest = new List<WorkRequest>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

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

            -- Документ + работа
            (SELECT TOP 1 td.Name + ' ' + d.Name
             FROM Documents d
             JOIN TypeDocs td ON td.id = d.idTypeDoc
             JOIN Works w ON w.idDocuments = d.id
             WHERE d.Number + '/' + CAST(w.id as VARCHAR(10)) = r.WorkDocumentNumber
            ) AS DocumentName,

            (SELECT TOP 1 w2.Name
             FROM Documents d2
             JOIN Works w2 ON w2.idDocuments = d2.id
             WHERE d2.Number + '/' + CAST(w2.id as VARCHAR(10)) = r.WorkDocumentNumber
            ) AS WorkName,

            -- Плановая дата
            (SELECT TOP 1 w3.DatePlan
             FROM Documents d3
             JOIN Works w3 ON w3.idDocuments = d3.id
             WHERE d3.Number + '/' + CAST(w3.id as VARCHAR(10)) = r.WorkDocumentNumber
            ) AS PlanDate,

            -- Korrect1..3
            (SELECT TOP 1 wu.DateKorrect1
             FROM Works w4
             JOIN WorkUser wu ON wu.idWork = w4.id
             JOIN Documents d4 ON w4.idDocuments = d4.id
             WHERE d4.Number + '/' + CAST(w4.id as VARCHAR(10)) = r.WorkDocumentNumber
               AND wu.DateFact IS NULL
            ) AS Korrect1,

            (SELECT TOP 1 wu.DateKorrect2
             FROM Works w5
             JOIN WorkUser wu ON wu.idWork = w5.id
             JOIN Documents d5 ON w5.idDocuments = d5.id
             WHERE d5.Number + '/' + CAST(w5.id as VARCHAR(10)) = r.WorkDocumentNumber
               AND wu.DateFact IS NULL
            ) AS Korrect2,

            (SELECT TOP 1 wu.DateKorrect3
             FROM Works w6
             JOIN WorkUser wu ON wu.idWork = w6.id
             JOIN Documents d6 ON w6.idDocuments = d6.id
             WHERE d6.Number + '/' + CAST(w6.id as VARCHAR(10)) = r.WorkDocumentNumber
               AND wu.DateFact IS NULL
            ) AS Korrect3,

            -- Executor (может быть несколько исполнителей)
            (
                SELECT STUFF((
                    SELECT ', ' + u2.smallName
                    FROM Works w7
                    JOIN WorkUser wu7 ON wu7.idWork = w7.id
                    JOIN Users u2 ON u2.idUser = wu7.idUser
                    JOIN Documents d7 ON w7.idDocuments = d7.id
                    WHERE d7.Number + '/' + CAST(w7.id as VARCHAR(10)) = r.WorkDocumentNumber
                      AND wu7.DateFact IS NULL
                    FOR XML PATH('')
                ), 1, 2, '')
            ) AS Executor,

            -- Controller (берём через WorkUserControl)
            (
                SELECT STUFF((
                    SELECT ', ' + u3.smallName
                    FROM Works w8
                    JOIN WorkUserControl wuc ON wuc.idWork = w8.id
                    JOIN Users u3 ON u3.idUser = wuc.idUser
                    JOIN Documents d8 ON w8.idDocuments = d8.id
                    WHERE d8.Number + '/' + CAST(w8.id as VARCHAR(10)) = r.WorkDocumentNumber
                    FOR XML PATH('')
                ), 1, 2, '')
            ) AS Controller,

            -- Approver (через WorkUserCheck)
            (
                SELECT STUFF((
                    SELECT ', ' + u4.smallName
                    FROM Works w9
                    JOIN WorkUserCheck wuch ON wuch.idWork = w9.id
                    JOIN Users u4 ON u4.idUser = wuch.idUser
                    JOIN Documents d9 ON w9.idDocuments = d9.id
                    WHERE d9.Number + '/' + CAST(w9.id as VARCHAR(10)) = r.WorkDocumentNumber
                    FOR XML PATH('')
                ), 1, 2, '')
            ) AS Approver

        FROM Requests r
        WHERE r.Status = 'Pending'
          AND r.IsDone = 0
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

                    // Новые поля
                    DocumentName = rdr["DocumentName"] as string,
                    WorkName = rdr["WorkName"] as string,
                    Executor = rdr["Executor"] as string,
                    Controller = rdr["Controller"] as string,
                    Approver = rdr["Approver"] as string,
                    PlanDate = rdr["PlanDate"] as DateTime?,
                    Korrect1 = rdr["Korrect1"] as DateTime?,
                    Korrect2 = rdr["Korrect2"] as DateTime?,
                    Korrect3 = rdr["Korrect3"] as DateTime?
                };
                listRequest.Add(wr);
            }

            return listRequest;
        }
    }
}