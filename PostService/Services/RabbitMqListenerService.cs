using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using PostService.Data;
using PostService.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PostService.Services
{
  public class RabbitMqListenerService : IHostedService
  {
      private readonly IServiceProvider _serviceProvider;
      private IConnection _connection;
      private IModel _channel;
      private EventingBasicConsumer _consumer;

      public RabbitMqListenerService(IServiceProvider serviceProvider)
      {
          _serviceProvider = serviceProvider;
      }

      public Task StartAsync(CancellationToken cancellationToken)
      {
          ListenForIntegrationEvents();
          return Task.CompletedTask;
      }

      private void ListenForIntegrationEvents()
      {
          var factory = new ConnectionFactory();
          _connection = factory.CreateConnection();
          _channel = _connection.CreateModel();
          _consumer = new EventingBasicConsumer(_channel);

          _consumer.Received += async (model, ea) =>
          {
              using (var scope = _serviceProvider.CreateScope())
              {
                  var dbContext = scope.ServiceProvider.GetRequiredService<PostServiceContext>();

                  var body = ea.Body.ToArray();
                  var message = Encoding.UTF8.GetString(body);
                  Console.WriteLine(" [x] Received {0}", message);

                  var data = JObject.Parse(message);
                  var type = ea.RoutingKey;

                  if (type == "user.add")
                  {
                      if (dbContext.User.Any(a => a.ID == data["id"].Value<int>()))
                      {
                          Console.WriteLine("Ignoring old/duplicate entity");
                      }
                      else
                      {
                          dbContext.User.Add(new User()
                          {
                              ID = data["id"].Value<int>(),
                              Name = data["name"].Value<string>(),
                              Version = data["version"].Value<int>()
                          });
                          await dbContext.SaveChangesAsync();
                      }
                  }
                  else if (type == "user.update")
                  {
                      int newVersion = data["version"].Value<int>();
                      var user = dbContext.User.First(a => a.ID == data["id"].Value<int>());
                      if (user.Version >= newVersion)
                      {
                          Console.WriteLine("Ignoring old/duplicate entity");
                      }
                      else
                      {
                          user.Name = data["newname"].Value<string>();
                          user.Version = newVersion;
                          await dbContext.SaveChangesAsync();
                      }
                  }
                //   else if (type == "user.delete")
                //   {
                //       var user = dbContext.User.First(a => a.ID == data["id"].Value<int>());
                //       dbContext.User.Remove(user);
                //       await dbContext.SaveChangesAsync();
                //   }
              }
              _channel.BasicAck(ea.DeliveryTag, false);
          };

          _channel.BasicConsume(queue: "user.postservice",
                                autoAck: false,
                                consumer: _consumer);
      }

      public Task StopAsync(CancellationToken cancellationToken)
      {
          _consumer?.Model?.Close();
          _connection?.Close();
          return Task.CompletedTask;
      }
  }
}
