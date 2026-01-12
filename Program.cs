using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Services;
using ApiMercadoComunidad.Models.DTOs;

var builder = WebApplication.CreateBuilder(args);

// Configurar MongoDB
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

// Registrar los servicios
builder.Services.AddSingleton<IProductService, ProductService>();
builder.Services.AddSingleton<ICommunityService, CommunityService>();
builder.Services.AddSingleton<ICommunityProductService, CommunityProductService>();
builder.Services.AddSingleton<IUserService, UserService>();

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
    var products = await service.GetByIdAsync(id);
    return products is not null ? Results.Ok(products) : Results.NotFound();
}); 

app.MapGet("/products/categoria/{categoria}", async (string categoria, IProductService service, int pageNumber = 1, int pageSize = 10) =>
{
    var result = await service.GetByCategoriaAsync(categoria, pageNumber, pageSize);
    return Results.Ok(result);
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

app.Run();
