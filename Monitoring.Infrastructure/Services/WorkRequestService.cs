using Monitoring.Application.Interfaces;
using Monitoring.Domain.Entities;
using Microsoft.Extensions.Configuration;
using System.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;

namespace Monitoring.Infrastructure.Services
{
    /// <summary>
    /// Упрощённая реализация работы с таблицей Requests,
    /// где все нужные поля (DocumentName, Executor и т.д.) хранятся непосредственно в таблице.
    /// </summary>
    public class WorkRequestService : IWorkRequestService
    {
        private readonly string _connectionString;

        public WorkRequestService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("Connection string not found");
        }

        /// <summary>
        /// Создаём новую заявку. 
        /// Вся информация (DocumentName, PlanDate и т.п.) уже подставляется при создании.
        /// </summary>
        public async Task<int> CreateRequestAsync(WorkRequest request)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                INSERT INTO [dbo].[Requests]
                (
                    WorkDocumentNumber,
                    DocumentName,
                    WorkName,
                    RequestType,
                    Sender,
                    Receiver,
                    RequestDate,
                    IsDone,
                    Note,
                    ProposedDate,
                    Status,
                    Executor,
                    Controller,
                    PlanDate,
                    Korrect1,
                    Korrect2,
                    Korrect3
                )
                OUTPUT INSERTED.Id
                VALUES
                (
                    @WorkDocumentNumber,
                    @DocumentName,
                    @WorkName,
                    @RequestType,
                    @Sender,
                    @Receiver,
                    @RequestDate,
                    @IsDone,
                    @Note,
                    @ProposedDate,
                    @Status,
                    @Executor,
                    @Controller,
                    @PlanDate,
                    @Korrect1,
                    @Korrect2,
                    @Korrect3
                )
            ";

            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@WorkDocumentNumber", request.WorkDocumentNumber);
            cmd.Parameters.AddWithValue("@DocumentName", request.DocumentName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@WorkName", request.WorkName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RequestType", request.RequestType ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Sender", request.Sender ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Receiver", request.Receiver ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RequestDate", request.RequestDate);
            cmd.Parameters.AddWithValue("@IsDone", request.IsDone);
            cmd.Parameters.AddWithValue("@Note", (object?)request.Note ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ProposedDate", (object?)request.ProposedDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", request.Status ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Executor", request.Executor ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Controller", request.Controller ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@PlanDate", (object?)request.PlanDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Korrect1", (object?)request.Korrect1 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Korrect2", (object?)request.Korrect2 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Korrect3", (object?)request.Korrect3 ?? DBNull.Value);

            var newIdObj = await cmd.ExecuteScalarAsync();
            int newId = Convert.ToInt32(newIdObj);
            return newId;
        }

