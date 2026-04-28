using System.Globalization;
using ApiMercadoComunidad.Models.DTOs;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ApiMercadoComunidad.Services;

public class CatalogPdfService : ICatalogPdfService
{
    private readonly IProductService _products;
    private readonly IStoreService _stores;
    private readonly IHttpClientFactory _http;

    private const string FrontendBase = "https://feriacomunidad.cl";

    public CatalogPdfService(IProductService products, IStoreService stores, IHttpClientFactory http)
    {
        _products = products;
        _stores = stores;
        _http = http;
    }

    public async Task<byte[]> GenerateStoreCatalogAsync(string storeId)
    {
        var store = await _stores.GetByIdAsync(storeId)
            ?? throw new KeyNotFoundException($"Tienda '{storeId}' no encontrada.");

        var products = (await _products.GetByStoreIdAsync(storeId))
            .Where(p => p.Active)
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Title)
            .ToList();

        var client = _http.CreateClient("og");

        byte[]? logoBytes = null;
        if (!string.IsNullOrWhiteSpace(store.Logo))
        {
            try { logoBytes = await client.GetByteArrayAsync(store.Logo); } catch { }
        }

        var productImages = new Dictionary<string, byte[]>();
        await Task.WhenAll(products
            .Where(p => p.Images.Count > 0)
            .Select(async p =>
            {
                try
                {
                    var bytes = await client.GetByteArrayAsync(p.Images[0]);
                    lock (productImages) productImages[p.Id] = bytes;
                }
                catch { }
            }));

        var storeUrl = $"{FrontendBase}/store/{store.LinkStore}";
        var qrBytes = GenerateQrPng(storeUrl);

        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(40);
                page.MarginVertical(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor("#111827"));

                page.Header().Element(c => ComposeHeader(c, store, logoBytes, qrBytes, storeUrl));
                page.Content().Element(c => ComposeContent(c, products, productImages));
                page.Footer().BorderTop(1).BorderColor("#E5E7EB").PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().Text("feriacomunidad.cl").FontColor("#9CA3AF").FontSize(9);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Página ").FontColor("#9CA3AF").FontSize(9);
                        x.CurrentPageNumber().FontColor("#9CA3AF").FontSize(9);
                        x.Span(" de ").FontColor("#9CA3AF").FontSize(9);
                        x.TotalPages().FontColor("#9CA3AF").FontSize(9);
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private static byte[] GenerateQrPng(string url)
    {
        using var qrGenerator = new QRCodeGenerator();
        var qrData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        var qrCode = new PngByteQRCode(qrData);
        return qrCode.GetGraphic(5);
    }

    private static void ComposeHeader(IContainer container, StoreResponse store, byte[]? logoBytes, byte[] qrBytes, string storeUrl)
    {
        container.PaddingBottom(20).BorderBottom(1).BorderColor("#E5E7EB").Row(row =>
        {
            // Logo
            if (logoBytes != null)
            {
                row.ConstantItem(72).Height(72).Image(logoBytes).FitArea();
                row.ConstantItem(16);
            }

            // Store info
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(store.Name).Bold().FontSize(24).FontColor("#111827");
                col.Item().PaddingTop(2).Text("Catálogo de Productos").FontSize(13).FontColor("#6B7280");
                col.Item().PaddingTop(4).Text(
                    DateTime.Now.ToString("MMMM yyyy", new CultureInfo("es-CL"))
                ).FontSize(9).FontColor("#9CA3AF");
            });

            // QR code
            row.ConstantItem(16);
            row.ConstantItem(80).Column(col =>
            {
                col.Item().Height(80).Hyperlink(storeUrl).Image(qrBytes).FitArea();
                col.Item().PaddingTop(3).AlignCenter().Text("Ver tienda").FontSize(7).FontColor("#9CA3AF");
            });
        });
    }

    private static void ComposeContent(IContainer container, List<ProductResponse> products, Dictionary<string, byte[]> images)
    {
        if (products.Count == 0)
        {
            container.PaddingTop(40).AlignCenter().Text("No hay productos activos en esta tienda.").FontColor("#9CA3AF");
            return;
        }

        container.PaddingTop(12).Column(col =>
        {
            string? lastCategory = null;

            foreach (var product in products)
            {
                // Category label
                if (!string.IsNullOrWhiteSpace(product.Category) && product.Category != lastCategory)
                {
                    lastCategory = product.Category;
                    col.Item().PaddingTop(lastCategory == products[0].Category ? 0 : 8)
                        .PaddingBottom(6)
                        .Text(product.Category.ToUpperInvariant())
                        .Bold().FontSize(9).FontColor("#3B82F6");
                }

                var productUrl = $"{FrontendBase}/product/{product.Id}";

                col.Item().BorderBottom(1).BorderColor("#F3F4F6").PaddingVertical(10).Row(row =>
                {
                    // Product image (clickable)
                    row.ConstantItem(90).Height(90).Hyperlink(productUrl).Element(c =>
                    {
                        if (images.TryGetValue(product.Id, out var imgBytes))
                            c.Image(imgBytes).FitArea();
                        else
                            c.Background("#F9FAFB").AlignCenter().AlignMiddle()
                             .Text("Sin imagen").FontColor("#D1D5DB").FontSize(8);
                    });

                    row.ConstantItem(14);

                    // Product info
                    row.RelativeItem().Column(textCol =>
                    {
                        textCol.Item().Text(product.Title).Bold().FontSize(13).FontColor("#111827");
                        textCol.Item().PaddingTop(4).Text(FormatPrice(product.Price))
                            .Bold().FontSize(16).FontColor("#3B82F6");

                        if (!string.IsNullOrWhiteSpace(product.Description))
                        {
                            var desc = product.Description.Length > 200
                                ? product.Description[..200] + "…"
                                : product.Description;
                            textCol.Item().PaddingTop(6).Text(desc).FontColor("#6B7280").FontSize(10);
                        }

                        textCol.Item().PaddingTop(8).Hyperlink(productUrl)
                            .Text("Ver publicación →").FontColor("#3B82F6").FontSize(9);
                    });
                });
            }
        });
    }

    private static string FormatPrice(decimal price) =>
        "$ " + price.ToString("N0", new CultureInfo("es-CL"));
}
