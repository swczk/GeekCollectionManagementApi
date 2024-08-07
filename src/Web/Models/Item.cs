namespace Web.Models;

public partial class Item
{
	public int Id { get; set; }
	public string Name { get; set; } = null!;
	public int CategoryId { get; set; }
	public string Description { get; set; } = null!;
	public string Condition { get; set; } = null!;
	public int CollectionId { get; set; }
	public virtual Category Category { get; set; } = null!;
	public virtual Collection Collection { get; set; } = null!;
	public virtual ICollection<Photo> Photos { get; set; } = new List<Photo>();
}
