using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UserService.Data;

namespace UserService.Services
{
    public class IntegrationEventSenderService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private CancellationTokenSource _wakeupCancelationTokenSource = new CancellationTokenSource();

        public IntegrationEventSenderService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
            using var scope = _scopeFactory.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<UserServiceContext>();
            dbContext.Database.EnsureCreated();
        }

        public void StartPublishingOutstandingIntegrationEvents()
        {
            _wakeupCancelationTokenSource.Cancel();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory();
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            channel.ConfirmSelect();
            IBasicProperties props = channel.CreateBasicProperties();
            props.DeliveryMode = 2;

            while (!stoppingToken.IsCancellationRequested)
            {
                await PublishOutstandingIntegrationEvents(channel, stoppingToken);
            }
            
            connection.Close();
            channel.Close();
        }

        private async Task PublishOutstandingIntegrationEvents(IModel channel, CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    using var scope = _scopeFactory.CreateScope();
                    using var dbContext = scope.ServiceProvider.GetRequiredService<UserServiceContext>();
                    var events = dbContext.IntegrationEventOutbox.OrderBy(o => o.ID).ToList();

                    foreach (var e in events)
                    {
                        var body = Encoding.UTF8.GetBytes(e.Data);
                        channel.BasicPublish(exchange: "user",
                                             routingKey: e.Event,
                                             basicProperties: null,
                                             body: body);
                        channel.WaitForConfirmsOrDie(new TimeSpan(0, 0, 5));
                        Console.WriteLine("Published: " + e.Event + " " + e.Data);

                        dbContext.Remove(e);
                        await dbContext.SaveChangesAsync();
                    }

                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_wakeupCancelationTokenSource.Token, stoppingToken);
                    try
                    {
                        await Task.Delay(Timeout.Infinite, linkedCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        if (_wakeupCancelationTokenSource.Token.IsCancellationRequested)
                        {
                            Console.WriteLine("Publish requested");
                            var tmp = _wakeupCancelationTokenSource;
                            _wakeupCancelationTokenSource = new CancellationTokenSource();
                            tmp.Dispose();
                        }
                        else if (stoppingToken.IsCancellationRequested)
                        {
                            Console.WriteLine("Shutting down.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.ToString());
            }
        }
    }
}
