using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Models.DTOs;
using ApiMercadoComunidad.Security;
using ApiMercadoComunidad.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

builder.Services.Configure<AzureBlobSettings>(
    builder.Configuration.GetSection("AzureBlobSettings"));

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("La sección Jwt es requerida.");

if (string.IsNullOrWhiteSpace(jwtSettings.Key))
    throw new InvalidOperationException("Jwt:Key es requerido.");

if (string.IsNullOrWhiteSpace(jwtSettings.Issuer) || string.IsNullOrWhiteSpace(jwtSettings.Audience))
    throw new InvalidOperationException("Jwt:Issuer y Jwt:Audience son requeridos.");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key));

builder.Services.AddSingleton<IProductService, ProductService>();
builder.Services.AddSingleton<ICommunityService, CommunityService>();
builder.Services.AddSingleton<ICommunityProductService, CommunityProductService>();
builder.Services.AddSingleton<IUserService, UserService>();
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();
builder.Services.AddSingleton<IStoreService, StoreService>();
builder.Services.AddSingleton<ICategoryService, CategoryService>();
builder.Services.AddSingleton<IProductSynchronizeService, ProductSynchronizeService>();
builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddSingleton<ITokenService, TokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "http://localhost:3001",
                "http://localhost:5173",
                "https://localhost:5173",
                "https://mercadocomunidad.cl")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

bool IsAuthenticated(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

IResult UnauthorizedResult() => Results.Unauthorized();

bool CanAccessEmail(ClaimsPrincipal user, string email)
{
    var currentEmail = user.FindFirstValue(ClaimTypes.Email);
    return AuthorizationHelpers.IsAdmin(user) ||
           (!string.IsNullOrWhiteSpace(currentEmail) &&
            string.Equals(currentEmail, email, StringComparison.OrdinalIgnoreCase));
}

#region Products

app.MapGet("/products", async (IProductService service, int pageNumber = 1, int pageSize = 10) =>
{
    var result = await service.GetPaginatedAsync(pageNumber, pageSize);
    return Results.Ok(result);
});

app.MapGet("/products/{id}", async (string id, IProductService service) =>
{
    var product = await service.GetByIdAsync(id);
    return product is not null ? Results.Ok(product) : Results.NotFound();
});

app.MapGet("/products/categoria/{categoria}", async (string categoria, IProductService service, int pageNumber = 1, int pageSize = 10) =>
{
    var result = await service.GetByCategoryAsync(categoria, pageNumber, pageSize);
    return Results.Ok(result);
});

app.MapGet("/products/store/{storeId}", async (string storeId, IProductService service, int pageNumber = 1, int pageSize = 10) =>
{
    var result = await service.GetByStoreIdPaginatedAsync(storeId, pageNumber, pageSize);
    return Results.Ok(result);
});

app.MapGet("/products/active/list", async (IProductService service) =>
{
    var products = await service.GetActiveProductsAsync();
    return Results.Ok(products);
});

app.MapPost("/products", async (CreateProductRequest request, ClaimsPrincipal user, IProductService service, IStoreService storeService) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (string.IsNullOrEmpty(request.Title) || string.IsNullOrEmpty(request.IdStore))
        return Results.BadRequest(new { message = "Title y IdStore son requeridos" });

    if (!await AuthorizationHelpers.CanManageStoreAsync(request.IdStore, user, storeService))
        return Results.Forbid();

    var product = await service.CreateAsync(request);
    return Results.Created($"/products/{product.Id}", product);
}).RequireAuthorization();

app.MapPut("/products/{id}", async (string id, UpdateProductRequest request, ClaimsPrincipal user, IProductService service, IStoreService storeService) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!await AuthorizationHelpers.CanManageProductAsync(id, user, service, storeService))
        return Results.Forbid();

    var product = await service.UpdateAsync(id, request);
    return product is not null ? Results.Ok(product) : Results.NotFound();
}).RequireAuthorization();

app.MapDelete("/products/{id}", async (string id, ClaimsPrincipal user, IProductService service, IStoreService storeService) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!await AuthorizationHelpers.CanManageProductAsync(id, user, service, storeService))
        return Results.Forbid();

    var success = await service.DeleteAsync(id);
    return success ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

app.MapPost("/products/{id}/images", async (string id, AddImageRequest request, ClaimsPrincipal user, IProductService service, IStoreService storeService) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!await AuthorizationHelpers.CanManageProductAsync(id, user, service, storeService))
        return Results.Forbid();

    if (string.IsNullOrEmpty(request.ImageUrl))
        return Results.BadRequest(new { message = "ImageUrl es requerida" });

    try
    {
        var product = await service.AddImageAsync(id, request.ImageUrl);
        return product is not null ? Results.Ok(product) : Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
}).RequireAuthorization();

