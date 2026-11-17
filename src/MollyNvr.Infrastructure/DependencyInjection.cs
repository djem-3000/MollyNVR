using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MollyNvr.Application.Archive;
using MollyNvr.Application.Cameras;
using MollyNvr.Application.Discovery;
using MollyNvr.Application.Recording;
using MollyNvr.Application.Streams;
using MollyNvr.Cameras.Abstractions;
using MollyNvr.Cameras.Hikvision;
using MollyNvr.Cameras.ONVIF;
using MollyNvr.Cameras.Reolink;
using MollyNvr.Domain.Streams;
using MollyNvr.Infrastructure.Cameras;
using MollyNvr.Infrastructure.Archive;
using MollyNvr.Infrastructure.Discovery;
using MollyNvr.Infrastructure.Media;
using MollyNvr.Infrastructure.Persistence;
using MollyNvr.Infrastructure.Recording;
using MollyNvr.Infrastructure.Security;
using MollyNvr.Infrastructure.Storage;

namespace MollyNvr.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var storageOptions = StorageOptions.Resolve(configuration, environment);
        services.AddSingleton(storageOptions);
        services.AddDbContextFactory<MollyNvrDbContext>(options =>
            options.UseSqlite($"Data Source={storageOptions.DatabasePath}"));

        services.AddSingleton<ICameraSecretStore, AesGcmCameraSecretStore>();
        services.AddSingleton<ICameraRepository, CameraRepository>();
        services.AddSingleton<IPortScanner, TcpPortScanner>();
        services.AddSingleton<IAliveHostScanner, PingHostScanner>();
        services.AddSingleton<IArpTableResolver, ArpTableResolver>();
        services.AddSingleton<IOuiLookup, OuiLookup>();
        services.AddHttpClient();
        services.AddSingleton<IDiscoveryProtocol, WsDiscoveryProtocol>();
        services.AddSingleton<IDiscoveryProtocol, SsdpDiscoveryProtocol>();
        services.AddSingleton<IDiscoveryProtocol, MdnsDiscoveryProtocol>();
        services.AddSingleton<IDiscoveryProtocol, SadpDiscoveryProtocol>();

        services.Configure<DiscoveryOptions>(configuration.GetSection(DiscoveryOptions.SectionName));
        services.Configure<Go2RtcOptions>(configuration.GetSection(Go2RtcOptions.SectionName));

        services.AddHttpClient<IMediaEngine, Go2RtcClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<Go2RtcOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        services.AddSingleton<StreamRegistry>();

        // Инициализация БД должна завершиться до синхронизации потоков камер с go2rtc.
        services.AddHostedService<DatabaseInitializer>();
        services.AddHostedService<Go2RtcProcessService>();

        services.AddSingleton<IFrameGrabber, Go2RtcFrameGrabber>();
        services.AddSingleton<IRtspMediaProbe, RtspMediaProbe>();
        services.AddSingleton<ICameraModule, HikvisionCameraModule>();
        services.AddSingleton<ICameraModule, OnvifCameraModule>();
        services.AddSingleton<ICameraModule, ReolinkCameraModule>();
        services.AddSingleton(serviceProvider =>
        {
            var options = ReolinkOptions.Resolve(configuration).ToBridgeOptions(storageOptions);
            var client = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            return new ReolinkBridgeClient(client, options);
        });
        services.AddSingleton<CameraProbeService>();
        services.AddSingleton<CameraRegistry>();
        services.AddSingleton<DiscoveryService>();

        var recordingOptions = RecordingOptions.Resolve(configuration, storageOptions);
        services.AddSingleton(recordingOptions);
        services.AddSingleton<IStorageProbe, StorageProbe>();
        services.AddSingleton<IRecordingStatusRegistry, RecordingStatusRegistry>();
        services.AddSingleton<RecordingSettingsNotifier>();
        services.AddSingleton<IRecordingSettingsNotifier>(serviceProvider =>
            serviceProvider.GetRequiredService<RecordingSettingsNotifier>());
        services.AddSingleton<IRecordingSettingsStore, RecordingSettingsStore>();
        services.AddSingleton<IStorageSettingsStore, StorageSettingsStore>();
        services.AddSingleton<IRecordingSegmentStore, RecordingSegmentStore>();
        services.AddSingleton<IRecordingEventStore, RecordingEventStore>();
        services.AddSingleton<SegmentIndexer>();
        services.AddSingleton<StorageRetention>();
        services.AddSingleton<RecordingService>();
        services.AddSingleton<RecordingLiveStatusService>();
        services.AddHostedService<RecordingWorker>();
        services.AddHostedService<CameraEventMonitor>();

        services.AddSingleton<IArchiveStore, ArchiveStore>();
        services.AddSingleton<ArchiveService>();
        services.AddSingleton<PlaybackSessionManager>();
        services.AddSingleton<IArchivePlaybackManager>(serviceProvider =>
            serviceProvider.GetRequiredService<PlaybackSessionManager>());
        services.AddHostedService<PlaybackCleanupService>();

        return services;
    }
}