        /// <summary>
        /// Получаем список всех заявок по данному DocumentNumber.
        /// Нужно, чтобы выяснить, есть ли среди них Pending (для подсветки строки).
        /// </summary>
        public async Task<List<WorkRequest>> GetRequestsByDocumentNumberAsync(string docNumber)
        {
            var result = new List<WorkRequest>();

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                SELECT 
                    Id,
                    WorkDocumentNumber,
                    DocumentName,
                    WorkName,
                    RequestType,
                    Sender,
                    Receiver,
                    RequestDate,
                    IsDone,
                    Note,
                    ProposedDate,
                    Status,
                    Executor,
                    Controller,
                    PlanDate,
                    Korrect1,
                    Korrect2,
                    Korrect3
                FROM [dbo].[Requests]
                WHERE WorkDocumentNumber = @docNumber
            ";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@docNumber", docNumber);

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                var r = new WorkRequest
                {
                    Id = rdr.GetInt32(rdr.GetOrdinal("Id")),
                    WorkDocumentNumber = rdr.GetString(rdr.GetOrdinal("WorkDocumentNumber")),
                    DocumentName = rdr["DocumentName"] as string ?? "",
                    WorkName = rdr["WorkName"] as string ?? "",
                    RequestType = rdr["RequestType"] as string ?? "",
                    Sender = rdr["Sender"] as string ?? "",
                    Receiver = rdr["Receiver"] as string ?? "",
                    RequestDate = rdr.GetDateTime(rdr.GetOrdinal("RequestDate")),
                    IsDone = rdr.GetBoolean(rdr.GetOrdinal("IsDone")),
                    Note = rdr["Note"] as string,
                    ProposedDate = rdr["ProposedDate"] as DateTime?,
                    Status = rdr["Status"] as string ?? "",
                    Executor = rdr["Executor"] as string ?? "",
                    Controller = rdr["Controller"] as string ?? "",
                    PlanDate = rdr["PlanDate"] as DateTime?,
                    Korrect1 = rdr["Korrect1"] as DateTime?,
                    Korrect2 = rdr["Korrect2"] as DateTime?,
                    Korrect3 = rdr["Korrect3"] as DateTime?,
                };
                result.Add(r);
            }

            return result;
        }

        /// <summary>
        /// Возвращает все заявки для заданного получателя (Receiver), которые ещё в статусе Pending и IsDone=0.
        /// Нужно для &laquo;Мои входящие заявки&raquo;.
        /// </summary>
        public async Task<List<WorkRequest>> GetPendingRequestsByReceiverAsync(string receiver)
        {
            var list = new List<WorkRequest>();

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                SELECT
                    Id,
                    WorkDocumentNumber,
                    DocumentName,
                    WorkName,
                    RequestType,
                    Sender,
                    Receiver,
                    RequestDate,
                    IsDone,
                    Note,
                    ProposedDate,
                    Status,
                    Executor,
                    Controller,
                    PlanDate,
                    Korrect1,
                    Korrect2,
                    Korrect3
                FROM [dbo].[Requests]
                WHERE Receiver = @receiver
                  AND Status = 'Pending'
                  AND IsDone = 0
            ";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@receiver", receiver);

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                var r = new WorkRequest
                {
                    Id = rdr.GetInt32(rdr.GetOrdinal("Id")),
                    WorkDocumentNumber = rdr.GetString(rdr.GetOrdinal("WorkDocumentNumber")),
                    DocumentName = rdr["DocumentName"] as string ?? "",
                    WorkName = rdr["WorkName"] as string ?? "",
                    RequestType = rdr["RequestType"] as string ?? "",
                    Sender = rdr["Sender"] as string ?? "",
                    Receiver = rdr["Receiver"] as string ?? "",
                    RequestDate = rdr.GetDateTime(rdr.GetOrdinal("RequestDate")),
                    IsDone = rdr.GetBoolean(rdr.GetOrdinal("IsDone")),
                    Note = rdr["Note"] as string,
                    ProposedDate = rdr["ProposedDate"] as DateTime?,
                    Status = rdr["Status"] as string ?? "",
                    Executor = rdr["Executor"] as string ?? "",
                    Controller = rdr["Controller"] as string ?? "",
                    PlanDate = rdr["PlanDate"] as DateTime?,
                    Korrect1 = rdr["Korrect1"] as DateTime?,
                    Korrect2 = rdr["Korrect2"] as DateTime?,
                    Korrect3 = rdr["Korrect3"] as DateTime?,
                };
                list.Add(r);
            }

            return list;
        }

        /// <summary>
        /// Обновить статус заявки (Accepted / Declined).
        /// При этом IsDone ставим в true (1).
        /// </summary>
        public async Task SetRequestStatusAsync(int requestId, string newStatus)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                UPDATE [dbo].[Requests]
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
        /// Обновить заявку
        /// </summary>
        public async Task UpdateRequestAsync(WorkRequest req)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
                UPDATE [dbo].[Requests]
                SET 
                    RequestType = @RequestType,
                    Receiver = @Receiver,
                    ProposedDate = @ProposedDate,
                    Note = @Note
                WHERE Id = @Id
                  AND Status = 'Pending'
            ";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@RequestType", req.RequestType ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Receiver", req.Receiver ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ProposedDate", (object?)req.ProposedDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Note", (object?)req.Note ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Id", req.Id);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Удалить заявку
        /// </summary>
        public async Task DeleteRequestAsync(int requestId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"DELETE FROM [dbo].[Requests] WHERE Id = @Id AND Status='Pending';";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", requestId);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}