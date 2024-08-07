namespace Application.DTO;

public class ShareDto
{
   public int Id { get; set; }
   public int SharedWithUserId { get; set; }
   public UserDto User { get; set; } = new UserDto();
}
