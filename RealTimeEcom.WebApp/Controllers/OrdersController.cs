using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Microsoft.AspNetCore.Mvc;
using RealTimeEcom.WebApp.Models;
using System.Text.Json;

namespace RealTimeEcom.WebApp.Controllers
{
    public class OrdersController : Controller
    {
        private readonly EventHubProducerClient _producer;
        public OrdersController(EventHubProducerClient producer) => _producer = producer;

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] OrderDto order)
        {
            if (order == null || order.Items == null || order.Items.Count == 0)
                return BadRequest("Invalid order payload.");
            order.Id ??= Guid.NewGuid();
            order.CreatedAt = DateTimeOffset.UtcNow;
            var json = JsonSerializer.Serialize(order);
            using var batch = await _producer.CreateBatchAsync();
            if (!batch.TryAdd(new EventData(BinaryData.FromString(json))))
                return BadRequest("Event too large.");
            await _producer.SendAsync(batch);
            return Ok(new { order.Id });
        }

        [HttpGet]
        public IActionResult Index() => View();
    }
}
