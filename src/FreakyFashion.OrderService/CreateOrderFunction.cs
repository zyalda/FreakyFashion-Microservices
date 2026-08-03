using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace FreakyFashion.OrderService
{
    public class CreateOrderFunction
    {
        private readonly ILogger<CreateOrderFunction> _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly ConnectionFactory _rabbitConnectionFactory;

        public CreateOrderFunction(ILogger<CreateOrderFunction> logger, IConfiguration config)
        {
            _logger = logger;
            var cosmosOptions = new CosmosClientOptions()
            {
                HttpClientFactory = () => new System.Net.Http.HttpClient(new System.Net.Http.HttpClientHandler()
                {
                    ServerCertificateCustomValidationCallback = (httpMessageHandler, certificate, chain, sslPolicyErrors) => true
                }),
                ConnectionMode = ConnectionMode.Gateway
            };
            _cosmosClient = new CosmosClient(config["CosmosDbConnectionString"]!, cosmosOptions);
            _rabbitConnectionFactory = new ConnectionFactory() { Uri = new Uri(config["RabbitMqConnectionString"]!) };
        }

        [Function("CreateOrder")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders")] HttpRequest req)
        {
            _logger.LogInformation("Tar emot DYNAMISK order i FreakyFashion...");

            //Read JSON-string user send in HTTP-body.
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            
            var customerData = JsonSerializer.Deserialize<OrderRequest>(requestBody, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true
            });

            if (customerData == null || string.IsNullOrEmpty(customerData.CustomerId))
            {
                return new BadRequestObjectResult("Felaktig order-data. Skicka med en giltig JSON-body!");
            }

            var orderId = Guid.NewGuid().ToString();
            var orderData = new
            {
                id = orderId, // Cosmos DB demand flatcase letters - "id"
                CustomerId = customerData.CustomerId,
                TotalPrice = customerData.TotalPrice,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                Items = customerData.Items
            };

            Database db = await _cosmosClient.CreateDatabaseIfNotExistsAsync("FreakyFashionDb");
            Container container = await db.CreateContainerIfNotExistsAsync("Orders", "/id");
            await container.CreateItemAsync(orderData, new PartitionKey(orderId));
            _logger.LogInformation($"Order {orderId} saved in Cosmos DB NoSQL!");

            //PUBLISH TO RABBITMQ BROKER (Asynchronous event! V7 pattern async)
            using (var connection = await _rabbitConnectionFactory.CreateConnectionAsync())
            using (var channel = await connection.CreateChannelAsync())
            {
                //RabbitMQ has different types of Exchange-rules (Direct, Topic, Headers, Fanout).
                //Create a "Fanout = Broadcast" Exchange (Which called Topic/bulletinboard in RabbitMQ.)
                await channel.ExchangeDeclareAsync(exchange: "order-events", type: ExchangeType.Fanout);

                string messageBody = JsonSerializer.Serialize(orderData);
                var body = Encoding.UTF8.GetBytes(messageBody);

                 // In RabbitMq.Client v7 mandatory och basicProperties sends explicit!
                await channel.BasicPublishAsync(
                    exchange: "order-events", 
                    routingKey: "", 
                    mandatory: false, 
                    basicProperties: new BasicProperties(), 
                    body: body);
            }

            _logger.LogInformation("Händelsen 'OrderCreated' har skickats till RabbitMQ-brokern!");

            return new OkObjectResult(new { Message = "Order mottagen!", OrderId = orderId });
        }
    }
}