using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Models;
using ApiMercadoComunidad.Models.DTOs;
using ApiMercadoComunidad.Security;
using System.Security.Cryptography;
using BCrypt.Net;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ApiMercadoComunidad.Services;

public class UserService : IUserService
{
    private readonly IMongoCollection<User> _usersCollection;
    private readonly IPlanService _planService;
    private readonly IEmailService _emailService;
    private readonly ILogger<UserService> _logger;
    private readonly Dictionary<string, List<DateTime>> _emailVerificationResendAttempts = new();
    private readonly object _emailVerificationResendLock = new();
    private static readonly TimeSpan EmailVerificationResendCooldown = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan EmailVerificationResendWindow = TimeSpan.FromHours(1);
    private const int EmailVerificationResendMaxPerWindow = 5;

    public UserService(
        IOptions<MongoDbSettings> mongoDbSettings,
        IPlanService planService,
        IEmailService emailService,
        ILogger<UserService> logger)
    {
        var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _usersCollection = mongoDatabase.GetCollection<User>("users");
        _planService = planService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<UserResponse?> RegisterAsync(RegisterRequest request)
    {
        // Verificar si el email ya existe
        if (await EmailExistsAsync(request.Email))
            return null;

        // Hash de la contraseña
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var requestedRole = (request.Role ?? string.Empty).Trim().ToLowerInvariant();
        var allowedRoles = new[] { UserRoles.Buyer, UserRoles.Seller, UserRoles.CommunityAdmin };
        var role = allowedRoles.Contains(requestedRole)
            ? requestedRole
            : UserRoles.Buyer;

        var user = new User
        {
            Name = request.Name,
            Email = request.Email.ToLower(),
            Password = hashedPassword,
            Phone = request.Phone ?? string.Empty,
            Role = role,
            IsActive = true,
            EmailVerified = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (string.Equals(role, UserRoles.Seller, StringComparison.OrdinalIgnoreCase))
        {
            var defaultSellerPlan = await _planService.GetDefaultSellerPlanAsync();
            if (!string.IsNullOrWhiteSpace(defaultSellerPlan?.Id))
                user.PlanId = defaultSellerPlan.Id;
        }

        await _usersCollection.InsertOneAsync(user);
        return await MapToUserResponseAsync(user);
    }

    public async Task<UserResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _usersCollection
            .Find(u => u.Email == request.Email.ToLower())
            .FirstOrDefaultAsync();

        if (user == null || !user.IsActive)
            return null;

        // Verificar contraseña
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            return null;

        if (!user.EmailVerified)
            throw new InvalidOperationException("No ha verificado su correo");

        // Actualizar último login
        var update = Builders<User>.Update
            .Set(u => u.LastLogin, DateTime.UtcNow)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);

        await _usersCollection.UpdateOneAsync(u => u.Id == user.Id, update);
        user.LastLogin = DateTime.UtcNow;

        return await MapToUserResponseAsync(user);
    }

    public async Task<UserResponse?> GetByIdAsync(string id)
    {
        var user = await _usersCollection
            .Find(u => u.Id == id && u.IsActive)
            .FirstOrDefaultAsync();

        return user != null ? await MapToUserResponseAsync(user) : null;
    }

    public async Task<UserResponse?> GetByEmailAsync(string email)
    {
        var user = await _usersCollection
            .Find(u => u.Email == email.ToLower() && u.IsActive)
            .FirstOrDefaultAsync();

        return user != null ? await MapToUserResponseAsync(user) : null;
    }

    public async Task<UserResponse?> UpdateUserAsync(string id, UpdateUserRequest request)
    {
        var updateDefinition = Builders<User>.Update
            .Set(u => u.UpdatedAt, DateTime.UtcNow);

        if (!string.IsNullOrEmpty(request.Name))
            updateDefinition = updateDefinition.Set(u => u.Name, request.Name);

        if (!string.IsNullOrEmpty(request.Email))
        {
            // Verificar que el email no esté en uso por otro usuario
            var existingUser = await _usersCollection
                .Find(u => u.Email == request.Email.ToLower() && u.Id != id)
                .FirstOrDefaultAsync();

            if (existingUser != null)
                return null; // O lanzar excepción: throw new InvalidOperationException("El email ya está en uso");

            updateDefinition = updateDefinition.Set(u => u.Email, request.Email.ToLower());
        }

        if (!string.IsNullOrEmpty(request.Avatar))
            updateDefinition = updateDefinition.Set(u => u.Avatar, request.Avatar);

        if (!string.IsNullOrEmpty(request.Phone))
            updateDefinition = updateDefinition.Set(u => u.Phone, request.Phone);

        if (request.Address != null)
            updateDefinition = updateDefinition.Set(u => u.Address, request.Address);

        var result = await _usersCollection.UpdateOneAsync(
            u => u.Id == id && u.IsActive,
            updateDefinition
        );

        if (result.ModifiedCount == 0)
            return null;

        return await GetByIdAsync(id);
    }

