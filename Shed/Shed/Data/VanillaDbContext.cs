using Microsoft.EntityFrameworkCore;
using OpensourceLab.FileStorage.Domain;
using OpensourceLab.FileStorage.Meta;

namespace Shed.Data
{
    public class VanillaDbContext : DbContext
    {
        public VanillaDbContext(DbContextOptions<VanillaDbContext> options) : base(options)
        {
        }

        public DbSet<FileItem> FileItems => Set<FileItem>();
        public DbSet<ACL> Acls => Set<ACL>();
        public DbSet<AccessKey> AccessKeys => Set<AccessKey>();
        public DbSet<UserFeatureFlag> UserFeatureFlags => Set<UserFeatureFlag>();
        public DbSet<UserHiddenSetting> UserHiddenSettings => Set<UserHiddenSetting>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<FileItem>(entity =>
            {
                entity.ToTable("FileItems");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Path).IsRequired().HasMaxLength(1024);
                entity.Property(x => x.ActualPath).IsRequired().HasMaxLength(2048);
                entity.Property(x => x.ContentType).IsRequired().HasMaxLength(256);
                entity.Property(x => x.Size).IsRequired();
                entity.Property(x => x.CreatedAt).IsRequired();
                entity.Property(x => x.UpdatedAt).IsRequired();
                entity.HasIndex(x => x.Path).IsUnique();
            });

            modelBuilder.Entity<ACL>(entity =>
            {
                entity.ToTable("Acls");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.FileItemId).IsRequired();
                entity.Property(x => x.Permissions)
                    .HasConversion<string>()
                    .HasMaxLength(64)
                    .IsRequired();
                entity.Property(x => x.CreatedAt).IsRequired();
                entity.Property(x => x.UpdatedAt).IsRequired();

                entity.OwnsOne(x => x.Owner, owner =>
                {
                    owner.Property(p => p.Type)
                        .HasColumnName("OwnerType")
                        .HasConversion<string>()
                        .HasMaxLength(64)
                        .IsRequired();
                    owner.Property(p => p.OwnerId)
                        .HasColumnName("OwnerId")
                        .HasMaxLength(256)
                        .IsRequired();
                });

                entity.HasIndex(x => x.FileItemId);
            });

            modelBuilder.Entity<AccessKey>(entity =>
            {
                entity.ToTable("AccessKeys");
                entity.HasKey(x => x.Key);
                entity.Property(x => x.Key).HasMaxLength(128).IsRequired();
                entity.Property(x => x.UserId).HasMaxLength(128).IsRequired();
                entity.Property(x => x.FilePathWildCards).HasMaxLength(4096).IsRequired();
                entity.Property(x => x.CanRead).IsRequired().HasDefaultValue(true);
                entity.Property(x => x.CanWrite).IsRequired().HasDefaultValue(true);
                entity.Property(x => x.Remark).HasMaxLength(256).IsRequired().HasDefaultValue("full access");
                entity.Property(x => x.ExpiresAt).IsRequired(false);
                entity.Property(x => x.CreatedAt).IsRequired();
                entity.Property(x => x.UpdatedAt).IsRequired();
                entity.HasIndex(x => x.UserId);
            });

            modelBuilder.Entity<UserFeatureFlag>(entity =>
            {
                entity.ToTable("UserFeatureFlags");
                entity.HasKey(x => new { x.UserId, x.Feature });
                entity.Property(x => x.UserId).HasMaxLength(128).IsRequired();
                entity.Property(x => x.Feature)
                    .HasConversion<string>()
                    .HasMaxLength(128)
                    .IsRequired();
                entity.Property(x => x.IsEnabled).IsRequired();
                entity.Property(x => x.CreatedAt).IsRequired();
                entity.Property(x => x.UpdatedAt).IsRequired();
            });

            modelBuilder.Entity<UserHiddenSetting>(entity =>
            {
                entity.ToTable("UserHiddenSettings");
                entity.HasKey(x => x.UserId);
                entity.Property(x => x.UserId).HasMaxLength(128).IsRequired();
                entity.Property(x => x.TranslatedText).HasMaxLength(1024).IsRequired();
                entity.Property(x => x.ThumbnailLogoUrl).HasMaxLength(2048).IsRequired();
                entity.Property(x => x.FontStyle).HasMaxLength(128).IsRequired();
                entity.Property(x => x.CreatedAt).IsRequired();
                entity.Property(x => x.UpdatedAt).IsRequired();
            });
        }
    }
}


