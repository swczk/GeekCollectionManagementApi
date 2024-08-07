namespace Application.DTO;

public class CollectionDto
{
	public int Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public int UserId { get; set; }
	public List<ItemDto> Items { get; set; } = new List<ItemDto>();
	public List<ShareDto>? Shares { get; set; }
}
