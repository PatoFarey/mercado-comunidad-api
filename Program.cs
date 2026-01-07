using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Services;

var builder = WebApplication.CreateBuilder(args);

// Configurar MongoDB
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

// Registrar los servicios
builder.Services.AddSingleton<IPublicacionService, PublicacionService>();
builder.Services.AddSingleton<ICommunityService, CommunityService>();

// Configurar CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:3001", "https://mercadocomunidad.cl")
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

#region "publicacions"

app.MapGet("/publications", async (IPublicacionService service, int pageNumber = 1, int pageSize = 10) =>
{
    var result = await service.GetPaginatedAsync(pageNumber, pageSize);
    return Results.Ok(result);
});

app.MapGet("/publications/{id}", async (string id, IPublicacionService service) =>
{
    var publicacion = await service.GetByIdAsync(id);
    return publicacion is not null ? Results.Ok(publicacion) : Results.NotFound();
});

app.MapGet("/publications/categoria/{categoria}", async (string categoria, IPublicacionService service, int pageNumber = 1, int pageSize = 10) =>
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

app.Run();
