namespace Application.DTO;

public class ItemCreateDto
{
	public string Name { get; set; } = null!;
	public int CategoryId { get; set; }
	public string Description { get; set; } = null!;
	public string Condition { get; set; } = null!;
}
