using Microsoft.AspNetCore.StaticFiles;
using MollyNvr.Api.Endpoints;
using MollyNvr.Infrastructure;

// Docker Desktop (host networking) does not pass IPv4-mapped connections (::ffff:127.0.0.1)
// to v4-only listeners — for example, to the go2rtc API on 127.0.0.1:1984. Force IPv4.
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
                // Do not cache the manifest and service worker so PWA updates are picked up immediately.
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
