namespace Web.Models;

public partial class Share
{
    public int Id { get; set; }

    public int CollectionId { get; set; }

    public int SharedWithUserId { get; set; }

    public virtual Collection Collection { get; set; } = null!;

    public virtual User SharedWithUser { get; set; } = null!;
}