app.MapDelete("/products/{id}/images", async (string id, string imageUrl, ClaimsPrincipal user, IProductService service, IStoreService storeService) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!await AuthorizationHelpers.CanManageProductAsync(id, user, service, storeService))
        return Results.Forbid();

    if (string.IsNullOrEmpty(imageUrl))
        return Results.BadRequest(new { message = "imageUrl query parameter es requerido" });

    try
    {
        var product = await service.RemoveImageAsync(id, imageUrl);
        return product is not null ? Results.Ok(product) : Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
}).RequireAuthorization();

app.MapPut("/products/{id}/images/reorder", async (string id, ReorderImagesRequest request, ClaimsPrincipal user, IProductService service, IStoreService storeService) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!await AuthorizationHelpers.CanManageProductAsync(id, user, service, storeService))
        return Results.Forbid();

    if (request.Images == null || !request.Images.Any())
        return Results.BadRequest(new { message = "Images es requerido y no puede estar vacío" });

    try
    {
        var product = await service.ReorderImagesAsync(id, request.Images);
        return product is not null ? Results.Ok(product) : Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
}).RequireAuthorization();

#endregion

#region Communities

app.MapGet("/communities", async (ICommunityService service) =>
{
    var communities = await service.GetAllAsync();
    return Results.Ok(communities);
});

app.MapGet("/communities/{id}", async (string id, ICommunityService service) =>
{
    var community = await service.GetByIdAsync(id);
    return community is not null ? Results.Ok(community) : Results.NotFound();
});

app.MapGet("/communities/by-community-id/{communityId}", async (string communityId, ICommunityService service) =>
{
    var community = await service.GetByCommunityIdAsync(communityId);
    return community is not null ? Results.Ok(community) : Results.NotFound();
});

app.MapGet("/communities/active", async (ICommunityService service) =>
{
    var communities = await service.GetActiveCommunitiesAsync();
    return Results.Ok(communities);
});

app.MapGet("/communities/visible", async (ICommunityService service) =>
{
    var communities = await service.GetVisibleCommunitiesAsync();
    return Results.Ok(communities);
});

#endregion

#region Community Products

app.MapGet("/community-products/{communityId}", async (string communityId, ICommunityProductService service, int pageNumber = 1, int pageSize = 10) =>
{
    var result = await service.GetByCommunityIdPaginatedAsync(communityId, pageNumber, pageSize);
    return Results.Ok(result);
});

app.MapGet("/community-products/{communityId}/categoria/{categoria}", async (string communityId, string categoria, ICommunityProductService service) =>
{
    var products = await service.GetByCategoriaAsync(communityId, categoria);
    return Results.Ok(products);
});

app.MapGet("/community-products/product/{id}", async (string id, ICommunityProductService service) =>
{
    var product = await service.GetByIdAsync(id);
    return product is not null ? Results.Ok(product) : Results.NotFound();
});

#endregion

#region Users

app.MapPost("/auth/register", async (RegisterRequest request, IUserService service, IEmailService emailService, ITokenService tokenService) =>
{
    if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
        return Results.BadRequest(new { message = "Email y contraseña son requeridos" });

    var user = await service.RegisterAsync(request);

    if (user == null)
        return Results.Conflict(new { message = "El email ya está registrado" });

    _ = Task.Run(async () =>
    {
        var userName = !string.IsNullOrEmpty(user.Name) ? user.Name : user.Email.Split('@')[0];
        await emailService.SendWelcomeEmailAsync(user.Email, userName, user.Id);
    });

    var authResponse = tokenService.CreateAuthResponse(user);
    return Results.Created($"/users/{user.Id}", authResponse);
});

app.MapPost("/auth/login", async (LoginRequest request, IUserService service, ITokenService tokenService) =>
{
    if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
        return Results.BadRequest(new { message = "Email y contraseña son requeridos" });

    var user = await service.LoginAsync(request);

    if (user == null)
        return Results.Unauthorized();

    var authResponse = tokenService.CreateAuthResponse(user);
    return Results.Ok(authResponse);
});

app.MapGet("/users/{id}", async (string id, ClaimsPrincipal user, IUserService service) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!AuthorizationHelpers.CanAccessUser(user, id))
        return Results.Forbid();

    var result = await service.GetByIdAsync(id);
    return result is not null ? Results.Ok(result) : Results.NotFound();
}).RequireAuthorization();

