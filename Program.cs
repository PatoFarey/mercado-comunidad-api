using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Services;

var builder = WebApplication.CreateBuilder(args);

// Configurar MongoDB
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

// Registrar los servicios
builder.Services.AddSingleton<IProductService, ProductService>();
builder.Services.AddSingleton<ICommunityService, CommunityService>();
builder.Services.AddSingleton<ICommunityProductService, CommunityProductService>();

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

app.Run();
