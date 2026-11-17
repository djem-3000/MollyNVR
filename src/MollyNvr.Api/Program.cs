using Microsoft.AspNetCore.StaticFiles;
using MollyNvr.Api.Endpoints;
using MollyNvr.Infrastructure;

// Docker Desktop (host networking) не пропускает IPv4-mapped соединения (::ffff:127.0.0.1)
// к v4-only слушателям — например, к API go2rtc на 127.0.0.1:1984. Принудительно используем IPv4.
AppContext.SetSwitch("System.Net.DisableIPv6", true);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

var app = builder.Build();

var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".webmanifest"] = "application/manifest+json";

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypes,
    OnPrepareResponse = context =>
    {
        // Манифест и service worker не кешируем, чтобы обновления PWA подхватывались сразу.
        if (context.File.Name is "sw.js" or "manifest.webmanifest")
        {
            context.Context.Response.Headers.CacheControl = "no-cache";
        }
    },
});

app.MapSystemEndpoints();
app.MapStreamEndpoints();
app.MapCameraEndpoints();
app.MapDiscoveryEndpoints();
app.MapRecordingEndpoints();
app.MapArchiveEndpoints();

app.MapFallbackToFile("index.html");

app.Run();