app.MapGet("/users/email/{email}", async (string email, ClaimsPrincipal user, IUserService service) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!CanAccessEmail(user, email))
        return Results.Forbid();

    var result = await service.GetByEmailAsync(email);
    return result is not null ? Results.Ok(result) : Results.NotFound();
}).RequireAuthorization();

app.MapPut("/users/{id}", async (string id, UpdateUserRequest request, ClaimsPrincipal user, IUserService service) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!AuthorizationHelpers.CanAccessUser(user, id))
        return Results.Forbid();

    var result = await service.UpdateUserAsync(id, request);
    return result is not null ? Results.Ok(result) : Results.NotFound();
}).RequireAuthorization();

app.MapPost("/users/{id}/change-password", async (string id, ChangePasswordRequest request, ClaimsPrincipal user, IUserService service) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!AuthorizationHelpers.CanAccessUser(user, id))
        return Results.Forbid();

    if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword))
        return Results.BadRequest(new { message = "Las contraseñas son requeridas" });

    var success = await service.ChangePasswordAsync(id, request);

    if (!success)
        return Results.BadRequest(new { message = "Contraseña actual incorrecta" });

    return Results.Ok(new { message = "Contraseña actualizada correctamente" });
}).RequireAuthorization();

app.MapDelete("/users/{id}", async (string id, ClaimsPrincipal user, IUserService service) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!AuthorizationHelpers.CanAccessUser(user, id))
        return Results.Forbid();

    var success = await service.DeleteUserAsync(id);
    return success ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

app.MapPost("/auth/verify-email/{userId}", async (string userId, IUserService service) =>
{
    if (string.IsNullOrEmpty(userId))
        return Results.BadRequest(new { message = "UserId es requerido" });

    var success = await service.VerifyEmailAsync(userId);

    if (!success)
        return Results.NotFound(new { message = "Usuario no encontrado o ya verificado" });

    return Results.Ok(new { message = "Email verificado correctamente", emailVerified = true });
});

app.MapPost("/auth/request-password-reset", async (RequestPasswordResetRequest request, IUserService service) =>
{
    if (string.IsNullOrEmpty(request.Email))
        return Results.BadRequest(new { message = "Email es requerido" });

    await service.RequestPasswordResetAsync(request.Email);

    return Results.Ok(new
    {
        message = "Si el email está registrado, recibirás un código de recuperación",
        success = true
    });
});

app.MapPost("/auth/validate-reset-code", async (ValidateResetCodeRequest request, IUserService service) =>
{
    if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Code))
        return Results.BadRequest(new { message = "Email y código son requeridos" });

    var isValid = await service.ValidateResetCodeAsync(request.Email, request.Code);

    if (!isValid)
        return Results.BadRequest(new { message = "Código inválido o expirado", valid = false });

    return Results.Ok(new { message = "Código válido", valid = true });
});

app.MapPost("/auth/reset-password", async (ResetPasswordRequest request, IUserService service) =>
{
    if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Code) || string.IsNullOrEmpty(request.NewPassword))
        return Results.BadRequest(new { message = "Email, código y nueva contraseña son requeridos" });

    if (request.NewPassword.Length < 6)
        return Results.BadRequest(new { message = "La contraseña debe tener al menos 6 caracteres" });

    var success = await service.ResetPasswordAsync(request.Email, request.Code, request.NewPassword);

    if (!success)
        return Results.BadRequest(new { message = "No se pudo restablecer la contraseña. Código inválido o expirado" });

    return Results.Ok(new { message = "Contraseña restablecida correctamente", success = true });
});

#endregion

#region Stores

app.MapGet("/stores", async (IStoreService service) =>
{
    var stores = await service.GetAllAsync();
    return Results.Ok(stores);
});

app.MapGet("/stores/{id}", async (string id, IStoreService service) =>
{
    var store = await service.GetByIdAsync(id);
    return store is not null ? Results.Ok(store) : Results.NotFound();
});

app.MapGet("/stores/link/{linkStore}", async (string linkStore, IStoreService service) =>
{
    var store = await service.GetByLinkStoreAsync(linkStore);
    return store is not null ? Results.Ok(store) : Results.NotFound();
});

app.MapGet("/stores/user/{userId}", async (string userId, ClaimsPrincipal user, IStoreService service) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!AuthorizationHelpers.CanAccessUser(user, userId))
        return Results.Forbid();

    var stores = await service.GetByUserIdAsync(userId);
    return Results.Ok(stores);
}).RequireAuthorization();

app.MapGet("/stores/active/list", async (IStoreService service) =>
{
    var stores = await service.GetActiveStoresAsync();
    return Results.Ok(stores);
});

