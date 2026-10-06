namespace Camping.Core.Enums;

public enum UserRole
{
    Customer = 0,

    // Elevated role. Kept at value 1 (was previously named "Organizer") so
    // existing stored role values remain valid without a data migration.
    Admin = 1
}
