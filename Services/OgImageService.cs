using System.Globalization;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ApiMercadoComunidad.Services;

public class OgImageService : IOgImageService
{
    private const int W = 1200;
    private const int H = 630;
    private const int ImagePanelW = 560;
    private const int TextX = 632;
    private const int TextMaxW = 520;

    private static readonly Color BgColor = Color.ParseHex("#0F172A");
    private static readonly Color AccentColor = Color.ParseHex("#3B82F6");
    private static readonly Color TitleColor = Color.White;
    private static readonly Color PriceColor = Color.ParseHex("#60A5FA");
    private static readonly Color StoreColor = Color.ParseHex("#94A3B8");
    private static readonly Color MutedColor = Color.ParseHex("#475569");

    private readonly IProductService _products;
    private readonly IStoreService _stores;
    private readonly IHttpClientFactory _http;
    private readonly FontFamily _font;

    public OgImageService(IProductService products, IStoreService stores, IHttpClientFactory http)
    {
        _products = products;
        _stores = stores;
        _http = http;
        _font = ResolveFont();
    }

    private static FontFamily ResolveFont()
    {
        var candidates = new[] { "Liberation Sans", "DejaVu Sans", "Arial", "FreeSans", "Noto Sans", "Helvetica" };
        foreach (var name in candidates)
            if (SystemFonts.TryGet(name, out var f)) return f;

        // Last resort: use any available system font
        var all = SystemFonts.Families.ToList();
        if (all.Count > 0) return all[0];

        throw new InvalidOperationException("No system fonts found. Install fonts-liberation or fonts-dejavu-core.");
    }

    public async Task<byte[]> GenerateProductOgImageAsync(string productId)
    {
        var product = await _products.GetByIdAsync(productId)
            ?? throw new KeyNotFoundException($"Producto '{productId}' no encontrado.");

        var store = await _stores.GetByIdAsync(product.IdStore);
        var storeName = store?.Name ?? string.Empty;

        Image<Rgba32>? productImg = null;
        if (product.Images.Count > 0)
        {
            try
            {
                var client = _http.CreateClient("og");
                var bytes = await client.GetByteArrayAsync(product.Images[0]);
                productImg = Image.Load<Rgba32>(bytes);
            }
            catch { /* sin imagen, solo fondo */ }
        }

        using var image = new Image<Rgba32>(W, H);

        image.Mutate(ctx =>
        {
            // Fondo completo
            ctx.Fill(BgColor);

            // Panel izquierdo: imagen del producto
            if (productImg != null)
            {
                productImg.Mutate(p => p.Resize(new ResizeOptions
                {
                    Size = new Size(ImagePanelW, H),
                    Mode = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center,
                }));
                ctx.DrawImage(productImg, new Point(0, 0), 1f);

                // Fade del panel de imagen al fondo
                var fade = new LinearGradientBrush(
                    new PointF(360, 0), new PointF(ImagePanelW, 0),
                    GradientRepetitionMode.None,
                    new ColorStop(0f, Color.Transparent),
                    new ColorStop(1f, BgColor));
                ctx.Fill(fade, new RectangleF(360, 0, ImagePanelW - 360, H));
            }

            // Barra de acento azul en panel derecho
            ctx.Fill(AccentColor, new RectangleF(TextX - 12, 0, W - TextX + 12, 5));

            var brandFont = _font.CreateFont(15, FontStyle.Regular);
            var storeFont = _font.CreateFont(22, FontStyle.Regular);
            var titleFont = _font.CreateFont(44, FontStyle.Bold);
            var priceFont = _font.CreateFont(56, FontStyle.Bold);
            var catFont = _font.CreateFont(16, FontStyle.Regular);

            // "mercadocomunidad.cl"
            ctx.DrawText(new RichTextOptions(brandFont)
            {
                Origin = new PointF(TextX, 30),
            }, "mercadocomunidad.cl", MutedColor);

            // Nombre de la tienda
            if (!string.IsNullOrWhiteSpace(storeName))
            {
                ctx.DrawText(new RichTextOptions(storeFont)
                {
                    Origin = new PointF(TextX, 170),
                    WrappingLength = TextMaxW,
                    WordBreaking = WordBreaking.Standard,
                }, Truncate(storeName, 45), StoreColor);
            }

            // Título del producto (máx ~2 líneas)
            var title = Truncate(product.Title, 80);
            ctx.DrawText(new RichTextOptions(titleFont)
            {
                Origin = new PointF(TextX, 215),
                WrappingLength = TextMaxW,
                WordBreaking = WordBreaking.Standard,
                LineSpacing = 1.15f,
            }, title, TitleColor);

            // Precio
            if (product.Price > 0)
            {
                var priceText = FormatPrice(product.Price);
                ctx.DrawText(new RichTextOptions(priceFont)
                {
                    Origin = new PointF(TextX, 430),
                }, priceText, PriceColor);
            }

            // Categoría
            if (!string.IsNullOrWhiteSpace(product.Category))
            {
                ctx.DrawText(new RichTextOptions(catFont)
                {
                    Origin = new PointF(TextX, 560),
                }, product.Category.ToUpperInvariant(), MutedColor);
            }
        });

        productImg?.Dispose();

        using var ms = new MemoryStream();
        await image.SaveAsPngAsync(ms);
        return ms.ToArray();
    }

    public async Task<ProductOgMeta> GetProductMetaAsync(string productId)
    {
        var product = await _products.GetByIdAsync(productId)
            ?? throw new KeyNotFoundException($"Producto '{productId}' no encontrado.");

        var store = await _stores.GetByIdAsync(product.IdStore);
        var storeName = store?.Name is { Length: > 0 } name ? $" · {name}" : string.Empty;

        var title = $"{product.Title}{storeName}";

        var description = !string.IsNullOrWhiteSpace(product.Description)
            ? Truncate(product.Description, 160)
            : product.Price > 0
                ? $"{product.Title} por {FormatPrice(product.Price)}"
                : product.Title;

        return new ProductOgMeta(title, description);
    }

    private static string FormatPrice(decimal price)
    {
        var formatted = price.ToString("N0", new CultureInfo("es-CL"));
        return $"$ {formatted}";
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)] + "…";
}
