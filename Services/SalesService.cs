using ApiMercadoComunidad.Configuration;
using ApiMercadoComunidad.Models;
using ApiMercadoComunidad.Models.DTOs;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace ApiMercadoComunidad.Services;

public class SalesService : ISalesService
{
    private readonly IMongoCollection<Sale> _salesCollection;
    private readonly IMongoCollection<CommunityProduct> _communityProductsCollection;
    private readonly IProductService _productService;
    private readonly IStoreService _storeService;
    private readonly IEmailService _emailService;
    private readonly ILogger<SalesService> _logger;

    public SalesService(
        IOptions<MongoDbSettings> mongoDbSettings,
        IProductService productService,
        IStoreService storeService,
        IEmailService emailService,
        ILogger<SalesService> logger)
    {
        var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _salesCollection = mongoDatabase.GetCollection<Sale>("sales");
        _communityProductsCollection = mongoDatabase.GetCollection<CommunityProduct>("community_products");
        _productService = productService;
        _storeService = storeService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<SaleResponse> CreateGuestSaleAsync(CreateGuestSaleRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            throw new InvalidOperationException("Debes incluir al menos un producto en la compra.");

        var saleItems = new List<SaleItem>();
        string? storeId = null;

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.ProductId) || item.Quantity <= 0)
                throw new InvalidOperationException("Los productos enviados son inválidos.");

            var resolvedProductId = await ResolveProductIdAsync(item.ProductId);
            var product = await _productService.GetByIdAsync(resolvedProductId);
            if (product == null || !product.Active)
                throw new InvalidOperationException("Uno de los productos ya no está disponible.");

            storeId ??= product.IdStore;
            if (!string.Equals(storeId, product.IdStore, StringComparison.Ordinal))
                throw new InvalidOperationException("Por ahora sólo se puede comprar productos de una misma tienda por pedido.");

