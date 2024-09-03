namespace Application.DTO;

public class ItemUpdateDto
{
	public int Id { get; set; }
	public string Name { get; set; } = null!;
	public int CategoryId { get; set; }
	public string Description { get; set; } = null!;
	public string Condition { get; set; } = null!;
}
