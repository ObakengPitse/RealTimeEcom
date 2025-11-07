using Azure.Messaging.EventHubs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading.Tasks;

public class EventHubOrdersIngest
{
    private readonly string _sql;
    private readonly ILogger _log;

    public EventHubOrdersIngest(IConfiguration cfg, ILoggerFactory lf)
    {
        _sql = cfg["Sql:ConnectionString"]!;
        _log = lf.CreateLogger<EventHubOrdersIngest>();
    }

    public EventHubOrdersIngest(ILogger<EventHubOrdersIngest> log)
    {
        _log = log;
        _sql = Environment.GetEnvironmentVariable("SqlConnectionString")!;
    }

    [Function("OrdersIngest")]
    public async Task Run(
        [EventHubTrigger("%OrdersEventHub__Name%", Connection = "OrdersEventHub__Connection", IsBatched = true)]
        EventData[] events)
    {
        if (events.Length == 0) return;

        using var conn = new SqlConnection(_sql);
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        try
        {
            foreach (var ev in events)
            {
                var json = ev.EventBody.ToString();
                var order = JsonSerializer.Deserialize<OrderDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                var orderId = order.Id ?? Guid.NewGuid();

                using (var cmd = new SqlCommand(@"
MERGE dbo.OrderEvents AS tgt
USING (VALUES (@OrderId, @FullName, @Email, @Phone, @PaymentMethod, @Total, @CreatedAtUtc))
    AS src(OrderId, FullName, Email, Phone, PaymentMethod, Total, CreatedAtUtc)
ON (tgt.OrderId = src.OrderId)
WHEN MATCHED THEN UPDATE SET
    CustomerFullName = src.FullName, Email = src.Email, Phone = src.Phone,
    PaymentMethod = src.PaymentMethod, Total = src.Total
WHEN NOT MATCHED THEN INSERT (OrderId, CustomerFullName, Email, Phone, PaymentMethod, Total, CreatedAtUtc)
VALUES (src.OrderId, src.FullName, src.Email, src.Phone, src.PaymentMethod, src.Total, src.CreatedAtUtc);", conn, (SqlTransaction)tx))
                {
                    cmd.Parameters.AddWithValue("@OrderId", orderId);
                    cmd.Parameters.AddWithValue("@FullName", order.FullName ?? "");
                    cmd.Parameters.AddWithValue("@Email", (object?)order.Email ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Phone", (object?)order.Phone ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@PaymentMethod", order.PaymentMethod ?? "card");
                    cmd.Parameters.AddWithValue("@Total", order.Total);
                    cmd.Parameters.AddWithValue("@CreatedAtUtc", order.CreatedAt.UtcDateTime);
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var del = new SqlCommand("DELETE FROM dbo.OrderItems WHERE OrderId = @OrderId", conn, (SqlTransaction)tx))
                {
                    del.Parameters.AddWithValue("@OrderId", orderId);
                    await del.ExecuteNonQueryAsync();
                }

                foreach (var it in order.Items ?? new())
                {
                    using var ins = new SqlCommand(@"
INSERT INTO dbo.OrderItems (OrderId, ItemId, Name, Qty, Price)
VALUES (@OrderId, @ItemId, @Name, @Qty, @Price)", conn, (SqlTransaction)tx);
                    ins.Parameters.AddWithValue("@OrderId", orderId);
                    ins.Parameters.AddWithValue("@ItemId", it.Id);
                    ins.Parameters.AddWithValue("@Name", it.Name ?? "");
                    ins.Parameters.AddWithValue("@Qty", it.Qty);
                    ins.Parameters.AddWithValue("@Price", it.Price);
                    await ins.ExecuteNonQueryAsync();
                }
            }

            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to process batch");
            await tx.RollbackAsync();
            throw;
        }
    }

    private sealed class OrderDto
    {
        public Guid? Id { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? PaymentMethod { get; set; }
        public decimal Total { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public List<Item> Items { get; set; } = new();
        public sealed class Item { public long Id { get; set; } public string? Name { get; set; } public int Qty { get; set; } public decimal Price { get; set; } }
    }
}