            var lineTotal = product.Price * item.Quantity;
            saleItems.Add(new SaleItem
            {
                ProductId = product.Id,
                ProductTitle = product.Title,
                ProductImage = product.Images.FirstOrDefault() ?? string.Empty,
                Quantity = item.Quantity,
                UnitPrice = product.Price,
                LineTotal = lineTotal
            });
        }

        var store = await _storeService.GetByIdAsync(storeId!);
        if (store == null || !store.Active)
            throw new InvalidOperationException("La tienda asociada a la compra no está disponible.");

        var subtotal = saleItems.Sum(item => item.LineTotal);
        var sale = new Sale
        {
            StoreId = store.Id,
            StoreName = store.Name,
            Status = SaleStatuses.Requested,
            CustomerName = request.CustomerName.Trim(),
            CustomerEmail = request.CustomerEmail.Trim(),
            CustomerPhone = request.CustomerPhone.Trim(),
            CustomerAddress = request.CustomerAddress.Trim(),
            Notes = request.Notes?.Trim() ?? string.Empty,
            Items = saleItems,
            Subtotal = subtotal,
            Total = subtotal,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _salesCollection.InsertOneAsync(sale);
        var saleResponse = MapToResponse(sale);

        // Enviar correos (fire-and-forget, no bloquea la respuesta)
        _ = SendOrderEmailsAsync(saleResponse, store);

        return saleResponse;
    }

    public async Task<PaginatedResult<SaleResponse>> GetPaginatedAsync(
        int pageNumber,
        int pageSize,
        string? status = null,
        string? storeId = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        IEnumerable<string>? allowedStoreIds = null,
        bool includeAllStores = false)
    {
        var filters = new List<FilterDefinition<Sale>>();

        if (!string.IsNullOrWhiteSpace(status))
            filters.Add(Builders<Sale>.Filter.Eq(s => s.Status, NormalizeStatus(status)));

        if (dateFrom.HasValue)
            filters.Add(Builders<Sale>.Filter.Gte(s => s.CreatedAt, dateFrom.Value));

        if (dateTo.HasValue)
            filters.Add(Builders<Sale>.Filter.Lt(s => s.CreatedAt, dateTo.Value));

        if (!string.IsNullOrWhiteSpace(storeId))
        {
            filters.Add(Builders<Sale>.Filter.Eq(s => s.StoreId, storeId));
        }
        else if (!includeAllStores)
        {
            var allowedIds = allowedStoreIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList() ?? new List<string>();
            if (allowedIds.Count == 0)
            {
                return new PaginatedResult<SaleResponse>
                {
                    Data = new List<SaleResponse>(),
                    TotalCount = 0,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }

            filters.Add(Builders<Sale>.Filter.In(s => s.StoreId, allowedIds));
        }

        var filter = filters.Count > 0
            ? Builders<Sale>.Filter.And(filters)
            : Builders<Sale>.Filter.Empty;

        var totalCount = await _salesCollection.CountDocumentsAsync(filter);
        var sales = await _salesCollection
            .Find(filter)
            .SortByDescending(s => s.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return new PaginatedResult<SaleResponse>
        {
            Data = sales.Select(MapToResponse).ToList(),
            TotalCount = (int)totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<SaleResponse?> GetByIdAsync(string id)
    {
        var sale = await _salesCollection.Find(s => s.Id == id).FirstOrDefaultAsync();
        return sale == null ? null : MapToResponse(sale);
    }

    public async Task<SaleResponse?> UpdateStatusAsync(string id, string status, string? storeObservation = null)
    {
        var normalizedStatus = NormalizeStatus(status);

        var update = Builders<Sale>.Update
            .Set(s => s.Status, normalizedStatus)
            .Set(s => s.StoreObservation, storeObservation?.Trim() ?? string.Empty)
            .Set(s => s.UpdatedAt, DateTime.UtcNow);

        var result = await _salesCollection.UpdateOneAsync(s => s.Id == id, update);
        if (result.MatchedCount == 0)
            return null;

        return await GetByIdAsync(id);
    }

    public async Task<SaleResponse?> UpdateSaleAsync(string id, UpdateSaleRequest request)
    {
        var existing = await _salesCollection.Find(s => s.Id == id).FirstOrDefaultAsync();
        if (existing == null) return null;

        var saleItems = new List<SaleItem>();
        if (request.Items != null && request.Items.Count > 0)
        {
            string? storeId = null;
            foreach (var item in request.Items)
            {
                if (string.IsNullOrWhiteSpace(item.ProductId) || item.Quantity <= 0)
                    throw new InvalidOperationException("Los productos enviados son inválidos.");

                var resolvedProductId = await ResolveProductIdAsync(item.ProductId);
                var product = await _productService.GetByIdAsync(resolvedProductId);
                if (product == null || !product.Active)
                    throw new InvalidOperationException("Uno de los productos ya no está disponible.");

                storeId ??= product.IdStore;
                if (!string.Equals(storeId, product.IdStore, StringComparison.Ordinal))
                    throw new InvalidOperationException("Por ahora sólo se puede editar productos de una misma tienda por pedido.");

                saleItems.Add(new SaleItem
                {
                    ProductId = product.Id,
                    ProductTitle = product.Title,
                    ProductImage = product.Images.FirstOrDefault() ?? string.Empty,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price,
                    LineTotal = product.Price * item.Quantity
                });
            }
        }
        else
        {
            saleItems = existing.Items;
        }

        var subtotal = saleItems.Sum(i => i.LineTotal);
        var total = request.Total.HasValue && request.Total.Value >= 0
            ? request.Total.Value
            : subtotal;
        var normalizedStatus = !string.IsNullOrWhiteSpace(request.Status)
            ? NormalizeStatus(request.Status)
            : existing.Status;

        var update = Builders<Sale>.Update
            .Set(s => s.CustomerName, request.CustomerName.Trim())
            .Set(s => s.CustomerEmail, request.CustomerEmail.Trim())
            .Set(s => s.CustomerPhone, request.CustomerPhone.Trim())
            .Set(s => s.CustomerAddress, request.CustomerAddress.Trim())
            .Set(s => s.Notes, request.Notes?.Trim() ?? string.Empty)
            .Set(s => s.Status, normalizedStatus)
            .Set(s => s.Items, saleItems)
            .Set(s => s.Subtotal, subtotal)
            .Set(s => s.Total, total)
            .Set(s => s.UpdatedAt, DateTime.UtcNow);

        await _salesCollection.UpdateOneAsync(s => s.Id == id, update);
        return await GetByIdAsync(id);
    }

    private async Task SendOrderEmailsAsync(SaleResponse sale, StoreResponse store)
    {
        // Correo al comprador (incluye email de la tienda si está disponible)
        var buyerResult = await _emailService.SendOrderConfirmationToBuyerAsync(sale.CustomerEmail, sale, store.Email ?? "");
        if (!buyerResult.success)
            _logger.LogWarning("No se pudo enviar correo al comprador {Email}: {Error}", sale.CustomerEmail, buyerResult.errorMessage);

        // Correo al vendedor si la tienda tiene email
        if (!string.IsNullOrWhiteSpace(store.Email))
        {
            var sellerResult = await _emailService.SendOrderNotificationToSellerAsync(store.Email, store.Name, sale);
            if (!sellerResult.success)
                _logger.LogWarning("No se pudo enviar correo al vendedor {Email}: {Error}", store.Email, sellerResult.errorMessage);
        }
    }

    private static string NormalizeStatus(string status)
    {
        var normalized = SaleStatuses.All.FirstOrDefault(item => item.Equals(status?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (normalized == null)
            throw new InvalidOperationException("Estado de venta inválido.");

        return normalized;
    }

    private async Task<string> ResolveProductIdAsync(string productId)
    {
        var product = await _productService.GetByIdAsync(productId);
        if (product != null)
            return productId;

        var communityProduct = await _communityProductsCollection
            .Find(cp => cp.Id == productId)
            .FirstOrDefaultAsync();

        return communityProduct?.ProductId ?? productId;
    }

    private static SaleResponse MapToResponse(Sale sale)
    {
        return new SaleResponse
        {
            Id = sale.Id ?? string.Empty,
            StoreId = sale.StoreId,
            StoreName = sale.StoreName,
            Status = sale.Status,
            PaymentMethod = sale.PaymentMethod,
            CustomerName = sale.CustomerName,
            CustomerEmail = sale.CustomerEmail,
            CustomerPhone = sale.CustomerPhone,
            CustomerAddress = sale.CustomerAddress,
            Notes = sale.Notes,
            StoreObservation = sale.StoreObservation,
            Items = sale.Items.Select(item => new SaleItemResponse
            {
                ProductId = item.ProductId,
                ProductTitle = item.ProductTitle,
                ProductImage = item.ProductImage,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                LineTotal = item.LineTotal
            }).ToList(),
            Subtotal = sale.Subtotal,
            Total = sale.Total,
            CreatedAt = sale.CreatedAt,
            UpdatedAt = sale.UpdatedAt
        };
    }
}