app.MapGet("/stores/global/list", async (IStoreService service) =>
{
    var stores = await service.GetGlobalStoresAsync();
    return Results.Ok(stores);
});

app.MapPost("/stores", async (CreateStoreRequest request, ClaimsPrincipal user, IStoreService service) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (string.IsNullOrEmpty(request.Name) || string.IsNullOrEmpty(request.LinkStore))
        return Results.BadRequest(new { message = "Name y LinkStore son requeridos" });

    request.UserId = AuthorizationHelpers.GetCurrentUserId(user);

    try
    {
        var store = await service.CreateAsync(request);
        return Results.Created($"/stores/{store.Id}", store);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
}).RequireAuthorization();

app.MapPut("/stores/{id}", async (string id, UpdateStoreRequest request, ClaimsPrincipal user, IStoreService service) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!await AuthorizationHelpers.CanManageStoreAsync(id, user, service))
        return Results.Forbid();

    var store = await service.UpdateAsync(id, request);
    return store is not null ? Results.Ok(store) : Results.NotFound();
}).RequireAuthorization();

app.MapDelete("/stores/{id}", async (string id, ClaimsPrincipal user, IStoreService service) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!await AuthorizationHelpers.CanManageStoreAsync(id, user, service))
        return Results.Forbid();

    var success = await service.DeleteAsync(id);
    return success ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

app.MapPost("/stores/{storeId}/users", async (string storeId, AddUserToStoreRequest request, ClaimsPrincipal user, IStoreService service) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!await AuthorizationHelpers.CanManageStoreUsersAsync(storeId, user, service))
        return Results.Forbid();

    if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.Role))
        return Results.BadRequest(new { message = "UserId y Role son requeridos" });

    var success = await service.AddUserToStoreAsync(storeId, request.UserId, request.Role);
    return success ? Results.Ok(new { message = "Usuario agregado a la tienda" }) : Results.BadRequest(new { message = "No se pudo agregar el usuario" });
}).RequireAuthorization();

app.MapDelete("/stores/{storeId}/users/{userId}", async (string storeId, string userId, ClaimsPrincipal user, IStoreService service) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!await AuthorizationHelpers.CanManageStoreUsersAsync(storeId, user, service))
        return Results.Forbid();

    var success = await service.RemoveUserFromStoreAsync(storeId, userId);
    return success ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

#endregion

#region Blob Storage

app.MapPost("/images/upload", async (HttpRequest request, ClaimsPrincipal user, IBlobStorageService service, IProductService productService, IStoreService storeService) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!request.HasFormContentType)
        return Results.BadRequest(new { message = "El contenido debe ser multipart/form-data" });

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    var folder = form["folder"].ToString();
    var entityId = form["entityId"].ToString();

    if (file == null || file.Length == 0)
        return Results.BadRequest(new { message = "No se ha enviado ningún archivo" });

    if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(entityId))
        return Results.BadRequest(new { message = "Folder y EntityId son requeridos" });

    if (!await AuthorizationHelpers.CanManageImageAsync(folder, entityId, user, productService, storeService))
        return Results.Forbid();

    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!allowedExtensions.Contains(fileExtension))
        return Results.BadRequest(new { message = "Solo se permiten archivos de imagen (jpg, jpeg, png, gif, webp)" });

    if (file.Length > 5 * 1024 * 1024)
        return Results.BadRequest(new { message = "El archivo no puede superar los 5MB" });

    var uploadRequest = new ImageUploadRequest
    {
        Folder = folder,
        EntityId = entityId,
        UploadedBy = AuthorizationHelpers.GetCurrentUserId(user)
    };

    using var stream = file.OpenReadStream();
    var result = await service.UploadImageAsync(stream, file.FileName, file.ContentType, uploadRequest);

    return Results.Created($"/images/{result.Id}", result);
}).RequireAuthorization();

app.MapGet("/images/{folder}/{entityId}", async (string folder, string entityId, IBlobStorageService service) =>
{
    var images = await service.GetImagesByEntityAsync(folder, entityId);
    return Results.Ok(images);
});

app.MapGet("/images/{id}", async (string id, IBlobStorageService service) =>
{
    var image = await service.GetImageByIdAsync(id);
    return image is not null ? Results.Ok(image) : Results.NotFound();
});

