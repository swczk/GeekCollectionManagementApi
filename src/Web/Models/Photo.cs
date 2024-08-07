namespace Web.Models;

public partial class Photo
{
	public int Id { get; set; }
	public string Url { get; set; } = null!;
	public int ItemId { get; set; }
	public virtual Item Item { get; set; } = null!;
}
