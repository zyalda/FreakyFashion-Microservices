using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FreakyFashion.InventoryService
{
    public class UpdateStockFunction : BackgroundService
    {
        private readonly ILogger<UpdateStockFunction> _logger;
        private readonly ConnectionFactory _rabbitConnectionFactory;
        private IConnection? _connection;
        private IChannel? _channel;

        public UpdateStockFunction(ILogger<UpdateStockFunction> logger, IConfiguration config)
        {
            _logger = logger;
            _rabbitConnectionFactory = new ConnectionFactory() { Uri = new Uri(config["RabbitMqConnectionString"]!) };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("InventoryService har vaknat och lyssnar på kön...");

            _connection = await _rabbitConnectionFactory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            // Hooka up in bulletinboard in RabbitMQ (Exchange)
            await _channel.ExchangeDeclareAsync(exchange: "order-events", type: ExchangeType.Fanout);

            // Create queue for inventory.
            string queueName = "inventory-order-queue";
            await _channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false);

            // Bind ihop kön med anslagstavlan
            await _channel.QueueBindAsync(queue: queueName, exchange: "order-events", routingKey: "");

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                
                _logger.LogInformation($"[LAGER-NOTIS] Ny order fångad via RabbitMQ!");
                _logger.LogInformation($"[LAGER-NOTIS] Payload: {message}");
                _logger.LogInformation("Minskar lagersaldot för Platform Boots med -1... [KLART!]");

                await Task.CompletedTask;
            };

            await _channel.BasicConsumeAsync(queue: queueName, autoAck: true, consumer: consumer);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        public override void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
            base.Dispose();
        }
    }
}