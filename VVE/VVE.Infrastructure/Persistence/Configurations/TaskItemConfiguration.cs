using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VVE.Domain;

namespace VVE.Infrastructure.Persistence.Configurations;

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("TaskItems");

        builder.HasKey(task => task.Id);
        builder.Property(task => task.Id).ValueGeneratedNever();

        builder.Property(task => task.Title)
            .HasMaxLength(TaskItem.TitleMaxLength)
            .IsRequired();

        builder.Property(task => task.IsCompleted).IsRequired();
        builder.Property(task => task.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(task => task.CompletedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);
        builder.Property(task => task.Priority)
            .HasConversion<int>()
            .IsRequired();

        // свойство для проверки конкурентности, постгра обновляет xmin при изменении строки
        builder.Property<uint>("RowVersion")
            .HasColumnName("xmin")
            .IsRowVersion();
    }
}
