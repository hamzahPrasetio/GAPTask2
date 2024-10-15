using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PostService.Data;
using PostService.Models;
using PostService.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PostService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostController : ControllerBase
    {
        private readonly PostServiceContext _context;

        public PostController(PostServiceContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Post>>> GetPost()
        {
            return await _context.Post.Include(x => x.User).ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Post>> GetUser(int id)
        {
            var post = await _context.Post.Include(x => x.User).FirstOrDefaultAsync(x => x.PostId == id);
            if (post == null)
            {
                return NotFound();
            }
            return post;
        }

        [HttpPost]
        public async Task<ActionResult<Post>> PostPost(PostDTO postdto)
        {
            Post post = new Post{
              PostId = postdto.PostId,
              Title = postdto.Title,
              Content = postdto.Content,
              UserId = postdto.UserId
            };
            var user = await _context.User.FindAsync(post.UserId);
            if (user == null)
            {
                return Conflict("User with id is not found.");
            }
            post.User = user;
            _context.Post.Add(post);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetPost", new { id = post.PostId }, post);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutPost(int id, PostDTO postdto)
        {
            Post post = new Post{
              PostId = postdto.PostId,
              Title = postdto.Title,
              Content = postdto.Content,
              UserId = postdto.UserId
            };
            var user = await _context.User.FindAsync(post.UserId);
            if (user == null)
            {
                return Conflict("User with id is not found.");
            }
            post.User = user;
            _context.Entry(post).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePost(int id)
        {
            var post = await _context.Post.FindAsync(id);
            if (post == null)
            {
                return NotFound();
            }

            _context.Post.Remove(post);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}