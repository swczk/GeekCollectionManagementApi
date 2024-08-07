namespace Web.Models;

public partial class Category
{
	public int Id { get; set; }
	public string Name { get; set; } = null!;
	public virtual ICollection<Item> Items { get; set; } = new List<Item>();
}
