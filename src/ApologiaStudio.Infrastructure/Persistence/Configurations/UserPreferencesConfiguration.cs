using ApologiaStudio.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApologiaStudio.Infrastructure.Persistence.Configurations;

internal sealed class UserPreferencesConfiguration
    : IEntityTypeConfiguration<UserPreferences>
{
    public void Configure(
        EntityTypeBuilder<UserPreferences> builder)
    {
        builder.ToTable(
            "user_preferences",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_user_preferences_interface_language",
                    "interface_language IN ('French', 'English')");

                tableBuilder.HasCheckConstraint(
                    "ck_user_preferences_theological_language",
                    "theological_language IS NULL OR " +
                    "theological_language IN ('French', 'English')");
            });

        builder.HasKey(preferences => preferences.UserId);

        builder.Property(preferences => preferences.UserId)
            .HasConversion(
                StronglyTypedIdConverters.UserIdConverter)
            .HasColumnName("user_id")
            .ValueGeneratedNever();

        builder.Property(preferences => preferences.InterfaceLanguage)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasColumnName("interface_language")
            .IsRequired();

        builder.Property(preferences => preferences.TheologicalLanguage)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasColumnName("theological_language");

        builder.Property(preferences => preferences.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
    }
}
