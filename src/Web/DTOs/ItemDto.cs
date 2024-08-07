namespace Application.DTO;

public class ItemDto
{
	public int Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public int CategoryId { get; set; }
	public CategoryDto Category { get; set; } = new CategoryDto();
	public string Description { get; set; } = string.Empty;
	public string Condition { get; set; } = string.Empty;
}