    public async Task<bool> ChangePasswordAsync(string id, ChangePasswordRequest request)
    {
        var user = await _usersCollection
            .Find(u => u.Id == id && u.IsActive)
            .FirstOrDefaultAsync();

        if (user == null)
            return false;

        // Verificar contraseña actual
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.Password))
            return false;

        // Hash de la nueva contraseña
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        var update = Builders<User>.Update
            .Set(u => u.Password, hashedPassword)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);

        var result = await _usersCollection.UpdateOneAsync(
            u => u.Id == id,
            update
        );

        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteUserAsync(string id)
    {
        // Soft delete: marcar como inactivo
        var update = Builders<User>.Update
            .Set(u => u.IsActive, false)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);

        var result = await _usersCollection.UpdateOneAsync(
            u => u.Id == id,
            update
        );

        return result.ModifiedCount > 0;
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        var count = await _usersCollection
            .CountDocumentsAsync(u => u.Email == email.ToLower());
        return count > 0;
    }

    public async Task<bool> VerifyEmailAsync(string userId)
    {
        try
        {
            if (!ObjectId.TryParse(userId, out _))
                return false;

            var filter = Builders<User>.Filter.Eq(u => u.Id, userId);
            var update = Builders<User>.Update
                .Set(u => u.EmailVerified, true)
                .Set(u => u.UpdatedAt, DateTime.UtcNow);

            var result = await _usersCollection.UpdateOneAsync(filter, update);

            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public async Task<bool> RequestEmailVerificationAsync(string email)
    {
        try
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            if (!TryRegisterEmailVerificationResendAttempt(normalizedEmail, out var retryAfterSeconds))
            {
                throw new InvalidOperationException(
                    $"Has solicitado demasiados reenvíos. Intenta nuevamente en {retryAfterSeconds} segundos.");
            }

            var user = await _usersCollection
                .Find(u => u.Email == normalizedEmail && u.IsActive)
                .FirstOrDefaultAsync();

            if (user == null || user.EmailVerified || string.IsNullOrWhiteSpace(user.Id))
                return true;

            var userName = !string.IsNullOrWhiteSpace(user.Name)
                ? user.Name
                : user.Email.Split('@')[0];

            _ = Task.Run(async () =>
            {
                await _emailService.SendWelcomeEmailAsync(user.Email, userName, user.Id);
            });

            _logger.LogInformation("Reenvío de verificación solicitado para {Email}", email);
            return true;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al reenviar verificación para {Email}", email);
            return false;
        }
    }

    private bool TryRegisterEmailVerificationResendAttempt(string email, out int retryAfterSeconds)
    {
        var now = DateTime.UtcNow;
        retryAfterSeconds = 0;

        lock (_emailVerificationResendLock)
        {
            if (!_emailVerificationResendAttempts.TryGetValue(email, out var attempts))
            {
                attempts = new List<DateTime>();
                _emailVerificationResendAttempts[email] = attempts;
            }

            attempts.RemoveAll(a => now - a > EmailVerificationResendWindow);

            if (attempts.Count > 0)
            {
                var sinceLastAttempt = now - attempts[^1];
                if (sinceLastAttempt < EmailVerificationResendCooldown)
                {
                    retryAfterSeconds = (int)Math.Ceiling((EmailVerificationResendCooldown - sinceLastAttempt).TotalSeconds);
                    return false;
                }
            }

            if (attempts.Count >= EmailVerificationResendMaxPerWindow)
            {
                var oldestAttempt = attempts[0];
                retryAfterSeconds = (int)Math.Ceiling((EmailVerificationResendWindow - (now - oldestAttempt)).TotalSeconds);
                return false;
            }

            attempts.Add(now);
            return true;
        }
    }

    public async Task<bool> RequestPasswordResetAsync(string email)
    {
        try
        {
            var user = await _usersCollection
                .Find(u => u.Email == email.ToLower() && u.IsActive)
                .FirstOrDefaultAsync();

            if (user == null)
                return false;

            // Generar código de 6 dígitos
            var resetCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            var expiryTime = DateTime.UtcNow.AddMinutes(30); // Válido por 30 minutos

            var update = Builders<User>.Update
                .Set(u => u.PasswordResetCode, resetCode)
                .Set(u => u.PasswordResetExpiry, expiryTime)
                .Set(u => u.UpdatedAt, DateTime.UtcNow);

            await _usersCollection.UpdateOneAsync(u => u.Id == user.Id, update);

            // Enviar correo con el código (sin bloquear)
            _ = Task.Run(async () =>
            {
                await _emailService.SendPasswordResetCodeAsync(user.Email, user.Name, resetCode);
            });

            _logger.LogInformation("Código de recuperación generado para {Email}", email);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar código de recuperación para {Email}", email);
            return false;
        }
    }

    public async Task<bool> ValidateResetCodeAsync(string email, string code)
    {
        try
        {
            var user = await _usersCollection
                .Find(u => u.Email == email.ToLower() && u.IsActive)
                .FirstOrDefaultAsync();

            if (user == null)
                return false;

            // Verificar que el código coincida y no haya expirado
            if (user.PasswordResetCode != code || 
                user.PasswordResetExpiry == null || 
                user.PasswordResetExpiry < DateTime.UtcNow)
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al validar código de recuperación para {Email}", email);
            return false;
        }
    }

    public async Task<bool> ResetPasswordAsync(string email, string code, string newPassword)
    {
        try
        {
            var user = await _usersCollection
                .Find(u => u.Email == email.ToLower() && u.IsActive)
                .FirstOrDefaultAsync();

            if (user == null)
                return false;

            // Verificar que el código coincida y no haya expirado
            if (user.PasswordResetCode != code || 
                user.PasswordResetExpiry == null || 
                user.PasswordResetExpiry < DateTime.UtcNow)
            {
                return false;
            }

            // Hash de la nueva contraseña
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);

            // Actualizar contraseña y limpiar código de recuperación
            var update = Builders<User>.Update
                .Set(u => u.Password, hashedPassword)
                .Unset(u => u.PasswordResetCode)
                .Unset(u => u.PasswordResetExpiry)
                .Set(u => u.UpdatedAt, DateTime.UtcNow);

            var result = await _usersCollection.UpdateOneAsync(u => u.Id == user.Id, update);

            _logger.LogInformation("Contraseña restablecida exitosamente para {Email}", email);
            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al restablecer contraseña para {Email}", email);
            return false;
        }
    }

    public async Task<bool> UpdateRoleAsync(string id, string role)
    {
        if (!UserRoles.All.Contains(role))
            return false;

        var update = Builders<User>.Update
            .Set(u => u.Role, role)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);

        var result = await _usersCollection.UpdateOneAsync(
            u => u.Id == id && u.IsActive,
            update
        );

        return result.ModifiedCount > 0;
    }

    public async Task<List<UserResponse>> GetAllAsync()
    {
        var users = await _usersCollection
            .Find(u => u.IsActive)
            .SortBy(u => u.Name)
            .ToListAsync();

        var responses = await Task.WhenAll(users.Select(MapToUserResponseAsync));
        return responses.ToList();
    }

    private async Task<UserResponse> MapToUserResponseAsync(User user)
    {
        Plan? plan = null;
        if (!string.IsNullOrWhiteSpace(user.Id))
        {
            plan = string.Equals(user.Role, UserRoles.CommunityAdmin, StringComparison.OrdinalIgnoreCase)
                ? await _planService.GetEffectivePlanForCommunityAdminAsync(user.Id)
                : await _planService.GetEffectivePlanForUserAsync(user.Id);
        }

        return new UserResponse
        {
            Id = user.Id ?? string.Empty,
            Name = user.Name,
            Email = user.Email,
            Avatar = user.Avatar,
            Role = user.Role,
            IsActive = user.IsActive,
            EmailVerified = user.EmailVerified,
            Phone = user.Phone,
            Address = user.Address,
            LastLogin = user.LastLogin,
            CreatedAt = user.CreatedAt,
            PlanId = user.PlanId,
            Plan = MapToPlanSummary(plan)
        };
    }

    private static PlanSummaryResponse? MapToPlanSummary(Plan? plan)
    {
        if (plan == null)
            return null;

        var storesLimit = plan.Limits.Stores > 0
            ? plan.Limits.Stores
            : plan.Tier.Equals("free", StringComparison.OrdinalIgnoreCase) ? 1
            : plan.Tier.Equals("pro", StringComparison.OrdinalIgnoreCase) ? 3
            : -1;

        return new PlanSummaryResponse
        {
            Id = plan.Id ?? string.Empty,
            Code = plan.Code,
            Name = plan.Name,
            Type = plan.Type,
            Tier = plan.Tier,
            Active = plan.Active,
            Limits = new PlanLimitsResponse
            {
                Stores = storesLimit,
                Products = plan.Limits.Products == 0 ? -1 : plan.Limits.Products,
                ImagesPerProduct = plan.Limits.ImagesPerProduct == 0 ? -1 : plan.Limits.ImagesPerProduct,
                VideoPerProduct = plan.Limits.VideoPerProduct,
                CommunitiesJoin = plan.Limits.CommunitiesJoin == 0 ? -1 : plan.Limits.CommunitiesJoin,
                CommunitiesCreate = plan.Limits.CommunitiesCreate == 0 ? -1 : plan.Limits.CommunitiesCreate,
                SellersPerCommunity = plan.Limits.SellersPerCommunity == 0 ? -1 : plan.Limits.SellersPerCommunity,
            }
        };
    }
}

