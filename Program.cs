using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Services;
using ApiMercadoComunidad.Models.DTOs;

var builder = WebApplication.CreateBuilder(args);

// Configurar MongoDB
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

// Configurar Azure Blob Storage
builder.Services.Configure<AzureBlobSettings>(
    builder.Configuration.GetSection("AzureBlobSettings"));

// Registrar los servicios
builder.Services.AddSingleton<IProductService, ProductService>();
builder.Services.AddSingleton<ICommunityService, CommunityService>();
builder.Services.AddSingleton<ICommunityProductService, CommunityProductService>();
builder.Services.AddSingleton<IUserService, UserService>();
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();
builder.Services.AddSingleton<IStoreService, StoreService>();
builder.Services.AddSingleton<ICategoryService, CategoryService>();
builder.Services.AddSingleton<IProductSynchronizeService, ProductSynchronizeService>();

// Configurar CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://mercadocomunidad.cl")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Agregar controladores si es necesario
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Habilitar CORS
app.UseCors("AllowFrontend");

#region "products"

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

app.MapPost("/products", async (CreateProductRequest request, IProductService service) =>
{
    if (string.IsNullOrEmpty(request.Title) || string.IsNullOrEmpty(request.IdStore))
        return Results.BadRequest(new { message = "Title y IdStore son requeridos" });

    var product = await service.CreateAsync(request);
    return Results.Created($"/products/{product.Id}", product);
});

app.MapPut("/products/{id}", async (string id, UpdateProductRequest request, IProductService service) =>
{
    var product = await service.UpdateAsync(id, request);
    return product is not null ? Results.Ok(product) : Results.NotFound();
});

app.MapDelete("/products/{id}", async (string id, IProductService service) =>
{
    var success = await service.DeleteAsync(id);
    return success ? Results.NoContent() : Results.NotFound();
});

// NUEVOS ENDPOINTS PARA GESTIÓN DE IMÁGENES

app.MapPost("/products/{id}/images", async (string id, AddImageRequest request, IProductService service) =>
{
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
});

app.MapDelete("/products/{id}/images", async (string id, string imageUrl, IProductService service) =>
{
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
});

app.MapPut("/products/{id}/images/reorder", async (string id, ReorderImagesRequest request, IProductService service) =>
{
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
});

#endregion

#region "communities"

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

#endregion

#region "community-products"

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

#region "users"

app.MapPost("/auth/register", async (RegisterRequest request, IUserService service) =>
{
    if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
        return Results.BadRequest(new { message = "Email y contraseña son requeridos" });

    var user = await service.RegisterAsync(request);
    
    if (user == null)
        return Results.Conflict(new { message = "El email ya está registrado" });

    return Results.Created($"/users/{user.Id}", user);
});

app.MapPost("/auth/login", async (LoginRequest request, IUserService service) =>
{
    if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
        return Results.BadRequest(new { message = "Email y contraseña son requeridos" });

    var user = await service.LoginAsync(request);
    
    if (user == null)
        return Results.Unauthorized();

    return Results.Ok(user);
});

app.MapGet("/users/{id}", async (string id, IUserService service) =>
{
    var user = await service.GetByIdAsync(id);
    return user is not null ? Results.Ok(user) : Results.NotFound();
});

app.MapGet("/users/email/{email}", async (string email, IUserService service) =>
{
    var user = await service.GetByEmailAsync(email);
    return user is not null ? Results.Ok(user) : Results.NotFound();
});

app.MapPut("/users/{id}", async (string id, UpdateUserRequest request, IUserService service) =>
{
    var user = await service.UpdateUserAsync(id, request);
    return user is not null ? Results.Ok(user) : Results.NotFound();
});

app.MapPost("/users/{id}/change-password", async (string id, ChangePasswordRequest request, IUserService service) =>
{
    if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword))
        return Results.BadRequest(new { message = "Las contraseñas son requeridas" });

    var success = await service.ChangePasswordAsync(id, request);
    
    if (!success)
        return Results.BadRequest(new { message = "Contraseña actual incorrecta" });

    return Results.Ok(new { message = "Contraseña actualizada correctamente" });
});

app.MapDelete("/users/{id}", async (string id, IUserService service) =>
{
    var success = await service.DeleteUserAsync(id);
    return success ? Results.NoContent() : Results.NotFound();
});

#endregion

#region "stores"

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

app.MapGet("/stores/user/{userId}", async (string userId, IStoreService service) =>
{
    var stores = await service.GetByUserIdAsync(userId);
    return Results.Ok(stores);
});

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

