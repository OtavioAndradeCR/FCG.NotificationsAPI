using FCG.NotificationsAPI.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FCG.NotificationsAPI.Data.Configurations;

public class NotificationUserConfiguration : IEntityTypeConfiguration<NotificationUser>
{
    public void Configure(EntityTypeBuilder<NotificationUser> builder)
    {
        builder.ToTable("notification_users");

        builder.HasKey(u => u.Id);

        // Id vem do UsersAPI — não gerado pelo banco
        builder.Property(u => u.Id)
            .ValueGeneratedNever();

        builder.Property(u => u.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.IsAdmin)
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        // Índice no email para busca rápida
        builder.HasIndex(u => u.Email);
    }
}
