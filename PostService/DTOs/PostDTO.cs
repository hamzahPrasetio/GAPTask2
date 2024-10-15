using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PostService.DTOs
{
    public class PostDTO
    {
        public int PostId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public int UserId { get; set; }
    }
}
