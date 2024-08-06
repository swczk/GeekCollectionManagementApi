using Microsoft.EntityFrameworkCore;

namespace Web.Models;

public partial class DBContext : DbContext
{
   private readonly IConfiguration _configuration;

   public DBContext(IConfiguration configuration)
   {
      _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
   }

   public virtual DbSet<Category> Categories { get; set; }
   public virtual DbSet<Collection> Collections { get; set; }
   public virtual DbSet<Item> Items { get; set; }
   public virtual DbSet<Photo> Photos { get; set; }
   public virtual DbSet<Share> Shares { get; set; }
   public virtual DbSet<User> Users { get; set; }

   protected override void OnConfiguring(DbContextOptionsBuilder options)
   {
      if (!options.IsConfigured)
      {
         options.UseMySql(_configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(_configuration.GetConnectionString("DefaultConnection")));
      }
   }

   protected override void OnModelCreating(ModelBuilder modelBuilder)
   {
      modelBuilder
          .UseCollation("utf8mb4_general_ci")
          .HasCharSet("utf8mb4");

      modelBuilder.Entity<Category>(entity =>
      {
         entity.HasKey(e => e.Id).HasName("PRIMARY");

         entity.Property(e => e.Id).HasColumnType("int(11)");
      });

      modelBuilder.Entity<Collection>(entity =>
      {
         entity.HasKey(e => e.Id).HasName("PRIMARY");

         entity.HasIndex(e => e.UserId, "IX_Collections_UserId");

         entity.Property(e => e.Id).HasColumnType("int(11)");
         entity.Property(e => e.UserId).HasColumnType("int(11)");

         entity.HasOne(d => d.User).WithMany(p => p.Collections).HasForeignKey(d => d.UserId);
      });

      modelBuilder.Entity<Item>(entity =>
      {
         entity.HasKey(e => e.Id).HasName("PRIMARY");

         entity.HasIndex(e => e.CategoryId, "IX_Items_CategoryId");

         entity.HasIndex(e => e.CollectionId, "IX_Items_CollectionId");

         entity.Property(e => e.Id).HasColumnType("int(11)");
         entity.Property(e => e.CategoryId).HasColumnType("int(11)");
         entity.Property(e => e.CollectionId).HasColumnType("int(11)");

         entity.HasOne(d => d.Category).WithMany(p => p.Items).HasForeignKey(d => d.CategoryId);

         entity.HasOne(d => d.Collection).WithMany(p => p.Items).HasForeignKey(d => d.CollectionId);
      });

      modelBuilder.Entity<Photo>(entity =>
      {
         entity.HasKey(e => e.Id).HasName("PRIMARY");

         entity.HasIndex(e => e.ItemId, "IX_Photos_ItemId");

         entity.Property(e => e.Id).HasColumnType("int(11)");
         entity.Property(e => e.ItemId).HasColumnType("int(11)");

         entity.HasOne(d => d.Item).WithMany(p => p.Photos).HasForeignKey(d => d.ItemId);
      });

      modelBuilder.Entity<Share>(entity =>
      {
         entity.HasKey(e => e.Id).HasName("PRIMARY");

         entity.HasIndex(e => e.CollectionId, "IX_Shares_CollectionId");

         entity.HasIndex(e => e.SharedWithUserId, "IX_Shares_SharedWithUserId");

         entity.Property(e => e.Id).HasColumnType("int(11)");
         entity.Property(e => e.CollectionId).HasColumnType("int(11)");
         entity.Property(e => e.SharedWithUserId).HasColumnType("int(11)");

         entity.HasOne(d => d.Collection).WithMany(p => p.Shares).HasForeignKey(d => d.CollectionId);

         entity.HasOne(d => d.SharedWithUser).WithMany(p => p.Shares).HasForeignKey(d => d.SharedWithUserId);
      });

      modelBuilder.Entity<User>(entity =>
      {
         entity.HasKey(e => e.Id).HasName("PRIMARY");

         entity.Property(e => e.Id).HasColumnType("int(11)");
      });

      OnModelCreatingPartial(modelBuilder);
   }

   partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
