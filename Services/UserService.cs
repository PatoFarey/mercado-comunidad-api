using Microsoft.Extensions.Options;
using MongoDB.Driver;
using BCrypt.Net;
using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Models;
using ApiMercadoComunidad.Models.DTOs;

namespace ApiMercadoComunidad.Services;

public class UserService : IUserService
{
    private readonly IMongoCollection<User> _usersCollection;

    public UserService(IOptions<MongoDbSettings> mongoDbSettings)
    {
        var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _usersCollection = mongoDatabase.GetCollection<User>("users");
    }

    public async Task<UserResponse?> RegisterAsync(RegisterRequest request)
    {
        // Verificar si el email ya existe
        if (await EmailExistsAsync(request.Email))
            return null;

        // Hash de la contraseña
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var user = new User
        {
            Name = request.Name,
            Email = request.Email.ToLower(),
            Password = hashedPassword,
            Phone = request.Phone ?? string.Empty,
            Role = "user",
            IsActive = true,
            EmailVerified = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _usersCollection.InsertOneAsync(user);
        return MapToUserResponse(user);
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

        // Actualizar último login
        var update = Builders<User>.Update
            .Set(u => u.LastLogin, DateTime.UtcNow)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);
        
        await _usersCollection.UpdateOneAsync(u => u.Id == user.Id, update);
        user.LastLogin = DateTime.UtcNow;

        return MapToUserResponse(user);
    }

    public async Task<UserResponse?> GetByIdAsync(string id)
    {
        var user = await _usersCollection
            .Find(u => u.Id == id && u.IsActive)
            .FirstOrDefaultAsync();

        return user != null ? MapToUserResponse(user) : null;
    }

    public async Task<UserResponse?> GetByEmailAsync(string email)
    {
        var user = await _usersCollection
            .Find(u => u.Email == email.ToLower() && u.IsActive)
            .FirstOrDefaultAsync();

        return user != null ? MapToUserResponse(user) : null;
    }

    public async Task<UserResponse?> UpdateUserAsync(string id, UpdateUserRequest request)
    {
        var updateDefinition = Builders<User>.Update
            .Set(u => u.UpdatedAt, DateTime.UtcNow);

        if (!string.IsNullOrEmpty(request.Name))
            updateDefinition = updateDefinition.Set(u => u.Name, request.Name);

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

    private UserResponse MapToUserResponse(User user)
    {
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
            CreatedAt = user.CreatedAt
        };
    }
}