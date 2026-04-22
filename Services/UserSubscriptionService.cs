using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Models;
using ApiMercadoComunidad.Models.DTOs;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace ApiMercadoComunidad.Services;

public class UserSubscriptionService : IUserSubscriptionService
{
    private readonly IMongoCollection<UserSubscription> _col;
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<Plan> _plans;

    public UserSubscriptionService(IOptions<MongoDbSettings> mongoDbSettings)
    {
        var client = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var db = client.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _col = db.GetCollection<UserSubscription>("user_subscriptions");
        _users = db.GetCollection<User>("users");
        _plans = db.GetCollection<Plan>("plans");
    }

    public async Task<UserSubscription?> GetByUserIdAsync(string userId)
    {
        return await _col
            .Find(s => s.UserId == userId)
            .SortByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<UserSubscription> CreatePendingAsync(string userId, string planId, string paykuClientId, string paykuSubscriptionToken)
    {
        var existing = await _col
            .Find(s => s.UserId == userId && s.Status == "pending")
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            var updatePending = Builders<UserSubscription>.Update
                .Set(s => s.PlanId, planId)
                .Set(s => s.PaykuClientId, paykuClientId)
                .Set(s => s.PaykuSubscriptionId, paykuSubscriptionToken)
                .Set(s => s.UpdatedAt, DateTime.UtcNow);
            await _col.UpdateOneAsync(s => s.Id == existing.Id, updatePending);
            existing.PlanId = planId;
            existing.PaykuClientId = paykuClientId;
            existing.PaykuSubscriptionId = paykuSubscriptionToken;
            return existing;
        }

        var sub = new UserSubscription
        {
            UserId = userId,
            PlanId = planId,
            PaykuClientId = paykuClientId,
            PaykuSubscriptionId = paykuSubscriptionToken,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _col.InsertOneAsync(sub);
        return sub;
    }

    public async Task<UserSubscription?> ActivateAsync(string paykuSubscriptionId, string paykuClientId, DateTime nextBillingDate)
    {
        var updateDef = Builders<UserSubscription>.Update
            .Set(s => s.Status, "active")
            .Set(s => s.StartDate, DateTime.UtcNow)
            .Set(s => s.NextBillingDate, nextBillingDate)
            .Set(s => s.UpdatedAt, DateTime.UtcNow);

        var update = string.IsNullOrWhiteSpace(paykuSubscriptionId)
            ? updateDef
            : updateDef.Set(s => s.PaykuSubscriptionId, paykuSubscriptionId);

        var result = await _col.FindOneAndUpdateAsync(
            s => s.PaykuClientId == paykuClientId && s.Status == "pending",
            update,
            new FindOneAndUpdateOptions<UserSubscription> { ReturnDocument = ReturnDocument.After });

        if (result != null)
            await _users.UpdateOneAsync(
                u => u.Id == result.UserId,
                Builders<User>.Update.Set(u => u.PlanId, result.PlanId));

        return result;
    }

    public async Task<UserSubscription?> CancelAsync(string userId)
    {
        var update = Builders<UserSubscription>.Update
            .Set(s => s.Status, "cancelled")
            .Set(s => s.CancelledAt, DateTime.UtcNow)
            .Set(s => s.UpdatedAt, DateTime.UtcNow);

        var result = await _col.FindOneAndUpdateAsync(
            s => s.UserId == userId && (s.Status == "active" || s.Status == "pending"),
            update,
            new FindOneAndUpdateOptions<UserSubscription> { ReturnDocument = ReturnDocument.After });

        if (result != null)
        {
            // Determine plan type from the cancelled plan to find the matching free plan
            var cancelledPlan = await _plans.Find(p => p.Id == result.PlanId).FirstOrDefaultAsync();
            var planType = cancelledPlan?.Type ?? "seller";
            var freePlan = await _plans
                .Find(p => p.Type == planType && p.Tier == "free" && p.Active)
                .FirstOrDefaultAsync();

            if (freePlan != null)
                await _users.UpdateOneAsync(
                    u => u.Id == userId,
                    Builders<User>.Update.Set(u => u.PlanId, freePlan.Id));
        }

        return result;
    }

    public async Task RecordPaymentAsync(string paykuClientId, string paykuSubscriptionId, DateTime nextBillingDate)
    {
        var update = Builders<UserSubscription>.Update
            .Set(s => s.Status, "active")
            .Set(s => s.NextBillingDate, nextBillingDate)
            .Set(s => s.UpdatedAt, DateTime.UtcNow);

        await _col.UpdateOneAsync(
            s => s.PaykuClientId == paykuClientId && s.PaykuSubscriptionId == paykuSubscriptionId,
            update);
    }
}
