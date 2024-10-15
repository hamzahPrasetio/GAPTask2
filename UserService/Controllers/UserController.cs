using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using UserService.Data;
using UserService.Models;
using UserService.DTOs;
using UserService.Services;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Text;

namespace UserService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly UserServiceContext _context;
        private readonly IntegrationEventSenderService _integrationEventSenderService;

        public UserController(UserServiceContext context, IntegrationEventSenderService integrationEventSenderService)
        {
            _context = context;
            _integrationEventSenderService = integrationEventSenderService;
        }

        // private void PublishToMessageQueue(string integrationEvent, string eventData)
        // {
        //     var factory = new ConnectionFactory();
        //     var connection = factory.CreateConnection();
        //     var channel = connection.CreateModel();
        //     var body = Encoding.UTF8.GetBytes(eventData);
        //     channel.BasicPublish(exchange: "user",
        //                                      routingKey: integrationEvent,
        //                                      basicProperties: null,
        //                                      body: body);
        // }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUser()
        {
            return await _context.User.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUser(int id)
        {
            var user = await _context.User.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            return user;
        }

        [HttpPost]
        public async Task<ActionResult<User>> PostUser(UserDTO userdto)
        {
            using var transaction = _context.Database.BeginTransaction();
            User user = new User{
              ID = userdto.ID,
              Name = userdto.Name,
              Mail = userdto.Mail,
              OtherData = userdto.OtherData,
              Version = 1
            };

            _context.User.Add(user);
            await _context.SaveChangesAsync();

            var integrationEventData = JsonConvert.SerializeObject(new
            {
                id = user.ID,
                name = user.Name,
                version = user.Version
            });
            _context.IntegrationEventOutbox.Add(
                new IntegrationEvent()
                {
                    Event = "user.add",
                    Data = integrationEventData
                });

            _context.SaveChanges();
            transaction.Commit();
            _integrationEventSenderService.StartPublishingOutstandingIntegrationEvents();

            return CreatedAtAction("GetUser", new { id = user.ID }, user);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, UserDTO userdto)
        {
            using var transaction = _context.Database.BeginTransaction();
            var user = await _context.User.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            user.Name = userdto.Name;
            user.Mail = userdto.Mail;
            user.OtherData = userdto.OtherData;
            user.Version += 1;

            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            var integrationEventData = JsonConvert.SerializeObject(new
            {
                id = user.ID,
                newname = user.Name,
                version = user.Version
            });
            _context.IntegrationEventOutbox.Add(
                new IntegrationEvent()
                {
                    Event = "user.update",
                    Data = integrationEventData
                });

            _context.SaveChanges();
            transaction.Commit();
            _integrationEventSenderService.StartPublishingOutstandingIntegrationEvents();

            return NoContent();
        }

        /*
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            using var transaction = _context.Database.BeginTransaction();

            var user = await _context.User.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            _context.User.Remove(user);
            await _context.SaveChangesAsync();
            
            var integrationEventData = JsonConvert.SerializeObject(new
            {
                id = user.ID
            });
            _context.IntegrationEventOutbox.Add(
                new IntegrationEvent()
                {
                    Event = "user.delete",
                    Data = integrationEventData
                });

            _context.SaveChanges();
            transaction.Commit();

            return NoContent();
        }
        */
    }
}