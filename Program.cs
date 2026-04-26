using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Models;
using ApiMercadoComunidad.Models.DTOs;
using ApiMercadoComunidad.Security;
using ApiMercadoComunidad.Services;
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Xml.Linq;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

builder.Services.Configure<R2StorageSettings>(
    builder.Configuration.GetSection("R2StorageSettings"));

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

builder.Services.Configure<TurnstileSettings>(
    builder.Configuration.GetSection("Turnstile"));

builder.Services.Configure<PaykuSettings>(
    builder.Configuration.GetSection("Payku"));

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("La sección Jwt es requerida.");

if (string.IsNullOrWhiteSpace(jwtSettings.Key))
    throw new InvalidOperationException("Jwt:Key es requerido.");

if (string.IsNullOrWhiteSpace(jwtSettings.Issuer) || string.IsNullOrWhiteSpace(jwtSettings.Audience))
    throw new InvalidOperationException("Jwt:Issuer y Jwt:Audience son requeridos.");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key));

builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MongoDbSettings>>().Value;
    var client = new MongoClient(settings.ConnectionString);
    return client.GetDatabase(settings.DatabaseName);
});

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
builder.Services.AddSingleton<ISalesService, SalesService>();
builder.Services.AddSingleton<IMetricsService, MetricsService>();
builder.Services.AddSingleton<IPlanService, PlanService>();
builder.Services.AddSingleton<IOgImageService, OgImageService>();
builder.Services.AddSingleton<IUserSubscriptionService, UserSubscriptionService>();
builder.Services.AddHttpClient<IPaykuService, PaykuService>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddHttpClient<ITurnstileService, TurnstileService>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(8);
});
builder.Services.AddHttpClient("og", c =>
{
    c.Timeout = TimeSpan.FromSeconds(6);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("FeriaComunidad-OG/1.0");
});

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

var corsOrigins = new List<string>
{
    "http://localhost:3000",
    "http://localhost:3001",
    "http://localhost:5173",
    "https://localhost:5173",
    "https://feriacomunidad.cl",
};

var extraOrigins = builder.Configuration["Cors:AllowedOrigins"];
if (!string.IsNullOrWhiteSpace(extraOrigins))
    corsOrigins.AddRange(extraOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (corsOrigins.Contains(origin)) return true;
                // Permitir todos los subdominios de feriacomunidad.cl
                if (Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
                    (uri.Host == "feriacomunidad.cl" || uri.Host.EndsWith(".feriacomunidad.cl")) &&
                    uri.Scheme == "https")
                    return true;
                return false;
            })
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

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0",
    environment = app.Environment.EnvironmentName,
    timestamp = DateTime.UtcNow,
}));

app.MapGet("/sitemap.xml", async (IMongoDatabase db, IConfiguration config, HttpContext ctx) =>
{
    var frontendUrl = (config["FrontendUrl"] ?? "https://feriacomunidad.cl").TrimEnd('/');

    var storesCollection = db.GetCollection<Store>("stores");
    var productsCollection = db.GetCollection<Products>("products");

    var activeStores = await storesCollection
        .Find(s => s.Active)
        .Project(s => new { s.Id, s.LinkStore, s.UpdatedAt })
        .ToListAsync();

    var activeStoreIds = activeStores
        .Select(s => s.Id)
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .ToList();

    var activeProductsFilter = activeStoreIds.Count > 0
        ? Builders<Products>.Filter.And(
            Builders<Products>.Filter.Eq(p => p.Active, true),
            Builders<Products>.Filter.In(p => p.IdStore, activeStoreIds))
        : Builders<Products>.Filter.Eq(p => p.Active, true);

    var activeProducts = await productsCollection
        .Find(activeProductsFilter)
        .Project(p => new { p.Id, p.UpdatedAt })
        .ToListAsync();

    var entries = new List<(string Url, DateTime LastMod)>
    {
        ($"{frontendUrl}/", DateTime.UtcNow),
        ($"{frontendUrl}/communities", DateTime.UtcNow),
    };

    entries.AddRange(activeStores
        .Where(s => !string.IsNullOrWhiteSpace(s.LinkStore))
        .Select(s => ($"{frontendUrl}/store/{Uri.EscapeDataString(s.LinkStore.Trim().ToLowerInvariant())}", s.UpdatedAt)));

    entries.AddRange(activeProducts
        .Where(p => !string.IsNullOrWhiteSpace(p.Id))
        .Select(p => ($"{frontendUrl}/product/{p.Id}", p.UpdatedAt)));

    var dedupedEntries = entries
        .GroupBy(e => e.Url, StringComparer.OrdinalIgnoreCase)
        .Select(g => g.OrderByDescending(e => e.LastMod).First())
        .ToList();

    XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
    var xml = new XDocument(
        new XDeclaration("1.0", "UTF-8", "yes"),
        new XElement(ns + "urlset",
            dedupedEntries.Select(entry =>
                new XElement(ns + "url",
                    new XElement(ns + "loc", entry.Url),
                    new XElement(ns + "lastmod", entry.LastMod.ToUniversalTime().ToString("yyyy-MM-dd")),
                    new XElement(ns + "changefreq", "daily"),
                    new XElement(ns + "priority", entry.Url.Contains("/product/") ? "0.7" : entry.Url.Contains("/store/") ? "0.8" : "1.0")
                )
            )
        )
    );

    ctx.Response.Headers.CacheControl = "public,max-age=900";
    return Results.Content(xml.ToString(), "application/xml; charset=utf-8");
});

