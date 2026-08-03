using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FreakyFashion.PaymentService
{
    public class ProcessPaymentFunction : BackgroundService
    {
        private readonly ILogger<ProcessPaymentFunction> _logger;
        private readonly ConnectionFactory _rabbitConnectionFactory;
        private IConnection? _connection;
        private IChannel? _channel;

        public ProcessPaymentFunction(ILogger<ProcessPaymentFunction> logger, IConfiguration config)
        {
            _logger = logger;
            _rabbitConnectionFactory = new ConnectionFactory() { Uri = new Uri(config["RabbitMqConnectionString"]!) };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PaymentService står redo vid kassan...");

            _connection = await _rabbitConnectionFactory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            //IDEMPOTENT: by chekc if bulletinboard order-events in RabbitMQ not exist create one to avoid crasch. 
            await _channel.ExchangeDeclareAsync(exchange: "order-events", type: ExchangeType.Fanout);

            // Create unik queue.
            string queueName = "payment-order-queue";
            await _channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false);

            // Binde payment queue with bulletinboard in RabbitMQ
            await _channel.QueueBindAsync(queue: queueName, exchange: "order-events", routingKey: "");

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                
                _logger.LogInformation($"[BETAL-NOTIS] Transaktion påbörjad!");
                _logger.LogInformation($"[BETAL-NOTIS] Drar 2499.00 SEK...");
                _logger.LogInformation($"[BETAL-NOTIS] Betalning GODKÄND för order: {message}");

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