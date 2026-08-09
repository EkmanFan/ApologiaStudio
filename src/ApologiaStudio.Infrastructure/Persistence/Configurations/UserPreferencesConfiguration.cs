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

                tableBuilder.HasCheckConstraint(
                    "ck_user_preferences_composer_enter_behavior",
                    "composer_enter_behavior IN " +
                    "('NewLine', 'SendMessage')");
                tableBuilder.HasCheckConstraint(
                    "ck_user_preferences_message_date_format",
                    "message_date_format IN " +
                    "('dd/MM/yyyy', 'MM/dd/yyyy', 'yyyy-MM-dd')");
                tableBuilder.HasCheckConstraint(
                    "ck_user_preferences_message_time_format",
                    "message_time_format IN " +
                    "('HH:mm:ss', 'HH:mm', 'hh:mm:ss tt', 'hh:mm tt')");
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

        builder.Property(preferences => preferences.EnterBehavior)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasColumnName("composer_enter_behavior")
            .HasDefaultValueSql("'NewLine'")
            .IsRequired();

        builder.Property(preferences => preferences.MessageDateFormat)
            .HasMaxLength(16)
            .HasColumnName("message_date_format")
            .HasDefaultValue(UserPreferences.DefaultMessageDateFormat)
            .IsRequired();

        builder.Property(preferences => preferences.MessageTimeFormat)
            .HasMaxLength(16)
            .HasColumnName("message_time_format")
            .HasDefaultValue(UserPreferences.DefaultMessageTimeFormat)
            .IsRequired();

        builder.Property(preferences => preferences.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
    }
}