app.MapPost("/stores", async (CreateStoreRequest request, IStoreService service) =>
{
    if (string.IsNullOrEmpty(request.Name) || string.IsNullOrEmpty(request.LinkStore))
        return Results.BadRequest(new { message = "Name y LinkStore son requeridos" });

    try
    {
        var store = await service.CreateAsync(request);
        return Results.Created($"/stores/{store.Id}", store);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
});

app.MapPut("/stores/{id}", async (string id, UpdateStoreRequest request, IStoreService service) =>
{
    var store = await service.UpdateAsync(id, request);
    return store is not null ? Results.Ok(store) : Results.NotFound();
});

app.MapDelete("/stores/{id}", async (string id, IStoreService service) =>
{
    var success = await service.DeleteAsync(id);
    return success ? Results.NoContent() : Results.NotFound();
});

app.MapPost("/stores/{storeId}/users", async (string storeId, AddUserToStoreRequest request, IStoreService service) =>
{
    if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.Role))
        return Results.BadRequest(new { message = "UserId y Role son requeridos" });

    var success = await service.AddUserToStoreAsync(storeId, request.UserId, request.Role);
    return success ? Results.Ok(new { message = "Usuario agregado a la tienda" }) : Results.BadRequest(new { message = "No se pudo agregar el usuario" });
});

app.MapDelete("/stores/{storeId}/users/{userId}", async (string storeId, string userId, IStoreService service) =>
{
    var success = await service.RemoveUserFromStoreAsync(storeId, userId);
    return success ? Results.NoContent() : Results.NotFound();
});

#endregion

#region "blob-storage"

app.MapPost("/images/upload", async (HttpRequest request, IBlobStorageService service) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { message = "El contenido debe ser multipart/form-data" });

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    var folder = form["folder"].ToString();
    var entityId = form["entityId"].ToString();
    var uploadedBy = form["uploadedBy"].ToString();

    if (file == null || file.Length == 0)
        return Results.BadRequest(new { message = "No se ha enviado ningún archivo" });

    if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(entityId))
        return Results.BadRequest(new { message = "Folder y EntityId son requeridos" });

    // Validar tipo de archivo (solo imágenes)
    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    var fileExtension = Path.GetExtension(file.FileName).ToLower();
    if (!allowedExtensions.Contains(fileExtension))
        return Results.BadRequest(new { message = "Solo se permiten archivos de imagen (jpg, jpeg, png, gif, webp)" });

    // Validar tamaño (máximo 5MB)
    if (file.Length > 5 * 1024 * 1024)
        return Results.BadRequest(new { message = "El archivo no puede superar los 5MB" });

    var uploadRequest = new ImageUploadRequest
    {
        Folder = folder,
        EntityId = entityId,
        UploadedBy = string.IsNullOrEmpty(uploadedBy) ? null : uploadedBy
    };

    using var stream = file.OpenReadStream();
    var result = await service.UploadImageAsync(stream, file.FileName, file.ContentType, uploadRequest);

    return Results.Created($"/images/{result.Id}", result);
});

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

app.MapDelete("/images/{id}", async (string id, IBlobStorageService service) =>
{
    var success = await service.DeleteImageAsync(id);
    return success ? Results.NoContent() : Results.NotFound();
});

app.MapDelete("/images/by-url", async (string blobUrl, string entityId, IBlobStorageService service) =>
{
    if (string.IsNullOrEmpty(blobUrl) || string.IsNullOrEmpty(entityId))
        return Results.BadRequest(new { message = "blobUrl y entityId son requeridos" });

    var success = await service.DeleteImageByUrlAsync(blobUrl, entityId);
    return success ? Results.NoContent() : Results.NotFound();
});
#endregion

#region "categories"

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

app.MapPost("/categories", async (CreateCategoryRequest request, ICategoryService service) =>
{
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
});

app.MapPut("/categories/{id}", async (string id, UpdateCategoryRequest request, ICategoryService service) =>
{
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
});

app.MapDelete("/categories/{id}", async (string id, ICategoryService service) =>
{
    var success = await service.DeleteAsync(id);
    return success ? Results.NoContent() : Results.NotFound();
});

#endregion

#region "product-synchronize"

app.MapPost("/products/{id}/synchronize", async (string id, IProductSynchronizeService service) =>
{
    var success = await service.SynchronizeProductAsync(id);
    
    if (!success)
        return Results.NotFound(new { message = "Producto no encontrado o no tiene tienda/comunidad asociada" });

    return Results.Ok(new { message = "Producto sincronizado correctamente", productId = id });
});

app.MapPost("/products/synchronize/all", async (IProductSynchronizeService service) =>
{
    var count = await service.SynchronizeAllProductsAsync();
    return Results.Ok(new { message = $"Se sincronizaron {count} productos", totalSynchronized = count });
});

app.MapPost("/products/synchronize/store/{storeId}", async (string storeId, IProductSynchronizeService service) =>
{
    var count = await service.SynchronizeProductsByStoreAsync(storeId);
    return Results.Ok(new { message = $"Se sincronizaron {count} productos de la tienda", totalSynchronized = count });
});

#endregion

app.Run();

// DTO adicional para agregar usuarios a tienda
public record AddUserToStoreRequest(string UserId, string Role);