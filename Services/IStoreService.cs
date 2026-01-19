using ApiMercadoComunidad.Models;
using ApiMercadoComunidad.Models.DTOs;

namespace ApiMercadoComunidad.Services;

public interface IStoreService
{
    Task<List<StoreResponse>> GetAllAsync();
    Task<StoreResponse?> GetByIdAsync(string id);
    Task<StoreResponse?> GetByLinkStoreAsync(string linkStore);
    Task<List<StoreResponse>> GetByUserIdAsync(string userId);
    Task<List<StoreResponse>> GetActiveStoresAsync();
    Task<List<StoreResponse>> GetGlobalStoresAsync();
    Task<StoreResponse> CreateAsync(CreateStoreRequest request);
    Task<StoreResponse?> UpdateAsync(string id, UpdateStoreRequest request);
    Task<bool> DeleteAsync(string id);
    Task<bool> AddUserToStoreAsync(string storeId, string userId, string role);
    Task<bool> RemoveUserFromStoreAsync(string storeId, string userId);
    Task<bool> LinkStoreExistsAsync(string linkStore);
}