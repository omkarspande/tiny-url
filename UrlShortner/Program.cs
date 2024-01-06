using LiteDB;
using Microsoft.AspNetCore.WebUtilities;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ILiteDatabase, LiteDatabase>(_ => new LiteDatabase("short-links.db"));
await using var app = builder.Build();

app.MapGet("/", (Func<string>)(() => "Hello World!"));

app.MapPost("/url", ShortenerDelegate);

app.MapFallback(RedirectDelegate);

await app.RunAsync();

static async Task ShortenerDelegate(HttpContext httpContext)
{
    var request = await httpContext.Request.ReadFromJsonAsync<UrlDto>();
    if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var inputUri))
    {
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsync("URL is invalid.");
        return;
    }

    var liteDb = httpContext.RequestServices.GetRequiredService<ILiteDatabase>();
    var links = liteDb.GetCollection<ShortUrl>(BsonAutoId.Int32);
    var entry = new ShortUrl(inputUri);
    links.Insert(entry);
    var result = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/{entry.UrlChunk}";
    await httpContext.Response.WriteAsJsonAsync(new { url = result });
}

static async Task RedirectDelegate(HttpContext httpContext)
{
    var db = httpContext.RequestServices.GetRequiredService<ILiteDatabase>();
    var collection = db.GetCollection<ShortUrl>();

    var path = httpContext.Request.Path.ToUriComponent().Trim('/');
    var id = BitConverter.ToInt32(WebEncoders.Base64UrlDecode(path));
    var entry = collection.Find(p => p.Id == id).FirstOrDefault();

    httpContext.Response.Redirect(entry?.Url ?? "/");
    await Task.CompletedTask;
}

public class UrlDto
{
    public string Url { get; set; }
}

public class ShortUrl
{
    public int Id { get; protected set; }
    public string Url { get; protected set; }
    public string UrlChunk => WebEncoders.Base64UrlEncode(BitConverter.GetBytes(Id));

    public ShortUrl(Uri url)
    {
        Url = url.ToString();
    }
}



