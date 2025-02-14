using Monitoring.Application.Interfaces;
using Monitoring.Domain.Entities;
using Microsoft.Extensions.Configuration;
using System.Data.SqlClient;

namespace Monitoring.Infrastructure.Services
{
    public class WorkRequestService : IWorkRequestService
    {
        private readonly IConfiguration _config;

        public WorkRequestService(IConfiguration config)
        {
            _config = config;
        }

        public async Task CreateRequestAsync(WorkRequest request)
        {
            string connStr = _config.GetConnectionString("DefaultConnection");
            using var conn = new SqlConnection(connStr);
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
            string connStr = _config.GetConnectionString("DefaultConnection");
            using var conn = new SqlConnection(connStr);
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
            string connStr = _config.GetConnectionString("DefaultConnection");
            using var conn = new SqlConnection(connStr);
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
    }
}