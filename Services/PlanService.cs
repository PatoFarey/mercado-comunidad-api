using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Models;
using ApiMercadoComunidad.Security;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ApiMercadoComunidad.Services;

public class PlanService : IPlanService
{
    private readonly IMongoCollection<Plan> _plansCollection;
    private readonly IMongoCollection<User> _usersCollection;

    public PlanService(IOptions<MongoDbSettings> mongoDbSettings)
    {
        var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _plansCollection = mongoDatabase.GetCollection<Plan>("plans");
        _usersCollection = mongoDatabase.GetCollection<User>("users");
    }

    public async Task<Plan?> GetByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out _))
            return null;

        return await _plansCollection
            .Find(p => p.Id == id && p.Active)
            .FirstOrDefaultAsync();
    }

    public async Task<Plan?> GetDefaultCommunityAdminPlanAsync()
    {
        var freeTierPlan = await _plansCollection
            .Find(p => p.Active && p.Type == "admin" && p.Tier == "free")
            .SortBy(p => p.PriceClp)
            .FirstOrDefaultAsync();

        if (freeTierPlan != null)
            return freeTierPlan;

        return await _plansCollection
            .Find(p => p.Active && p.Type == "admin")
            .SortBy(p => p.PriceClp)
            .FirstOrDefaultAsync();
    }

    public async Task<Plan?> GetEffectivePlanForCommunityAdminAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || !ObjectId.TryParse(userId, out _))
            return null;

        var user = await _usersCollection
            .Find(u => u.Id == userId && u.IsActive)
            .FirstOrDefaultAsync();

        if (user == null || !string.Equals(user.Role, UserRoles.CommunityAdmin, StringComparison.OrdinalIgnoreCase))
            return null;

        if (!string.IsNullOrWhiteSpace(user.PlanId) && ObjectId.TryParse(user.PlanId, out _))
        {
            var assignedPlan = await GetByIdAsync(user.PlanId);
            if (assignedPlan != null && string.Equals(assignedPlan.Type, "admin", StringComparison.OrdinalIgnoreCase))
                return assignedPlan;
        }

        return await GetDefaultCommunityAdminPlanAsync();
    }

    public async Task<Plan?> GetDefaultSellerPlanAsync()
    {
        var freeTierPlan = await _plansCollection
            .Find(p => p.Active && p.Type == "seller" && p.Tier == "free")
            .SortBy(p => p.PriceClp)
            .FirstOrDefaultAsync();

        if (freeTierPlan != null)
            return freeTierPlan;

        return await _plansCollection
            .Find(p => p.Active && p.Type == "seller")
            .SortBy(p => p.PriceClp)
            .FirstOrDefaultAsync();
    }

    public async Task<Plan?> GetEffectivePlanForUserAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || !ObjectId.TryParse(userId, out _))
            return null;

        var user = await _usersCollection
            .Find(u => u.Id == userId && u.IsActive)
            .FirstOrDefaultAsync();

        if (user == null || !string.Equals(user.Role, UserRoles.Seller, StringComparison.OrdinalIgnoreCase))
            return null;

        if (!string.IsNullOrWhiteSpace(user.PlanId) && ObjectId.TryParse(user.PlanId, out _))
        {
            var assignedPlan = await GetByIdAsync(user.PlanId);
            if (assignedPlan != null && string.Equals(assignedPlan.Type, "seller", StringComparison.OrdinalIgnoreCase))
                return assignedPlan;
        }

        return await GetDefaultSellerPlanAsync();
    }
}