app.MapGet("/og/product/{id}", async (string id, IOgImageService ogService, HttpContext ctx) =>
{
    try
    {
        var png = await ogService.GenerateProductOgImageAsync(id);
        ctx.Response.Headers["Cache-Control"] = "public, max-age=3600";
        return Results.Bytes(png, "image/png");
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch
    {
        return Results.StatusCode(500);
    }
});

app.MapGet("/og/html/product/{id}", async (string id, IOgImageService ogService, IConfiguration config, HttpRequest req) =>
{
    try
    {
        var meta = await ogService.GetProductMetaAsync(id);
        var frontendUrl = config["FrontendUrl"]?.TrimEnd('/') ?? "https://feriacomunidad.cl";
        var apiBase = $"{req.Scheme}://{req.Host}";
        var productUrl = $"{frontendUrl}/product/{id}";
        var imageUrl = $"{apiBase}/og/product/{id}";

        var title = System.Net.WebUtility.HtmlEncode(meta.Title);
        var description = System.Net.WebUtility.HtmlEncode(meta.Description);

        var html = $"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="UTF-8" />
              <title>{title} · FeriaComunidad</title>
              <meta property="og:type" content="product" />
              <meta property="og:site_name" content="FeriaComunidad" />
              <meta property="og:title" content="{title}" />
              <meta property="og:description" content="{description}" />
              <meta property="og:image" content="{imageUrl}" />
              <meta property="og:image:width" content="1200" />
              <meta property="og:image:height" content="630" />
              <meta property="og:url" content="{productUrl}" />
              <meta name="twitter:card" content="summary_large_image" />
              <meta name="twitter:title" content="{title}" />
              <meta name="twitter:description" content="{description}" />
              <meta name="twitter:image" content="{imageUrl}" />
              <meta http-equiv="refresh" content="0;url={productUrl}" />
            </head>
            <body>
              <p><a href="{productUrl}">{title}</a></p>
              <script>window.location.replace('{productUrl}');</script>
            </body>
            </html>
            """;

        return Results.Content(html, "text/html; charset=utf-8");
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch
    {
        return Results.StatusCode(500);
    }
});

bool IsAuthenticated(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

IResult UnauthorizedResult() => Results.Unauthorized();

bool CanAccessEmail(ClaimsPrincipal user, string email)
{
    var currentEmail = user.FindFirstValue(ClaimTypes.Email);
    return AuthorizationHelpers.IsAdmin(user) ||
           (!string.IsNullOrWhiteSpace(currentEmail) &&
            string.Equals(currentEmail, email, StringComparison.OrdinalIgnoreCase));
}

bool IsSellerUser(ClaimsPrincipal user) => user.IsInRole(UserRoles.Seller);

int ResolveStoreLimit(Plan plan)
{
    if (plan.Limits.Stores == -1 || plan.Limits.Stores > 0)
        return plan.Limits.Stores;

    if (plan.Tier.Equals("free", StringComparison.OrdinalIgnoreCase))
        return 1;

    if (plan.Tier.Equals("pro", StringComparison.OrdinalIgnoreCase))
        return 3;

    return -1;
}

int ResolvePlanLimit(int rawLimit) => rawLimit == 0 ? -1 : rawLimit;

async Task<(bool Allowed, string Message)> CheckStoreCreationLimitAsync(
    ClaimsPrincipal user,
    IPlanService planService,
    IMongoDatabase db)
{
    if (!IsSellerUser(user))
        return (true, string.Empty);

    var currentUserId = AuthorizationHelpers.GetCurrentUserId(user);
    if (string.IsNullOrWhiteSpace(currentUserId))
        return (false, "No se pudo identificar el usuario.");

    var plan = await planService.GetEffectivePlanForUserAsync(currentUserId);
    if (plan == null)
        return (true, string.Empty);

    var storesLimit = ResolveStoreLimit(plan);
    if (storesLimit < 0)
        return (true, string.Empty);

    var storesCollection = db.GetCollection<Store>("stores");
    var storesCountFilter = Builders<Store>.Filter.And(
        Builders<Store>.Filter.ElemMatch(s => s.Users, u => u.UserID == currentUserId),
        Builders<Store>.Filter.Eq(s => s.Active, true)
    );
    var currentStores = await storesCollection.CountDocumentsAsync(storesCountFilter);

    return currentStores >= storesLimit
        ? (false, $"Tu plan actual permite hasta {storesLimit} tienda(s).")
        : (true, string.Empty);
}

async Task<(bool Allowed, string Message)> CheckProductCreationLimitAsync(
    ClaimsPrincipal user,
    IPlanService planService,
    IMongoDatabase db)
{
    if (!IsSellerUser(user))
        return (true, string.Empty);

    var currentUserId = AuthorizationHelpers.GetCurrentUserId(user);
    if (string.IsNullOrWhiteSpace(currentUserId))
        return (false, "No se pudo identificar el usuario.");

    var plan = await planService.GetEffectivePlanForUserAsync(currentUserId);
    if (plan == null)
        return (true, string.Empty);

    var productsLimit = ResolvePlanLimit(plan.Limits.Products);
    if (productsLimit < 0)
        return (true, string.Empty);

    var storesCollection = db.GetCollection<Store>("stores");
    var productsCollection = db.GetCollection<Products>("products");

    var userStoreIds = await storesCollection
        .Find(Builders<Store>.Filter.And(
            Builders<Store>.Filter.ElemMatch(s => s.Users, u => u.UserID == currentUserId),
            Builders<Store>.Filter.Eq(s => s.Active, true)))
        .Project(s => s.Id)
        .ToListAsync();

    if (userStoreIds.Count == 0)
        return (true, string.Empty);

    var productsFilter = Builders<Products>.Filter.And(
        Builders<Products>.Filter.In(p => p.IdStore, userStoreIds),
        Builders<Products>.Filter.Eq(p => p.Active, true)
    );
    var currentProducts = await productsCollection.CountDocumentsAsync(productsFilter);

    return currentProducts >= productsLimit
        ? (false, $"Tu plan actual permite hasta {productsLimit} producto(s).")
        : (true, string.Empty);
}

async Task<(bool Allowed, string Message)> CheckProductImagesLimitAsync(
    ClaimsPrincipal user,
    string productId,
    int targetImagesCount,
    IProductService productService,
    IPlanService planService)
{
    if (!IsSellerUser(user))
        return (true, string.Empty);

    var currentUserId = AuthorizationHelpers.GetCurrentUserId(user);
    if (string.IsNullOrWhiteSpace(currentUserId))
        return (false, "No se pudo identificar el usuario.");

    var plan = await planService.GetEffectivePlanForUserAsync(currentUserId);
    if (plan == null)
        return (true, string.Empty);

    var imagesPerProductLimit = ResolvePlanLimit(plan.Limits.ImagesPerProduct);
    if (imagesPerProductLimit < 0)
        return (true, string.Empty);

    var product = await productService.GetByIdAsync(productId);
    if (product == null)
        return (false, "Producto no encontrado.");

    return targetImagesCount > imagesPerProductLimit
        ? (false, $"Tu plan permite hasta {imagesPerProductLimit} imagen(es) por producto.")
        : (true, string.Empty);
}

async Task<(bool Allowed, string Message)> CheckCommunityCreationLimitAsync(
    ClaimsPrincipal user,
    IPlanService planService,
    IMongoDatabase db)
{
    if (!AuthorizationHelpers.IsAdmin(user))
        return (true, string.Empty);

    var currentUserId = AuthorizationHelpers.GetCurrentUserId(user);
    if (string.IsNullOrWhiteSpace(currentUserId))
        return (false, "No se pudo identificar el usuario.");

    var plan = await planService.GetEffectivePlanForCommunityAdminAsync(currentUserId);
    if (plan == null)
        return (true, string.Empty);

    var limit = plan.Limits.CommunitiesCreate == 0 ? -1 : plan.Limits.CommunitiesCreate;
    if (limit < 0)
        return (true, string.Empty);

    var communitiesCol = db.GetCollection<Community>("communities");
    var current = await communitiesCol.CountDocumentsAsync(
        Builders<Community>.Filter.And(
            Builders<Community>.Filter.Eq(c => c.OwnerUserId, currentUserId),
            Builders<Community>.Filter.Eq(c => c.Active, true)));

    return current >= limit
        ? (false, $"Tu plan permite crear hasta {limit} feria(s)/comunidad(es).")
        : (true, string.Empty);
}

async Task<(bool Allowed, string Message)> CheckSellersPerCommunityLimitAsync(
    string communityId,
    IMongoDatabase db)
{
    var communitiesCol = db.GetCollection<Community>("communities");
    var community = await communitiesCol
        .Find(Builders<Community>.Filter.Eq(c => c.Id, communityId))
        .FirstOrDefaultAsync();

    if (community == null || string.IsNullOrWhiteSpace(community.OwnerUserId))
        return (true, string.Empty);

    // Get owner's plan
    var usersCol = db.GetCollection<User>("users");
    var owner = await usersCol
        .Find(Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.Id, community.OwnerUserId),
            Builders<User>.Filter.Eq(u => u.IsActive, true)))
        .FirstOrDefaultAsync();

    if (owner == null)
        return (true, string.Empty);

    var plansCol = db.GetCollection<Plan>("plans");
    Plan? plan = null;

    if (!string.IsNullOrWhiteSpace(owner.PlanId))
    {
        plan = await plansCol
            .Find(Builders<Plan>.Filter.And(
                Builders<Plan>.Filter.Eq(p => p.Id, owner.PlanId),
                Builders<Plan>.Filter.Eq(p => p.Active, true),
                Builders<Plan>.Filter.Eq(p => p.Type, "admin")))
            .FirstOrDefaultAsync();
    }

    if (plan == null)
    {
        plan = await plansCol
            .Find(Builders<Plan>.Filter.And(
                Builders<Plan>.Filter.Eq(p => p.Active, true),
                Builders<Plan>.Filter.Eq(p => p.Type, "admin"),
                Builders<Plan>.Filter.Eq(p => p.Tier, "free")))
            .SortBy(p => p.PriceClp)
            .FirstOrDefaultAsync();
    }

    if (plan == null)
        return (true, string.Empty);

    var limit = plan.Limits.SellersPerCommunity == 0 ? -1 : plan.Limits.SellersPerCommunity;
    if (limit < 0)
        return (true, string.Empty);

    var communityStoresCol = db.GetCollection<CommunityStore>("community_stores");
    var current = await communityStoresCol.CountDocumentsAsync(
        Builders<CommunityStore>.Filter.And(
            Builders<CommunityStore>.Filter.Eq(cs => cs.CommunityId, communityId),
            Builders<CommunityStore>.Filter.Eq(cs => cs.Status, true)));

    return current >= limit
        ? (false, $"Esta feria/comunidad alcanzó el límite de {limit} tienda(s) permitidas por su plan.")
        : (true, string.Empty);
}

async Task<(bool Allowed, string Message)> CheckCommunitiesJoinLimitAsync(
    ClaimsPrincipal user,
    string communityId,
    bool includePendingRequests,
    IPlanService planService,
    IMongoDatabase db)
{
    if (!IsSellerUser(user))
        return (true, string.Empty);

    var currentUserId = AuthorizationHelpers.GetCurrentUserId(user);
    if (string.IsNullOrWhiteSpace(currentUserId))
        return (false, "No se pudo identificar el usuario.");

    var plan = await planService.GetEffectivePlanForUserAsync(currentUserId);
    if (plan == null)
        return (true, string.Empty);

    var communitiesLimit = ResolvePlanLimit(plan.Limits.CommunitiesJoin);
    if (communitiesLimit < 0)
        return (true, string.Empty);

    var storesCollection = db.GetCollection<Store>("stores");
    var communityStoresCollection = db.GetCollection<CommunityStore>("community_stores");
    var communityRequestsCollection = db.GetCollection<CommunityRequest>("community_requests");

    var userStoreIds = await storesCollection
        .Find(Builders<Store>.Filter.And(
            Builders<Store>.Filter.ElemMatch(s => s.Users, u => u.UserID == currentUserId),
            Builders<Store>.Filter.Eq(s => s.Active, true)))
        .Project(s => s.Id)
        .ToListAsync();

    if (userStoreIds.Count == 0)
        return (true, string.Empty);

    var activeCommunityIds = await communityStoresCollection
        .Find(Builders<CommunityStore>.Filter.And(
            Builders<CommunityStore>.Filter.In(cs => cs.StoreId, userStoreIds),
            Builders<CommunityStore>.Filter.Eq(cs => cs.Status, true)))
        .Project(cs => cs.CommunityId)
        .ToListAsync();

    var trackedCommunityIds = new HashSet<string>(activeCommunityIds.Where(id => !string.IsNullOrWhiteSpace(id)));

    if (includePendingRequests)
    {
        var pendingCommunityIds = await communityRequestsCollection
            .Find(Builders<CommunityRequest>.Filter.And(
                Builders<CommunityRequest>.Filter.In(cr => cr.StoreId, userStoreIds),
                Builders<CommunityRequest>.Filter.Eq(cr => cr.Status, "pending")))
            .Project(cr => cr.CommunityId)
            .ToListAsync();

        foreach (var pendingCommunityId in pendingCommunityIds.Where(id => !string.IsNullOrWhiteSpace(id)))
            trackedCommunityIds.Add(pendingCommunityId);
    }

    var willIncrease = !trackedCommunityIds.Contains(communityId);

    return willIncrease && trackedCommunityIds.Count >= communitiesLimit
        ? (false, $"Tu plan permite hasta {communitiesLimit} feria(s)/comunidad(es).")
        : (true, string.Empty);
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

app.MapGet("/products/latest", async (IProductService service, int limit = 8) =>
{
    var products = await service.GetLatestAsync(limit);
    return Results.Ok(products);
});

app.MapPost("/products", async (CreateProductRequest request, ClaimsPrincipal user, IProductService service, IStoreService storeService, IPlanService planService, IMongoDatabase db) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (string.IsNullOrEmpty(request.Title) || string.IsNullOrEmpty(request.IdStore))
        return Results.BadRequest(new { message = "Title y IdStore son requeridos" });

    if (!await AuthorizationHelpers.CanManageStoreAsync(request.IdStore, user, storeService))
        return Results.Forbid();

    var productLimitCheck = await CheckProductCreationLimitAsync(user, planService, db);
    if (!productLimitCheck.Allowed)
        return Results.BadRequest(new { message = productLimitCheck.Message, code = "PLAN_LIMIT_PRODUCTS" });

    if (IsSellerUser(user))
    {
        var currentUserId = AuthorizationHelpers.GetCurrentUserId(user)!;
        var plan = await planService.GetEffectivePlanForUserAsync(currentUserId);
        if (plan != null)
        {
            var imagesPerProductLimit = ResolvePlanLimit(plan.Limits.ImagesPerProduct);
            if (imagesPerProductLimit > -1 && request.Images.Count > imagesPerProductLimit)
                return Results.BadRequest(new { message = $"Tu plan permite hasta {imagesPerProductLimit} imagen(es) por producto.", code = "PLAN_LIMIT_IMAGES" });
        }
    }

    var product = await service.CreateAsync(request);
    return Results.Created($"/products/{product.Id}", product);
}).RequireAuthorization();

app.MapPut("/products/{id}", async (string id, UpdateProductRequest request, ClaimsPrincipal user, IProductService service, IStoreService storeService, IPlanService planService, IProductSynchronizeService syncService) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!await AuthorizationHelpers.CanManageProductAsync(id, user, service, storeService))
        return Results.Forbid();

    if (request.Images != null)
    {
        var imagesLimitCheck = await CheckProductImagesLimitAsync(user, id, request.Images.Count, service, planService);
        if (!imagesLimitCheck.Allowed)
            return Results.BadRequest(new { message = imagesLimitCheck.Message, code = "PLAN_LIMIT_IMAGES" });
    }

    var product = await service.UpdateAsync(id, request);
    if (product is null) return Results.NotFound();

    _ = Task.Run(() => syncService.SynchronizeProductAsync(id));
    return Results.Ok(product);
}).RequireAuthorization();

app.MapDelete("/products/{id}", async (string id, ClaimsPrincipal user, IProductService service, IStoreService storeService, IProductSynchronizeService syncService) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!await AuthorizationHelpers.CanManageProductAsync(id, user, service, storeService))
        return Results.Forbid();

    var success = await service.DeleteAsync(id);
    if (!success) return Results.NotFound();

    _ = Task.Run(() => syncService.SynchronizeProductAsync(id));
    return Results.NoContent();
}).RequireAuthorization();

app.MapPost("/products/{id}/images", async (string id, AddImageRequest request, ClaimsPrincipal user, IProductService service, IStoreService storeService, IPlanService planService, IProductSynchronizeService syncService) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!await AuthorizationHelpers.CanManageProductAsync(id, user, service, storeService))
        return Results.Forbid();

    if (string.IsNullOrEmpty(request.ImageUrl))
        return Results.BadRequest(new { message = "ImageUrl es requerida" });

    var product = await service.GetByIdAsync(id);
    if (product == null)
        return Results.NotFound();

    var targetImagesCount = product.Images.Count + 1;
    var imagesLimitCheck = await CheckProductImagesLimitAsync(user, id, targetImagesCount, service, planService);
    if (!imagesLimitCheck.Allowed)
        return Results.BadRequest(new { message = imagesLimitCheck.Message, code = "PLAN_LIMIT_IMAGES" });

    try
    {
        var updated = await service.AddImageAsync(id, request.ImageUrl);
        if (updated is null) return Results.NotFound();
        _ = Task.Run(() => syncService.SynchronizeProductAsync(id));
        return Results.Ok(updated);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
}).RequireAuthorization();

app.MapDelete("/products/{id}/images", async (string id, string imageUrl, ClaimsPrincipal user, IProductService service, IStoreService storeService, IProductSynchronizeService syncService) =>
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
        if (product is null) return Results.NotFound();
        _ = Task.Run(() => syncService.SynchronizeProductAsync(id));
        return Results.Ok(product);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
}).RequireAuthorization();

app.MapPut("/products/{id}/images/reorder", async (string id, ReorderImagesRequest request, ClaimsPrincipal user, IProductService service, IStoreService storeService, IPlanService planService, IProductSynchronizeService syncService) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!await AuthorizationHelpers.CanManageProductAsync(id, user, service, storeService))
        return Results.Forbid();

    if (request.Images == null || !request.Images.Any())
        return Results.BadRequest(new { message = "Images es requerido y no puede estar vacío" });

    var imagesLimitCheck = await CheckProductImagesLimitAsync(user, id, request.Images.Count, service, planService);
    if (!imagesLimitCheck.Allowed)
        return Results.BadRequest(new { message = imagesLimitCheck.Message, code = "PLAN_LIMIT_IMAGES" });

    try
    {
        var product = await service.ReorderImagesAsync(id, request.Images);
        if (product is null) return Results.NotFound();
        _ = Task.Run(() => syncService.SynchronizeProductAsync(id));
        return Results.Ok(product);
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
    var communities = await service.GetAllWithStatsAsync();
    return Results.Ok(communities);
});

app.MapGet("/communities/{id}", async (string id, ICommunityService service) =>
{
    var community = await service.GetByIdAsync(id);
    return community is not null ? Results.Ok(community) : Results.NotFound();
});

app.MapGet("/communities/by-community-id/{communityId}", async (string communityId, ICommunityService service, IUserService userService, IPlanService planService) =>
{
    var community = await service.GetByCommunityIdAsync(communityId);
    if (community is null) return Results.NotFound();

    var planTier = "free";
    if (!string.IsNullOrWhiteSpace(community.OwnerUserId))
    {
        var owner = await userService.GetByIdAsync(community.OwnerUserId);
        if (owner?.PlanId is not null)
        {
            var plan = await planService.GetByIdAsync(owner.PlanId);
            if (plan is not null)
                planTier = plan.Tier;
        }
    }

    return Results.Ok(new
    {
        community.Id,
        community.CommunityId,
        community.Name,
        community.Title,
        community.Description,
        community.Phone,
        community.Email,
        community.Open,
        community.Active,
        community.Visible,
        community.Logo,
        community.OwnerUserId,
        community.CreatedAt,
        community.UpdatedAt,
        OwnerPlanTier = planTier,
    });
});

app.MapGet("/communities/active", async (ICommunityService service) =>
{
    var communities = await service.GetActiveCommunitiesAsync();
    return Results.Ok(communities);
});

app.MapGet("/communities/visible", async (ICommunityService service) =>
{
    var communities = await service.GetVisibleWithStatsAsync();
    return Results.Ok(communities);
});

app.MapGet("/admin/communities/my", async (ClaimsPrincipal user, ICommunityService service) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!AuthorizationHelpers.IsAdmin(user))
        return Results.Forbid();

    var currentUserId = AuthorizationHelpers.GetCurrentUserId(user);
    var currentEmail = user.FindFirstValue(ClaimTypes.Email);
    var includeAll = AuthorizationHelpers.IsSuperAdmin(user);

    var communities = await service.GetByOwnerWithStatsAsync(currentUserId, currentEmail, includeAll);
    return Results.Ok(communities);
}).RequireAuthorization();

app.MapPost("/admin/communities", async (CreateCommunityRequest request, ClaimsPrincipal user, ICommunityService service, IPlanService planService, IMongoDatabase db) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!AuthorizationHelpers.IsAdmin(user))
        return Results.Forbid();

    if (string.IsNullOrWhiteSpace(request.Name))
        return Results.BadRequest(new { message = "Name es requerido." });

    var communityCreationCheck = await CheckCommunityCreationLimitAsync(user, planService, db);
    if (!communityCreationCheck.Allowed)
        return Results.BadRequest(new { message = communityCreationCheck.Message, code = "PLAN_LIMIT_COMMUNITIES_CREATE" });

    var currentUserId = AuthorizationHelpers.GetCurrentUserId(user);
    var currentEmail = user.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

    static string Slugify(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        var chars = normalized.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        var slug = new string(chars);
        while (slug.Contains("--"))
            slug = slug.Replace("--", "-");
        return slug.Trim('-');
    }

    var communityId = string.IsNullOrWhiteSpace(request.CommunityId)
        ? Slugify(request.Name)
        : Slugify(request.CommunityId);

    if (string.IsNullOrWhiteSpace(communityId))
        return Results.BadRequest(new { message = "CommunityId inválido." });

    var existingCommunity = await service.GetByCommunityIdAsync(communityId);
    if (existingCommunity != null)
        return Results.Conflict(new { message = "El id de comunidad ya existe." });

    var community = new Community
    {
        CommunityId = communityId,
        Name = request.Name.Trim(),
        Title = string.IsNullOrWhiteSpace(request.Title) ? request.Name.Trim() : request.Title.Trim(),
        Description = request.Description?.Trim() ?? string.Empty,
        Phone = request.Phone?.Trim() ?? string.Empty,
        Email = string.IsNullOrWhiteSpace(request.Email) ? currentEmail : request.Email.Trim().ToLowerInvariant(),
        Open = request.Open,
        Active = request.Active,
        Visible = request.Visible,
        Logo = request.Logo?.Trim() ?? string.Empty,
        OwnerUserId = currentUserId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    var created = await service.CreateAsync(community);
    return Results.Created($"/communities/{created.Id}", created);
}).RequireAuthorization();

app.MapPut("/admin/communities/{id}", async (string id, UpdateCommunityRequest request, ClaimsPrincipal user, ICommunityService service) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!AuthorizationHelpers.IsAdmin(user))
        return Results.Forbid();

    var community = await service.GetByIdAsync(id);
    if (community == null)
        return Results.NotFound();

    var currentUserId = AuthorizationHelpers.GetCurrentUserId(user);
    var currentEmail = user.FindFirstValue(ClaimTypes.Email);
    var canManage = AuthorizationHelpers.IsSuperAdmin(user) ||
        (!string.IsNullOrWhiteSpace(currentUserId) && community.OwnerUserId == currentUserId) ||
        (string.IsNullOrWhiteSpace(community.OwnerUserId) &&
         !string.IsNullOrWhiteSpace(currentEmail) &&
         string.Equals(community.Email, currentEmail, StringComparison.OrdinalIgnoreCase));

    if (!canManage)
        return Results.Forbid();

    if (!string.IsNullOrWhiteSpace(request.Name)) community.Name = request.Name.Trim();
    if (!string.IsNullOrWhiteSpace(request.Title)) community.Title = request.Title.Trim();
    if (request.Description != null) community.Description = request.Description.Trim();
    if (request.Phone != null) community.Phone = request.Phone.Trim();
    if (!string.IsNullOrWhiteSpace(request.Email)) community.Email = request.Email.Trim().ToLowerInvariant();
    if (request.Open.HasValue) community.Open = request.Open.Value;
    if (request.Active.HasValue) community.Active = request.Active.Value;
    if (request.Visible.HasValue) community.Visible = request.Visible.Value;
    if (request.Logo != null) community.Logo = request.Logo.Trim();
    if (!string.IsNullOrWhiteSpace(request.CommunityId)) community.CommunityId = request.CommunityId.Trim().ToLowerInvariant();
    community.UpdatedAt = DateTime.UtcNow;

    await service.UpdateAsync(id, community);
    return Results.Ok(community);
}).RequireAuthorization();

app.MapGet("/admin/communities/{id}/stores", async (string id, ClaimsPrincipal user, ICommunityService communityService, IMongoDatabase db) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!AuthorizationHelpers.IsAdmin(user))
        return Results.Forbid();

    var community = await communityService.GetByIdAsync(id);
    if (community == null)
        return Results.NotFound(new { message = "Comunidad no encontrada." });

    // Only the owner or a super admin can see stores
    var currentUserId = AuthorizationHelpers.GetCurrentUserId(user);
    if (!AuthorizationHelpers.IsSuperAdmin(user) && community.OwnerUserId != currentUserId)
        return Results.Forbid();

    var communityStoresCol = db.GetCollection<CommunityStore>("community_stores");
    var storesCol = db.GetCollection<Store>("stores");

    var communityStores = await communityStoresCol
        .Find(Builders<CommunityStore>.Filter.Eq(cs => cs.CommunityId, id))
        .ToListAsync();

    if (communityStores.Count == 0)
        return Results.Ok(new List<object>());

    var storeIds = communityStores.Select(cs => cs.StoreId).Distinct().ToList();
    var stores = await storesCol
        .Find(Builders<Store>.Filter.In(s => s.Id, storeIds))
        .ToListAsync();

    var storeMap = stores.ToDictionary(s => s.Id ?? string.Empty);

    var result = communityStores
        .Where(cs => storeMap.ContainsKey(cs.StoreId))
        .Select(cs =>
        {
            var store = storeMap[cs.StoreId];
            return new
            {
                id = store.Id,
                name = store.Name,
                linkStore = store.LinkStore,
                logo = store.Logo,
                active = store.Active,
                ownerEnabled = cs.OwnerEnabled,
                sellerEnabled = cs.SellerEnabled,
                memberStatus = cs.OwnerEnabled && cs.SellerEnabled,
                joinedAt = cs.CreatedAt,
            };
        })
        .OrderBy(s => s.name)
        .ToList();

    return Results.Ok(result);
}).RequireAuthorization();

app.MapPut("/admin/communities/{communityId}/stores/{storeId}/toggle", async (string communityId, string storeId, ClaimsPrincipal user, ICommunityService communityService, IMongoDatabase db) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!AuthorizationHelpers.IsAdmin(user))
        return Results.Forbid();

    var community = await communityService.GetByIdAsync(communityId);
    if (community == null)
        return Results.NotFound(new { message = "Comunidad no encontrada." });

    var currentUserId = AuthorizationHelpers.GetCurrentUserId(user);
    if (!AuthorizationHelpers.IsSuperAdmin(user) && community.OwnerUserId != currentUserId)
        return Results.Forbid();

    var col = db.GetCollection<CommunityStore>("community_stores");
    var filter = Builders<CommunityStore>.Filter.And(
        Builders<CommunityStore>.Filter.Eq(cs => cs.CommunityId, communityId),
        Builders<CommunityStore>.Filter.Eq(cs => cs.StoreId, storeId));

    var existing = await col.Find(filter).FirstOrDefaultAsync();
    if (existing == null)
        return Results.NotFound(new { message = "Relación no encontrada." });

    var newOwnerEnabled = !existing.OwnerEnabled;
    var newStatus = newOwnerEnabled && existing.SellerEnabled;
    await col.UpdateOneAsync(filter, Builders<CommunityStore>.Update
        .Set(cs => cs.OwnerEnabled, newOwnerEnabled)
        .Set(cs => cs.Status, newStatus)
        .Set(cs => cs.UpdatedAt, DateTime.UtcNow));

    return Results.Ok(new {
        ownerEnabled = newOwnerEnabled,
        sellerEnabled = existing.SellerEnabled,
        memberStatus = newStatus,
    });
}).RequireAuthorization();

app.MapDelete("/admin/communities/{id}", async (string id, ClaimsPrincipal user, ICommunityService service) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!AuthorizationHelpers.IsAdmin(user))
        return Results.Forbid();

    var community = await service.GetByIdAsync(id);
    if (community == null)
        return Results.NotFound();

    var currentUserId = AuthorizationHelpers.GetCurrentUserId(user);
    var currentEmail = user.FindFirstValue(ClaimTypes.Email);
    var canManage = AuthorizationHelpers.IsSuperAdmin(user) ||
        (!string.IsNullOrWhiteSpace(currentUserId) && community.OwnerUserId == currentUserId) ||
        (string.IsNullOrWhiteSpace(community.OwnerUserId) &&
         !string.IsNullOrWhiteSpace(currentEmail) &&
         string.Equals(community.Email, currentEmail, StringComparison.OrdinalIgnoreCase));

    if (!canManage)
        return Results.Forbid();

    await service.DeleteAsync(id);
    return Results.NoContent();
}).RequireAuthorization();

#endregion

#region Community Requests Management

// GET pending requests for communities owned by the current admin
app.MapGet("/admin/community-requests", async (ClaimsPrincipal user, IMongoDatabase db) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!AuthorizationHelpers.IsAdmin(user))
        return Results.Forbid();

    var currentUserId = AuthorizationHelpers.GetCurrentUserId(user);

    var communitiesCol = db.GetCollection<Community>("communities");
    var requestsCol = db.GetCollection<CommunityRequest>("community_requests");
    var storesCol = db.GetCollection<Store>("stores");

    // Get communities owned by this admin
    var ownedCommunities = AuthorizationHelpers.IsSuperAdmin(user)
        ? await communitiesCol.Find(FilterDefinition<Community>.Empty).ToListAsync()
        : await communitiesCol.Find(Builders<Community>.Filter.Eq(c => c.OwnerUserId, currentUserId)).ToListAsync();

    if (ownedCommunities.Count == 0)
        return Results.Ok(new List<object>());

    var communityIds = ownedCommunities.Select(c => c.Id).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();

    var requests = await requestsCol
        .Find(Builders<CommunityRequest>.Filter.And(
            Builders<CommunityRequest>.Filter.In(r => r.CommunityId, communityIds),
            Builders<CommunityRequest>.Filter.Eq(r => r.Status, "pending")))
        .SortByDescending(r => r.CreatedAt)
        .ToListAsync();

    if (requests.Count == 0)
        return Results.Ok(new List<object>());

    var storeIds = requests.Select(r => r.StoreId).Distinct().ToList();
    var stores = await storesCol
        .Find(Builders<Store>.Filter.In(s => s.Id, storeIds))
        .ToListAsync();

    var storeMap = stores.ToDictionary(s => s.Id ?? string.Empty);
    var communityMap = ownedCommunities.ToDictionary(c => c.Id ?? string.Empty);

    var result = requests.Select(r =>
    {
        storeMap.TryGetValue(r.StoreId, out var store);
        communityMap.TryGetValue(r.CommunityId, out var community);
        return new
        {
            id = r.Id,
            communityId = r.CommunityId,
            communityName = community?.Name ?? string.Empty,
            communityLogo = community?.Logo ?? string.Empty,
            storeId = r.StoreId,
            storeName = store?.Name ?? string.Empty,
            storeLogo = store?.Logo ?? string.Empty,
            storeLinkStore = store?.LinkStore ?? string.Empty,
            status = r.Status,
            message = r.Message,
            createdAt = r.CreatedAt,
        };
    }).ToList();

    return Results.Ok(result);
}).RequireAuthorization();

// PUT approve or reject a request
app.MapPut("/admin/community-requests/{requestId}", async (
    string requestId,
    ApproveRejectRequest body,
    ClaimsPrincipal user,
    IMongoDatabase db,
    IPlanService planService,
    IEmailService emailService) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!AuthorizationHelpers.IsAdmin(user))
        return Results.Forbid();

    if (body.Status != "approved" && body.Status != "rejected")
        return Results.BadRequest(new { message = "Status debe ser 'approved' o 'rejected'." });

    var requestsCol = db.GetCollection<CommunityRequest>("community_requests");
    var communitiesCol = db.GetCollection<Community>("communities");
    var communityStoresCol = db.GetCollection<CommunityStore>("community_stores");

    var request = await requestsCol
        .Find(Builders<CommunityRequest>.Filter.Eq(r => r.Id, requestId))
        .FirstOrDefaultAsync();

    if (request == null)
        return Results.NotFound();

    // Verify the admin owns this community
    var community = await communitiesCol
        .Find(Builders<Community>.Filter.Eq(c => c.Id, request.CommunityId))
        .FirstOrDefaultAsync();

    if (community == null)
        return Results.NotFound();

    var currentUserId = AuthorizationHelpers.GetCurrentUserId(user);
    var canManage = AuthorizationHelpers.IsSuperAdmin(user) ||
        community.OwnerUserId == currentUserId;

    if (!canManage)
        return Results.Forbid();

    // Update request status
    await requestsCol.UpdateOneAsync(
        Builders<CommunityRequest>.Filter.Eq(r => r.Id, requestId),
        Builders<CommunityRequest>.Update
            .Set(r => r.Status, body.Status)
            .Set(r => r.Reason, body.Reason ?? string.Empty)
            .Set(r => r.UpdatedAt, DateTime.UtcNow));

    // If approved: check sellers limit then upsert community_stores
    if (body.Status == "approved")
    {
        var sellersLimitCheck = await CheckSellersPerCommunityLimitAsync(request.CommunityId, db);
        if (!sellersLimitCheck.Allowed)
            return Results.BadRequest(new { message = sellersLimitCheck.Message, code = "PLAN_LIMIT_SELLERS_PER_COMMUNITY" });

        var filter = Builders<CommunityStore>.Filter.And(
            Builders<CommunityStore>.Filter.Eq(cs => cs.CommunityId, request.CommunityId),
            Builders<CommunityStore>.Filter.Eq(cs => cs.StoreId, request.StoreId));

        var existing = await communityStoresCol.Find(filter).FirstOrDefaultAsync();
        if (existing != null)
        {
            await communityStoresCol.UpdateOneAsync(
                Builders<CommunityStore>.Filter.Eq(cs => cs.Id, existing.Id),
                Builders<CommunityStore>.Update
                    .Set(cs => cs.OwnerEnabled, true)
                    .Set(cs => cs.SellerEnabled, true)
                    .Set(cs => cs.Status, true)
                    .Set(cs => cs.UpdatedAt, DateTime.UtcNow));
        }
        else
        {
            await communityStoresCol.InsertOneAsync(new CommunityStore
            {
                CommunityId = request.CommunityId,
                StoreId = request.StoreId,
                OwnerEnabled = true,
                SellerEnabled = true,
                Status = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }
    }

    // Fire-and-forget: notify store owner
    _ = Task.Run(async () =>
    {
        try
        {
            var storesCol = db.GetCollection<Store>("stores");
            var usersCol = db.GetCollection<User>("users");

            var store = await storesCol.Find(Builders<Store>.Filter.Eq(s => s.Id, request.StoreId)).FirstOrDefaultAsync();
            if (store == null) return;

            // Find store owner: prefer store.Email, fallback to first user in Users list
            var storeOwnerEmail = store.Email;
            if (string.IsNullOrWhiteSpace(storeOwnerEmail) && store.Users.Count > 0)
            {
                var ownerId = store.Users.FirstOrDefault(u => u.Role == "owner")?.UserID ?? store.Users[0].UserID;
                var owner = await usersCol.Find(Builders<User>.Filter.Eq(u => u.Id, ownerId)).FirstOrDefaultAsync();
                if (owner != null) storeOwnerEmail = owner.Email;
            }

            if (!string.IsNullOrWhiteSpace(storeOwnerEmail))
                await emailService.SendCommunityRequestResultToStoreAsync(storeOwnerEmail, store.Name, community.Name, body.Status == "approved", body.Reason ?? string.Empty);
        }
        catch { /* silently fail */ }
    });

    return Results.Ok(new { success = true });
}).RequireAuthorization();

#endregion

#region Community Stores Management

app.MapGet("/community-stores/store/{storeId}", async (string storeId, IMongoDatabase db, ICommunityService communityService) =>
{
    var communityStoresCol = db.GetCollection<CommunityStore>("community_stores");
    var communityRequestsCol = db.GetCollection<CommunityRequest>("community_requests");

    var communities = await communityService.GetVisibleWithStatsAsync();

    var storeFilter = Builders<CommunityStore>.Filter.Eq(cs => cs.StoreId, storeId);
    var storeCommunities = await communityStoresCol.Find(storeFilter).ToListAsync();

    var requestFilter = Builders<CommunityRequest>.Filter.Eq(cr => cr.StoreId, storeId);
    var storeRequests = await communityRequestsCol.Find(requestFilter).ToListAsync();

    var result = communities.Select(c =>
    {
        var cs = storeCommunities.FirstOrDefault(x => x.CommunityId == c.Id);
        var req = storeRequests.FirstOrDefault(x => x.CommunityId == c.Id);
        return new StoreCommunityStatusResponse
        {
            CommunityId = c.Id,
            Name = c.Name,
            Logo = c.Logo,
            Open = c.Open,
            Published = cs?.OwnerEnabled == true && cs?.SellerEnabled == true,
            OwnerEnabled = cs?.OwnerEnabled ?? true,
            RequestStatus = c.Open ? string.Empty : (req?.Status ?? "none"),
            RequestReason = (!c.Open && req?.Status == "rejected") ? (req?.Reason ?? string.Empty) : string.Empty,
        };
    }).ToList();

    return Results.Ok(result);
});

app.MapPost("/community-stores", async (PublishStoreRequest request, ClaimsPrincipal user, IMongoDatabase db, IStoreService storeService, IPlanService planService, IProductSynchronizeService syncService) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!await AuthorizationHelpers.CanManageStoreAsync(request.StoreId, user, storeService))
        return Results.Forbid();

    var communitiesLimitCheck = await CheckCommunitiesJoinLimitAsync(user, request.CommunityId, includePendingRequests: false, planService, db);
    if (!communitiesLimitCheck.Allowed)
        return Results.BadRequest(new { message = communitiesLimitCheck.Message, code = "PLAN_LIMIT_COMMUNITIES" });

    var sellersLimitCheck = await CheckSellersPerCommunityLimitAsync(request.CommunityId, db);
    if (!sellersLimitCheck.Allowed)
        return Results.BadRequest(new { message = sellersLimitCheck.Message, code = "PLAN_LIMIT_SELLERS_PER_COMMUNITY" });

    var col = db.GetCollection<CommunityStore>("community_stores");

    var filter = Builders<CommunityStore>.Filter.And(
        Builders<CommunityStore>.Filter.Eq(cs => cs.CommunityId, request.CommunityId),
        Builders<CommunityStore>.Filter.Eq(cs => cs.StoreId, request.StoreId)
    );

    var existing = await col.Find(filter).FirstOrDefaultAsync();

    if (existing != null)
    {
        var newStatus = existing.OwnerEnabled && true;
        var update = Builders<CommunityStore>.Update
            .Set(cs => cs.SellerEnabled, true)
            .Set(cs => cs.Status, newStatus)
            .Set(cs => cs.UpdatedAt, DateTime.UtcNow);
        await col.UpdateOneAsync(cs => cs.Id == existing.Id, update);
    }
    else
    {
        await col.InsertOneAsync(new CommunityStore
        {
            CommunityId = request.CommunityId,
            StoreId = request.StoreId,
            OwnerEnabled = true,
            SellerEnabled = true,
            Status = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
    }

    _ = Task.Run(() => syncService.SynchronizeProductsByStoreAsync(request.StoreId));
    return Results.Ok(new { success = true });
}).RequireAuthorization();

app.MapDelete("/community-stores", async (string storeId, string communityId, IMongoDatabase db, IProductSynchronizeService syncService) =>
{
    var col = db.GetCollection<CommunityStore>("community_stores");

    var filter = Builders<CommunityStore>.Filter.And(
        Builders<CommunityStore>.Filter.Eq(cs => cs.CommunityId, communityId),
        Builders<CommunityStore>.Filter.Eq(cs => cs.StoreId, storeId)
    );

    await col.UpdateOneAsync(filter, Builders<CommunityStore>.Update
        .Set(cs => cs.SellerEnabled, false)
        .Set(cs => cs.Status, false)
        .Set(cs => cs.UpdatedAt, DateTime.UtcNow));
    _ = Task.Run(() => syncService.SynchronizeProductsByStoreAsync(storeId));
    return Results.Ok(new { success = true });
});

app.MapPost("/community-requests", async (PublishStoreRequest request, ClaimsPrincipal user, IMongoDatabase db, IStoreService storeService, IPlanService planService, IEmailService emailService) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!await AuthorizationHelpers.CanManageStoreAsync(request.StoreId, user, storeService))
        return Results.Forbid();

    var communitiesLimitCheck = await CheckCommunitiesJoinLimitAsync(user, request.CommunityId, includePendingRequests: true, planService, db);
    if (!communitiesLimitCheck.Allowed)
        return Results.BadRequest(new { message = communitiesLimitCheck.Message, code = "PLAN_LIMIT_COMMUNITIES" });

    var col = db.GetCollection<CommunityRequest>("community_requests");

    var filter = Builders<CommunityRequest>.Filter.And(
        Builders<CommunityRequest>.Filter.Eq(cr => cr.CommunityId, request.CommunityId),
        Builders<CommunityRequest>.Filter.Eq(cr => cr.StoreId, request.StoreId)
    );

    var existing = await col.Find(filter).FirstOrDefaultAsync();
    if (existing != null)
        return Results.Conflict(new { message = "Ya existe una solicitud para esta comunidad" });

    await col.InsertOneAsync(new CommunityRequest
    {
        CommunityId = request.CommunityId,
        StoreId = request.StoreId,
        Status = "pending",
        Message = request.Message ?? string.Empty,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    });

    // Fire-and-forget: notify community admin
    _ = Task.Run(async () =>
    {
        try
        {
            var communitiesCol = db.GetCollection<Community>("communities");
            var storesCol = db.GetCollection<Store>("stores");
            var usersCol = db.GetCollection<User>("users");

            var community = await communitiesCol.Find(Builders<Community>.Filter.Eq(c => c.Id, request.CommunityId)).FirstOrDefaultAsync();
            var store = await storesCol.Find(Builders<Store>.Filter.Eq(s => s.Id, request.StoreId)).FirstOrDefaultAsync();

            if (community == null || store == null) return;

            // Get admin email: prefer community.Email, fallback to OwnerUserId lookup
            var adminEmail = community.Email;
            var adminName = community.Name;
            if (string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(community.OwnerUserId))
            {
                var owner = await usersCol.Find(Builders<User>.Filter.Eq(u => u.Id, community.OwnerUserId)).FirstOrDefaultAsync();
                if (owner != null)
                {
                    adminEmail = owner.Email;
                    adminName = !string.IsNullOrWhiteSpace(owner.Name) ? owner.Name : owner.Email;
                }
            }

            if (!string.IsNullOrWhiteSpace(adminEmail))
                await emailService.SendCommunityRequestToAdminAsync(adminEmail, adminName, store.Name, community.Name, request.Message ?? string.Empty);
        }
        catch { /* silently fail */ }
    });

    return Results.Ok(new { success = true });
}).RequireAuthorization();

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

app.MapPost("/auth/login", async (LoginRequest request, IUserService service, ITokenService tokenService, ITurnstileService turnstileService, HttpContext httpContext) =>
{
    if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
        return Results.BadRequest(new { message = "Email y contraseña son requeridos" });

    var captchaOk = await turnstileService.ValidateTokenAsync(request.TurnstileToken, httpContext.Connection.RemoteIpAddress?.ToString());
    if (!captchaOk)
        return Results.BadRequest(new { message = "Captcha inválido. Intenta nuevamente." });

    UserResponse? user;
    try
    {
        user = await service.LoginAsync(request);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Json(new { message = ex.Message, code = "EMAIL_NOT_VERIFIED" }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (user == null)
        return Results.Json(new { message = "Credenciales inválidas" }, statusCode: StatusCodes.Status401Unauthorized);

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

app.MapPut("/users/{id}/role", async (string id, UpdateRoleRequest request, ClaimsPrincipal user, IUserService service) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!AuthorizationHelpers.IsSuperAdmin(user))
        return Results.Forbid();

    if (string.IsNullOrEmpty(request.Role))
        return Results.BadRequest(new { message = "El rol es requerido" });

    var success = await service.UpdateRoleAsync(id, request.Role);
    return success ? Results.Ok(new { success = true }) : Results.NotFound();
}).RequireAuthorization();

app.MapGet("/admin/stores", async (ClaimsPrincipal user, IMongoDatabase db) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!AuthorizationHelpers.IsSuperAdmin(user))
        return Results.Forbid();

    var storesCol = db.GetCollection<Store>("stores");
    var productsCol = db.GetCollection<Products>("products");

    var stores = await storesCol.Find(_ => true).SortBy(s => s.Name).ToListAsync();

    var productCounts = await productsCol
        .Aggregate()
        .Group(p => p.IdStore, g => new { StoreId = g.Key, Count = g.Count() })
        .ToListAsync();

    var countMap = productCounts.ToDictionary(x => x.StoreId, x => x.Count);

    var result = stores.Select(s => new
    {
        id = s.Id,
        name = s.Name,
        linkStore = s.LinkStore,
        logo = s.Logo,
        active = s.Active,
        isGlobal = s.IsGlobal,
        email = s.Email,
        phone = s.Phone,
        productCount = s.Id != null && countMap.TryGetValue(s.Id, out var c) ? c : 0,
        createdAt = s.CreatedAt,
    });

    return Results.Ok(result);
}).RequireAuthorization();

app.MapGet("/admin/users", async (ClaimsPrincipal user, IUserService service, IMongoDatabase db) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!AuthorizationHelpers.IsSuperAdmin(user))
        return Results.Forbid();

    var users = await service.GetAllAsync();
    var storesCol = db.GetCollection<BsonDocument>("stores");
    var pipeline = new[]
    {
        new BsonDocument("$unwind", "$users"),
        new BsonDocument("$group", new BsonDocument
        {
            { "_id", "$users.userID" },
            { "count", new BsonDocument("$sum", 1) }
        })
    };
    var storeCounts = await storesCol.Aggregate<BsonDocument>(pipeline).ToListAsync();

    var countMap = storeCounts
        .Where(d => d.Contains("_id") && !d["_id"].IsBsonNull)
        .ToDictionary(
            d => d["_id"].BsonType == BsonType.ObjectId
                ? d["_id"].AsObjectId.ToString()
                : d["_id"].ToString(),
            d => d["count"].AsInt32);

    var result = users.Select(u => new
    {
        u.Id,
        u.Name,
        u.Email,
        u.Role,
        u.EmailVerified,
        storeCount = countMap.GetValueOrDefault(u.Id ?? "", 0)
    });

    return Results.Ok(result);
}).RequireAuthorization();

app.MapGet("/users/{userId}/plan-usage", async (string userId, ClaimsPrincipal user, IMongoDatabase db) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!AuthorizationHelpers.CanAccessUser(user, userId))
        return Results.Forbid();

    var storesCol = db.GetCollection<Store>("stores");
    var communityStoresCol = db.GetCollection<CommunityStore>("community_stores");
    var communityRequestsCol = db.GetCollection<CommunityRequest>("community_requests");

    var userStoreIds = await storesCol
        .Find(Builders<Store>.Filter.And(
            Builders<Store>.Filter.ElemMatch(s => s.Users, u => u.UserID == userId),
            Builders<Store>.Filter.Eq(s => s.Active, true)))
        .Project(s => s.Id)
        .ToListAsync();

    var storeCount = userStoreIds.Count;

    var trackedCommunityIds = new HashSet<string>();

    if (userStoreIds.Count > 0)
    {
        // Publicadas en community_stores (status = true)
        var publishedIds = await communityStoresCol
            .Find(Builders<CommunityStore>.Filter.And(
                Builders<CommunityStore>.Filter.In(cs => cs.StoreId, userStoreIds),
                Builders<CommunityStore>.Filter.Eq(cs => cs.Status, true)))
            .Project(cs => cs.CommunityId)
            .ToListAsync();

        foreach (var id in publishedIds.Where(id => !string.IsNullOrWhiteSpace(id)))
            trackedCommunityIds.Add(id);

        // Solicitudes pendientes en community_requests
        var pendingIds = await communityRequestsCol
            .Find(Builders<CommunityRequest>.Filter.And(
                Builders<CommunityRequest>.Filter.In(cr => cr.StoreId, userStoreIds),
                Builders<CommunityRequest>.Filter.Eq(cr => cr.Status, "pending")))
            .Project(cr => cr.CommunityId)
            .ToListAsync();

        foreach (var id in pendingIds.Where(id => !string.IsNullOrWhiteSpace(id)))
            trackedCommunityIds.Add(id);
    }

    // Community admin usage
    var communitiesCol = db.GetCollection<Community>("communities");
    var communitiesCreated = await communitiesCol.CountDocumentsAsync(
        Builders<Community>.Filter.And(
            Builders<Community>.Filter.Eq(c => c.OwnerUserId, userId),
            Builders<Community>.Filter.Eq(c => c.Active, true)));

    return Results.Ok(new
    {
        storeCount,
        communitiesJoined = trackedCommunityIds.Count,
        communitiesCreated = (int)communitiesCreated,
    });
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

app.MapPost("/auth/resend-verification", async (RequestEmailVerificationRequest request, IUserService service) =>
{
    if (string.IsNullOrWhiteSpace(request.Email))
        return Results.BadRequest(new { message = "Email es requerido" });

    bool success;
    try
    {
        success = await service.RequestEmailVerificationAsync(request.Email);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status429TooManyRequests);
    }

    if (!success)
        return Results.BadRequest(new { message = "No se pudo procesar el reenvío de verificación" });

    return Results.Ok(new
    {
        message = "Si tu cuenta existe y no está verificada, enviamos un correo de verificación",
        success = true
    });
});

app.MapPost("/auth/request-password-reset", async (RequestPasswordResetRequest request, IUserService service, ITurnstileService turnstileService, HttpContext httpContext) =>
{
    if (string.IsNullOrEmpty(request.Email))
        return Results.BadRequest(new { message = "Email es requerido" });

    var captchaOk = await turnstileService.ValidateTokenAsync(request.TurnstileToken, httpContext.Connection.RemoteIpAddress?.ToString());
    if (!captchaOk)
        return Results.BadRequest(new { message = "Captcha inválido. Intenta nuevamente." });

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

app.MapPost("/stores", async (CreateStoreRequest request, ClaimsPrincipal user, IStoreService service, IPlanService planService, IMongoDatabase db) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (string.IsNullOrEmpty(request.Name) || string.IsNullOrEmpty(request.LinkStore))
        return Results.BadRequest(new { message = "Name y LinkStore son requeridos" });

    var storeLimitCheck = await CheckStoreCreationLimitAsync(user, planService, db);
    if (!storeLimitCheck.Allowed)
        return Results.BadRequest(new { message = storeLimitCheck.Message, code = "PLAN_LIMIT_STORES" });

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

#region Metrics

app.MapPost("/metrics/track", async (TrackMetricRequest request, IMetricsService service) =>
{
    if (string.IsNullOrWhiteSpace(request.EventType))
        return Results.BadRequest(new { message = "EventType es requerido." });

    try
    {
        await service.TrackEventAsync(request);
        return Results.Ok(new { success = true });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapPost("/metrics/track-batch", async (TrackMetricsBatchRequest request, IMetricsService service) =>
{
    if (request.Events == null || request.Events.Count == 0)
        return Results.BadRequest(new { message = "Events es requerido y no puede ser vacío." });

    try
    {
        await service.TrackBatchAsync(request.Events);
        return Results.Ok(new { success = true, total = request.Events.Count });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapGet("/metrics/summary", async (
    ClaimsPrincipal user,
    IMetricsService service,
    IStoreService storeService,
    string? storeId = null,
    string? productId = null,
    string? communityId = null,
    DateTime? dateFrom = null,
    DateTime? dateTo = null) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    var isAdmin = AuthorizationHelpers.IsAdmin(user);

    if (!isAdmin && !string.IsNullOrWhiteSpace(storeId) && !await AuthorizationHelpers.CanManageStoreAsync(storeId, user, storeService))
        return Results.Forbid();

    var allowedStoreIds = new List<string>();
    if (!isAdmin)
    {
        var currentUserId = AuthorizationHelpers.GetCurrentUserId(user);
        if (string.IsNullOrWhiteSpace(currentUserId))
            return UnauthorizedResult();

        allowedStoreIds = (await storeService.GetByUserIdAsync(currentUserId))
            .Where(store => store.Active)
            .Select(store => store.Id)
            .ToList();
    }

    var normalizedDateFrom = dateFrom?.Date;
    var normalizedDateTo = dateTo?.Date.AddDays(1);

    var summary = await service.GetSummaryAsync(
        storeId,
        productId,
        communityId,
        normalizedDateFrom,
        normalizedDateTo,
        allowedStoreIds,
        isAdmin);

    return Results.Ok(summary);
}).RequireAuthorization();

app.MapGet("/admin/community-metrics", async (
    ClaimsPrincipal user,
    IMongoDatabase db,
    DateTime? dateFrom = null,
    DateTime? dateTo = null,
    string? communityId = null) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    if (!AuthorizationHelpers.IsAdmin(user))
        return Results.Forbid();

    var currentUserId = AuthorizationHelpers.GetCurrentUserId(user);

    var communitiesCol = db.GetCollection<Community>("communities");
    var communityStoresCol = db.GetCollection<CommunityStore>("community_stores");
    var metricsCol = db.GetCollection<MetricEvent>("metric_events");

    // Get communities owned by this admin
    var ownedFilter = AuthorizationHelpers.IsSuperAdmin(user)
        ? FilterDefinition<Community>.Empty
        : Builders<Community>.Filter.Eq(c => c.OwnerUserId, currentUserId);
    var ownedCommunities = await communitiesCol.Find(ownedFilter).ToListAsync();

    if (ownedCommunities.Count == 0)
        return Results.Ok(new { communities = new List<object>(), timeline = new List<object>(), totals = new { communityViews = 0L, totalStores = 0L, activeCommunities = 0 } });

    // Filter to a specific community if requested
    var targetCommunities = string.IsNullOrWhiteSpace(communityId)
        ? ownedCommunities
        : ownedCommunities.Where(c => c.Id == communityId).ToList();

    var targetIds = targetCommunities.Select(c => c.Id).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
    // Slugs used when tracking events (e.g. "quilaco"), distinct from the ObjectId
    var targetSlugs = targetCommunities.Select(c => c.CommunityId).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();

    // Get store counts per community (CommunityStore.CommunityId references the ObjectId)
    var storeFilter = Builders<CommunityStore>.Filter.And(
        Builders<CommunityStore>.Filter.In(cs => cs.CommunityId, targetIds),
        Builders<CommunityStore>.Filter.Eq(cs => cs.Status, true));
    var communityStores = await communityStoresCol.Find(storeFilter).ToListAsync();
    var storeCountMap = communityStores.GroupBy(cs => cs.CommunityId).ToDictionary(g => g.Key, g => g.Count());

    // Get community_view metric events (MetricEvent.CommunityId stores the slug, not the ObjectId)
    var metricFilters = new List<FilterDefinition<MetricEvent>>
    {
        Builders<MetricEvent>.Filter.Eq(m => m.EventType, MetricEventTypes.CommunityView),
        Builders<MetricEvent>.Filter.In(m => m.CommunityId, targetSlugs),
    };
    if (dateFrom.HasValue) metricFilters.Add(Builders<MetricEvent>.Filter.Gte(m => m.CreatedAt, dateFrom.Value.Date));
    if (dateTo.HasValue)   metricFilters.Add(Builders<MetricEvent>.Filter.Lt(m => m.CreatedAt, dateTo.Value.Date.AddDays(1)));

    var events = await metricsCol
        .Find(Builders<MetricEvent>.Filter.And(metricFilters))
        .Project(m => new { m.CommunityId, m.CreatedAt })
        .ToListAsync();

    // Per-community breakdown
    var viewsByCommunity = events.GroupBy(e => e.CommunityId).ToDictionary(g => g.Key ?? string.Empty, g => (long)g.Count());

    var communitiesResult = targetCommunities.Select(c => new
    {
        id = c.Id,
        communityId = c.CommunityId,
        name = c.Name,
        logo = c.Logo,
        active = c.Active,
        storeCount = storeCountMap.TryGetValue(c.Id ?? string.Empty, out var sc) ? sc : 0,
        communityViews = viewsByCommunity.TryGetValue(c.CommunityId ?? string.Empty, out var cv) ? cv : 0L,
    }).OrderByDescending(c => c.communityViews).ToList();

    // Daily timeline
    var timeline = events
        .GroupBy(e => e.CreatedAt.Date)
        .OrderBy(g => g.Key)
        .Select(g => new { date = g.Key, communityViews = (long)g.Count() })
        .ToList();

    var totals = new
    {
        communityViews = (long)events.Count,
        totalStores = (long)communityStores.Count,
        activeCommunities = targetCommunities.Count(c => c.Active),
    };

    return Results.Ok(new { communities = communitiesResult, timeline, totals });
}).RequireAuthorization();

#endregion

#region Sales

app.MapPost("/sales/guest", async (CreateGuestSaleRequest request, ISalesService service) =>
{
    if (string.IsNullOrWhiteSpace(request.CustomerName) ||
        string.IsNullOrWhiteSpace(request.CustomerEmail) ||
        string.IsNullOrWhiteSpace(request.CustomerPhone) ||
        string.IsNullOrWhiteSpace(request.CustomerAddress))
    {
        return Results.BadRequest(new { message = "Nombre, email, teléfono y dirección son requeridos." });
    }

    try
    {
        var sale = await service.CreateGuestSaleAsync(request);
        return Results.Created($"/sales/{sale.Id}", sale);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapGet("/sales", async (
    ClaimsPrincipal user,
    ISalesService service,
    IStoreService storeService,
    int pageNumber = 1,
    int pageSize = 20,
    string? status = null,
    string? storeId = null,
    DateTime? dateFrom = null,
    DateTime? dateTo = null) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    var isAdmin = AuthorizationHelpers.IsAdmin(user);

    if (!isAdmin && !string.IsNullOrWhiteSpace(storeId) && !await AuthorizationHelpers.CanManageStoreAsync(storeId, user, storeService))
        return Results.Forbid();

    var allowedStoreIds = new List<string>();
    if (!isAdmin)
    {
        var currentUserId = AuthorizationHelpers.GetCurrentUserId(user);
        if (string.IsNullOrWhiteSpace(currentUserId))
            return UnauthorizedResult();

        allowedStoreIds = (await storeService.GetByUserIdAsync(currentUserId))
            .Select(store => store.Id)
            .ToList();
    }

    try
    {
        var normalizedDateFrom = dateFrom?.Date;
        var normalizedDateTo = dateTo?.Date.AddDays(1);

        var sales = await service.GetPaginatedAsync(
            pageNumber,
            pageSize,
            status,
            storeId,
            normalizedDateFrom,
            normalizedDateTo,
            allowedStoreIds,
            isAdmin);

        return Results.Ok(sales);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
}).RequireAuthorization();

app.MapGet("/sales/{id}", async (string id, ClaimsPrincipal user, ISalesService service, IStoreService storeService) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    var sale = await service.GetByIdAsync(id);
    if (sale == null)
        return Results.NotFound();

    if (!AuthorizationHelpers.IsAdmin(user) && !await AuthorizationHelpers.CanManageStoreAsync(sale.StoreId, user, storeService))
        return Results.Forbid();

    return Results.Ok(sale);
}).RequireAuthorization();

app.MapPut("/sales/{id}/status", async (string id, UpdateSaleStatusRequest request, ClaimsPrincipal user, ISalesService service, IStoreService storeService) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    var sale = await service.GetByIdAsync(id);
    if (sale == null)
        return Results.NotFound();

    if (!AuthorizationHelpers.IsAdmin(user) && !await AuthorizationHelpers.CanManageStoreAsync(sale.StoreId, user, storeService))
        return Results.Forbid();

    if (string.IsNullOrWhiteSpace(request.Status))
        return Results.BadRequest(new { message = "Status es requerido." });

    try
    {
        var updatedSale = await service.UpdateStatusAsync(id, request.Status, request.StoreObservation);
        return Results.Ok(updatedSale);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
}).RequireAuthorization();

app.MapPut("/sales/{id}", async (string id, UpdateSaleRequest request, ClaimsPrincipal user, ISalesService service, IStoreService storeService) =>
{
    if (!IsAuthenticated(user))
        return UnauthorizedResult();

    var sale = await service.GetByIdAsync(id);
    if (sale == null)
        return Results.NotFound();

    if (!AuthorizationHelpers.IsAdmin(user) && !await AuthorizationHelpers.CanManageStoreAsync(sale.StoreId, user, storeService))
        return Results.Forbid();

    if (string.IsNullOrWhiteSpace(request.CustomerName))
        return Results.BadRequest(new { message = "El nombre del cliente es requerido." });

    try
    {
        var updatedSale = await service.UpdateSaleAsync(id, request);
        return Results.Ok(updatedSale);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
}).RequireAuthorization();

#endregion

#region Blob Storage

app.MapPost("/images/upload", async (HttpRequest request, ClaimsPrincipal user, IBlobStorageService service, IProductService productService, IStoreService storeService, IPlanService planService) =>
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

    if (string.Equals(folder, "product", StringComparison.OrdinalIgnoreCase))
    {
        var product = await productService.GetByIdAsync(entityId);
        if (product == null)
            return Results.NotFound(new { message = "Producto no encontrado." });

        var targetImagesCount = product.Images.Count + 1;
        var imagesLimitCheck = await CheckProductImagesLimitAsync(user, entityId, targetImagesCount, productService, planService);
        if (!imagesLimitCheck.Allowed)
            return Results.BadRequest(new { message = imagesLimitCheck.Message, code = "PLAN_LIMIT_IMAGES" });
    }

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
    {
        // Fallback idempotente solo para imágenes de carpeta "product":
        // si no existe registro en colección images, igual intentamos remover la URL del producto.
        var isProductBlob = false;
        if (Uri.TryCreate(blobUrl, UriKind.Absolute, out var blobUri))
        {
            var segments = blobUri.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries);
            isProductBlob = segments.Length > 0 &&
                            string.Equals(segments[0], "product", StringComparison.OrdinalIgnoreCase);
        }

        var canManageProductImage = false;
        if (isProductBlob)
        {
            try
            {
                canManageProductImage = await AuthorizationHelpers.CanManageImageAsync("product", entityId, user, productService, storeService);
            }
            catch (FormatException)
            {
                canManageProductImage = false;
            }
        }

        if (canManageProductImage)
        {
            try
            {
                await productService.RemoveImageAsync(entityId, blobUrl);
            }
            catch (InvalidOperationException)
            {
                // Si no está en el producto, la eliminación ya es efectiva (idempotente).
            }
            catch (FormatException)
            {
                // entityId inválido para producto; evitamos caída y respondemos idempotente.
            }

            return Results.NoContent();
        }

        // Respuesta idempotente: si no hay metadata en images, no forzamos 404.
        return Results.NoContent();
    }

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

#region Subscriptions

app.MapGet("/plans", async (IMongoDatabase db) =>
{
    var col = db.GetCollection<Plan>("plans");
    var plans = await col.Find(p => p.Active).SortBy(p => p.PriceClp).ToListAsync();
    return Results.Ok(plans.Select(p => new
    {
        id = p.Id,
        code = p.Code,
        name = p.Name,
        description = p.Description,
        popular = p.Popular,
        type = p.Type,
        tier = p.Tier,
        priceClp = p.PriceClp,
        billing = p.Billing,
        paykuPlanId = p.PaykuPlanId,
        limits = p.Limits,
        features = p.Features
    }));
});

// Super admin: sync a plan to Payku (creates suplan entry)
app.MapPost("/admin/plans/{id}/sync-payku", async (string id, IMongoDatabase db, IPaykuService paykuService, ClaimsPrincipal user) =>
{
    if (!IsAuthenticated(user)) return UnauthorizedResult();
    if (!AuthorizationHelpers.IsSuperAdmin(user)) return Results.Forbid();

    var col = db.GetCollection<Plan>("plans");
    var plan = await col.Find(p => p.Id == id).FirstOrDefaultAsync();
    if (plan == null) return Results.NotFound(new { message = "Plan no encontrado" });
    if (plan.PriceClp <= 0) return Results.BadRequest(new { message = "El plan debe tener un precio mayor a 0 para crear suscripciones en Payku" });

    var paykuResp = await paykuService.CreatePlanAsync(new PaykuCreatePlanRequest
    {
        name = $"{plan.Name}_{plan.Id![^6..]}",
        amount = ((int)plan.PriceClp).ToString(),
        interval = "1",
        interval_count = "1",
        trial_days = "0",
        currency = "CLP"
    });

    if (string.IsNullOrWhiteSpace(paykuResp.PlanId))
        return Results.BadRequest(new { message = paykuResp.message_error ?? paykuResp.message ?? "Error al crear plan en Payku" });

    await col.UpdateOneAsync(p => p.Id == id,
        Builders<Plan>.Update.Set(p => p.PaykuPlanId, paykuResp.PlanId));

    return Results.Ok(new { paykuPlanId = paykuResp.PlanId });
}).RequireAuthorization();

// Super admin: set Payku plan ID manually (when plan already exists in Payku)
app.MapPut("/admin/plans/{id}/payku-id", async (string id, HttpContext ctx, IMongoDatabase db, ClaimsPrincipal user) =>
{
    if (!IsAuthenticated(user)) return UnauthorizedResult();
    if (!AuthorizationHelpers.IsSuperAdmin(user)) return Results.Forbid();

    var body = await ctx.Request.ReadFromJsonAsync<Dictionary<string, string>>();
    var paykuPlanId = body?.GetValueOrDefault("paykuPlanId")?.Trim();
    if (string.IsNullOrWhiteSpace(paykuPlanId))
        return Results.BadRequest(new { message = "paykuPlanId es requerido" });

    var col = db.GetCollection<Plan>("plans");
    var result = await col.UpdateOneAsync(p => p.Id == id,
        Builders<Plan>.Update.Set(p => p.PaykuPlanId, paykuPlanId));

    if (result.MatchedCount == 0) return Results.NotFound(new { message = "Plan no encontrado" });
    return Results.Ok(new { paykuPlanId });
}).RequireAuthorization();

// User: start a subscription flow
app.MapPost("/subscriptions/start", async (StartSubscriptionRequest request, ClaimsPrincipal user, IMongoDatabase db,
    IPaykuService paykuService, IUserSubscriptionService subService, IUserService userService, IConfiguration config) =>
{
    if (!IsAuthenticated(user)) return UnauthorizedResult();

    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
    var userData = await userService.GetByIdAsync(userId);
    if (userData == null) return Results.NotFound(new { message = "Usuario no encontrado" });

    var col = db.GetCollection<Plan>("plans");
    var plan = await col.Find(p => p.Id == request.PlanId && p.Active).FirstOrDefaultAsync();
    if (plan == null) return Results.NotFound(new { message = "Plan no encontrado" });
    if (string.IsNullOrWhiteSpace(plan.PaykuPlanId))
        return Results.BadRequest(new { message = "El plan no está sincronizado con Payku" });

    // Create or retrieve Payku client
    var clientResp = await paykuService.CreateClientAsync(new PaykuCreateClientRequest
    {
        name = userData.Name ?? "Usuario",
        email = userData.Email ?? "",
        phone = !string.IsNullOrWhiteSpace(userData.Phone) ? userData.Phone : "000000000"
    });

    if (string.IsNullOrWhiteSpace(clientResp.ClientId))
        return Results.BadRequest(new { message = clientResp.message_error ?? clientResp.message ?? "Error al crear cliente en Payku" });

    // Build webhook and redirect URLs
    var frontendUrl = (config["FrontendUrl"] ?? "https://feriacomunidad.cl").TrimEnd('/');
    var apiBase = config["ApiBaseUrl"] ?? "https://api.feriacomunidad.cl";

    app.Logger.LogInformation("Payku URLs → return={Return} cancel={Cancel} notifyActivation={Activation} notifyPayment={Payment}",
        (object)$"{frontendUrl}/admin/subscription?status=success",
        (object)$"{frontendUrl}/admin/subscription?status=cancel",
        (object)$"{apiBase}/subscriptions/webhook/activation",
        (object)$"{apiBase}/subscriptions/webhook/payment");

    // Create Payku subscription BEFORE persisting locally — avoids orphaned pending records on failure
    var subResp = await paykuService.CreateSubscriptionAsync(new PaykuCreateSubscriptionRequest
    {
        plan = plan.PaykuPlanId,
        client = clientResp.ClientId,
        url_return = $"{frontendUrl}/admin/subscription?status=success",
        url_cancel = $"{frontendUrl}/admin/subscription?status=cancel",
        url_notify_suscription = $"{apiBase}/subscriptions/webhook/activation",
        url_notify_payment = $"{apiBase}/subscriptions/webhook/payment"
    });

    if (string.IsNullOrWhiteSpace(subResp.url))
        return Results.BadRequest(new { message = subResp.message ?? "Error al iniciar suscripción en Payku" });

    // Persist pending subscription only after Payku confirms it
    await subService.CreatePendingAsync(userId, request.PlanId, clientResp.ClientId, subResp.SubscriptionId ?? "");

    return Results.Ok(new StartSubscriptionResponse
    {
        SubscriptionId = subResp.SubscriptionId ?? "",
        PaymentUrl = subResp.url
    });
}).RequireAuthorization();

// Payku webhook: subscription activated
app.MapPost("/subscriptions/webhook/activation", async (PaykuWebhookActivation payload, IUserSubscriptionService subService) =>
{
    if (string.IsNullOrWhiteSpace(payload.client)) return Results.BadRequest();

    // Payku activates subscriptions at the start of next month billing cycle
    var nextBilling = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc)
        .AddMonths(1);

    await subService.ActivateAsync(payload.token ?? "", payload.client, nextBilling);
    return Results.Ok();
});

// Payku webhook: payment received
app.MapPost("/subscriptions/webhook/payment", async (PaykuWebhookPayment payload, IUserSubscriptionService subService) =>
{
    if (string.IsNullOrWhiteSpace(payload.client)) return Results.BadRequest();

    var nextBilling = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc)
        .AddMonths(1);

    await subService.RecordPaymentAsync(payload.client, payload.token ?? "", nextBilling);
    return Results.Ok();
});

// User: confirm subscription after returning from Payku gateway (fallback for when webhook can't reach localhost)
app.MapPost("/subscriptions/confirm", async (ClaimsPrincipal user, IUserSubscriptionService subService) =>
{
    if (!IsAuthenticated(user)) return UnauthorizedResult();

    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
    var sub = await subService.GetByUserIdAsync(userId);
    if (sub == null) return Results.NotFound(new { message = "No se encontró suscripción pendiente" });
    if (sub.Status != "pending") return Results.Ok(new { status = sub.Status });

    var nextBilling = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc)
        .AddMonths(1);

    await subService.ActivateAsync(sub.PaykuSubscriptionId ?? "", sub.PaykuClientId, nextBilling);
    return Results.Ok(new { status = "active" });
}).RequireAuthorization();

// User: get own subscription
app.MapGet("/subscriptions/me", async (ClaimsPrincipal user, IUserSubscriptionService subService, IMongoDatabase db) =>
{
    if (!IsAuthenticated(user)) return UnauthorizedResult();

    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
    var sub = await subService.GetByUserIdAsync(userId);
    if (sub == null) return Results.Ok((object?)null);

    var plansCol = db.GetCollection<Plan>("plans");
    var plan = await plansCol.Find(p => p.Id == sub.PlanId).FirstOrDefaultAsync();

    return Results.Ok(new UserSubscriptionResponse
    {
        Id = sub.Id ?? "",
        PlanId = sub.PlanId,
        PlanName = plan?.Name ?? "",
        Status = sub.Status,
        StartDate = sub.StartDate,
        NextBillingDate = sub.NextBillingDate,
        CreatedAt = sub.CreatedAt
    });
}).RequireAuthorization();

// User: cancel subscription
app.MapPost("/subscriptions/cancel", async (ClaimsPrincipal user, IUserSubscriptionService subService, IPaykuService paykuService, ILogger<Program> logger) =>
{
    if (!IsAuthenticated(user)) return UnauthorizedResult();

    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    // Get subscription before cancelling to retrieve the Payku token
    var existing = await subService.GetByUserIdAsync(userId);
    if (existing == null || (existing.Status != "active" && existing.Status != "pending"))
        return Results.NotFound(new { message = "No se encontró una suscripción activa" });

    // Cancel in Payku first
    if (!string.IsNullOrWhiteSpace(existing.PaykuSubscriptionId))
    {
        try { await paykuService.CancelSubscriptionAsync(existing.PaykuSubscriptionId); }
        catch (Exception ex) { logger.LogWarning("Payku cancel error (ignored): {Msg}", ex.Message); }
    }

    await subService.CancelAsync(userId);
    return Results.Ok(new { message = "Suscripción cancelada" });
}).RequireAuthorization();

#endregion

app.Run();

public record AddUserToStoreRequest(string UserId, string Role);
