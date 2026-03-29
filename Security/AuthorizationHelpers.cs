using System.Security.Claims;
using ApiMercadoComunidad.Models;
using ApiMercadoComunidad.Services;

namespace ApiMercadoComunidad.Security;

public static class AuthorizationHelpers
{
    public static string? GetCurrentUserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(ClaimTypes.Name);

    public static bool IsAdmin(ClaimsPrincipal user) => user.IsInRole("admin");

    public static bool CanAccessUser(ClaimsPrincipal user, string targetUserId)
    {
        var currentUserId = GetCurrentUserId(user);
        return IsAdmin(user) || (!string.IsNullOrEmpty(currentUserId) && currentUserId == targetUserId);
    }

    public static async Task<bool> CanManageStoreAsync(string storeId, ClaimsPrincipal user, IStoreService storeService)
    {
        if (IsAdmin(user))
        {
            return true;
        }

        var currentUserId = GetCurrentUserId(user);
        if (string.IsNullOrEmpty(currentUserId))
        {
            return false;
        }

        var store = await storeService.GetByIdAsync(storeId);
        return store != null && store.Users.Any(u => u.UserID == currentUserId);
    }

    public static async Task<bool> CanManageProductAsync(
        string productId,
        ClaimsPrincipal user,
        IProductService productService,
        IStoreService storeService)
    {
        if (IsAdmin(user))
        {
            return true;
        }

        var product = await productService.GetByIdAsync(productId);
        return product != null && await CanManageStoreAsync(product.IdStore, user, storeService);
    }

    public static async Task<bool> CanManageStoreUsersAsync(string storeId, ClaimsPrincipal user, IStoreService storeService)
    {
        if (IsAdmin(user))
        {
            return true;
        }

        var currentUserId = GetCurrentUserId(user);
        if (string.IsNullOrEmpty(currentUserId))
        {
            return false;
        }

        var store = await storeService.GetByIdAsync(storeId);
        if (store == null)
        {
            return false;
        }

        return store.Users.Any(u => u.UserID == currentUserId && (u.Role == "1" || u.Role.Equals("admin", StringComparison.OrdinalIgnoreCase)));
    }

    public static async Task<bool> CanManageImageAsync(
        string folder,
        string entityId,
        ClaimsPrincipal user,
        IProductService productService,
        IStoreService storeService)
    {
        if (IsAdmin(user))
        {
            return true;
        }

        var currentUserId = GetCurrentUserId(user);
        if (string.IsNullOrEmpty(currentUserId))
        {
            return false;
        }

        return folder.ToLowerInvariant() switch
        {
            "user" => currentUserId == entityId,
            "store" => await CanManageStoreAsync(entityId, user, storeService),
            "product" => await CanManageProductAsync(entityId, user, productService, storeService),
            _ => false
        };
    }

    public static Task<bool> CanManageImageAsync(
        ImageUpload image,
        ClaimsPrincipal user,
        IProductService productService,
        IStoreService storeService) =>
        CanManageImageAsync(image.Folder, image.EntityId, user, productService, storeService);
}