app.MapDelete("/images/{id}", async (string id, ClaimsPrincipal user, IBlobStorageService service, IProductService productService, IStoreService storeService) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    var image = await service.GetImageByIdAsync(id);
    if (image == null)
        return Results.NotFound();

    if (!await AuthorizationHelpers.CanManageImageAsync(image, user, productService, storeService))
        return Results.Forbid();

    var success = await service.DeleteImageAsync(id);
    return success ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

app.MapDelete("/images/by-url", async (string blobUrl, string entityId, ClaimsPrincipal user, IBlobStorageService service, IProductService productService, IStoreService storeService) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (string.IsNullOrEmpty(blobUrl) || string.IsNullOrEmpty(entityId))
        return Results.BadRequest(new { message = "blobUrl y entityId son requeridos" });

    var image = (await service.GetImagesByEntityAsync("product", entityId))
        .FirstOrDefault(i => string.Equals(i.BlobUrl, blobUrl, StringComparison.OrdinalIgnoreCase))
        ?? (await service.GetImagesByEntityAsync("store", entityId))
            .FirstOrDefault(i => string.Equals(i.BlobUrl, blobUrl, StringComparison.OrdinalIgnoreCase))
        ?? (await service.GetImagesByEntityAsync("user", entityId))
            .FirstOrDefault(i => string.Equals(i.BlobUrl, blobUrl, StringComparison.OrdinalIgnoreCase));

    if (image == null)
        return Results.NotFound();

    if (!await AuthorizationHelpers.CanManageImageAsync(image, user, productService, storeService))
        return Results.Forbid();

    var success = await service.DeleteImageByUrlAsync(blobUrl, entityId);
    return success ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

#endregion

#region Categories

app.MapGet("/categories", async (ICategoryService service) =>
{
    var categories = await service.GetAllAsync();
    return Results.Ok(categories);
});

app.MapGet("/categories/{id}", async (string id, ICategoryService service) =>
{
    var category = await service.GetByIdAsync(id);
    return category is not null ? Results.Ok(category) : Results.NotFound();
});

app.MapGet("/categories/name/{name}", async (string name, ICategoryService service) =>
{
    var category = await service.GetByNameAsync(name);
    return category is not null ? Results.Ok(category) : Results.NotFound();
});

app.MapPost("/categories", async (CreateCategoryRequest request, ClaimsPrincipal user, ICategoryService service) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!AuthorizationHelpers.IsAdmin(user))
        return Results.Forbid();

    if (string.IsNullOrEmpty(request.Name))
        return Results.BadRequest(new { message = "Name es requerido" });

    try
    {
        var category = await service.CreateAsync(request);
        return Results.Created($"/categories/{category.Id}", category);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
}).RequireAuthorization();

app.MapPut("/categories/{id}", async (string id, UpdateCategoryRequest request, ClaimsPrincipal user, ICategoryService service) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!AuthorizationHelpers.IsAdmin(user))
        return Results.Forbid();

    if (string.IsNullOrEmpty(request.Name))
        return Results.BadRequest(new { message = "Name es requerido" });

    try
    {
        var category = await service.UpdateAsync(id, request);
        return category is not null ? Results.Ok(category) : Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
}).RequireAuthorization();

app.MapDelete("/categories/{id}", async (string id, ClaimsPrincipal user, ICategoryService service) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!AuthorizationHelpers.IsAdmin(user))
        return Results.Forbid();

    var success = await service.DeleteAsync(id);
    return success ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

#endregion

#region Product Synchronize

app.MapPost("/products/{id}/synchronize", async (string id, ClaimsPrincipal user, IProductSynchronizeService synchronizeService, IProductService productService, IStoreService storeService) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!await AuthorizationHelpers.CanManageProductAsync(id, user, productService, storeService))
        return Results.Forbid();

    var success = await synchronizeService.SynchronizeProductAsync(id);

    if (!success)
        return Results.NotFound(new { message = "Producto no encontrado o no tiene tienda/comunidad asociada" });

    return Results.Ok(new { message = "Producto sincronizado correctamente", productId = id });
}).RequireAuthorization();

app.MapPost("/products/synchronize/all", async (ClaimsPrincipal user, IProductSynchronizeService service) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!AuthorizationHelpers.IsAdmin(user))
        return Results.Forbid();

    var count = await service.SynchronizeAllProductsAsync();
    return Results.Ok(new { message = $"Se sincronizaron {count} productos", totalSynchronized = count });
}).RequireAuthorization();

app.MapPost("/products/synchronize/store/{storeId}", async (string storeId, ClaimsPrincipal user, IProductSynchronizeService service, IStoreService storeService) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!await AuthorizationHelpers.CanManageStoreAsync(storeId, user, storeService))
        return Results.Forbid();

    var count = await service.SynchronizeProductsByStoreAsync(storeId);
    return Results.Ok(new { message = $"Se sincronizaron {count} productos de la tienda", totalSynchronized = count });
}).RequireAuthorization();

#endregion

app.Run();

public record AddUserToStoreRequest(string UserId, string Role);
