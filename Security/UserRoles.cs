namespace ApiMercadoComunidad.Security;

public static class UserRoles
{
    public const string Buyer = "buyer";
    public const string Seller = "user";
    public const string CommunityAdmin = "community_admin";
    public const string SuperAdmin = "super_admin";

    public static readonly string[] All = [Buyer, Seller, CommunityAdmin, SuperAdmin];
    public static readonly string[] CanSell = [Seller, CommunityAdmin, SuperAdmin];
    public static readonly string[] AdminLevels = [CommunityAdmin, SuperAdmin];
}
