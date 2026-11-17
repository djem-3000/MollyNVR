using Microsoft.EntityFrameworkCore;
using MollyNvr.Application.Cameras;
using MollyNvr.Domain.Cameras;
using CameraCredentials = MollyNvr.Cameras.Abstractions.CameraCredentials;

namespace MollyNvr.Infrastructure.Persistence;

/// <summary>Репозиторий камер на EF Core/SQLite; учётные данные шифруются перед записью.</summary>
public sealed class CameraRepository(
    IDbContextFactory<MollyNvrDbContext> contextFactory,
    ICameraSecretStore secretStore) : ICameraRepository
{
    public async Task<IReadOnlyList<Camera>> ListAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var records = await db.Cameras
            .AsNoTracking()
            .Include(static camera => camera.Profiles)
            .OrderBy(static camera => camera.Name)
            .ToListAsync(cancellationToken);

        return records.Select(ToDomain).ToList();
    }

    public async Task<Camera?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var record = await db.Cameras
            .AsNoTracking()
            .Include(static camera => camera.Profiles)
            .FirstOrDefaultAsync(camera => camera.Id == id, cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    public async Task AddAsync(Camera camera, CameraCredentials credentials, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var record = ToRecord(camera);
        record.Credentials = secretStore.Protect(credentials);

        db.Cameras.Add(record);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        Camera camera,
        CameraCredentials? credentials,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var record = await db.Cameras.FirstOrDefaultAsync(item => item.Id == camera.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Камера {camera.Id} отсутствует в базе данных.");

        record.Name = camera.Name;
        record.Host = camera.Host;
        record.RtspPort = camera.RtspPort;
        record.VendorKey = camera.VendorKey;
        record.Vendor = camera.Vendor;
        record.Model = camera.Model;
        record.Firmware = camera.Firmware;
        record.SerialNumber = camera.SerialNumber;
        record.IsActive = camera.IsActive;
        record.MainProfileId = camera.MainProfileId;
        record.SubProfileId = camera.SubProfileId;
        record.UpdatedAt = camera.UpdatedAt;

        if (credentials is not null)
        {
            record.Credentials = secretStore.Protect(credentials);
        }

        await db.CameraProfiles
            .Where(profile => profile.CameraId == camera.Id)
            .ExecuteDeleteAsync(cancellationToken);

        db.CameraProfiles.AddRange(camera.Profiles.Select(profile => ToRecord(profile, camera)));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.CameraProfiles.Where(profile => profile.CameraId == id).ExecuteDeleteAsync(cancellationToken);
        await db.Cameras.Where(camera => camera.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<CameraCredentials?> GetCredentialsAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var payload = await db.Cameras
            .AsNoTracking()
            .Where(camera => camera.Id == id)
            .Select(camera => camera.Credentials)
            .FirstOrDefaultAsync(cancellationToken);

        return payload is null ? null : secretStore.Unprotect(payload);
    }

    public async Task<Camera?> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var record = await db.Cameras
            .Include(static camera => camera.Profiles)
            .FirstOrDefaultAsync(camera => camera.Id == id, cancellationToken);

        if (record is null)
        {
            return null;
        }

        record.IsActive = isActive;
        record.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return ToDomain(record);
    }

    private static Camera ToDomain(CameraRecord record) =>
        new(
            id: record.Id,
            name: record.Name,
            host: record.Host,
            rtspPort: record.RtspPort,
            vendorKey: record.VendorKey,
            vendor: record.Vendor,
            model: record.Model,
            firmware: record.Firmware,
            serialNumber: record.SerialNumber,
            isActive: record.IsActive,
            createdAt: record.CreatedAt,
            updatedAt: record.UpdatedAt,
            profiles: record.Profiles
                .OrderByDescending(profile => string.Equals(profile.Id, record.MainProfileId, StringComparison.Ordinal))
                .Select(ToDomain)
                .ToList(),
            mainProfileId: record.MainProfileId,
            subProfileId: record.SubProfileId);

    private static CameraProfile ToDomain(CameraProfileRecord record) =>
        new(
            Id: record.Id,
            Name: record.Name,
            Video: new CameraProfileVideo(
                Codec: record.VideoCodec,
                Width: record.Width,
                Height: record.Height,
                FrameRate: record.FrameRate,
                BitrateKbps: record.BitrateKbps),
            Audio: record.AudioCodec is null
                ? null
                : new CameraProfileAudio(
                    AudioCodec: record.AudioCodec,
                    AudioSampleRate: record.AudioSampleRate ?? 0,
                    BitrateKbps: record.AudioBitrateKbps ?? 0),
            RtspUrl: record.RtspUrl);

    private static CameraRecord ToRecord(Camera camera) =>
        new()
        {
            Id = camera.Id,
            Name = camera.Name,
            Host = camera.Host,
            RtspPort = camera.RtspPort,
            VendorKey = camera.VendorKey,
            Vendor = camera.Vendor,
            Model = camera.Model,
            Firmware = camera.Firmware,
            SerialNumber = camera.SerialNumber,
            IsActive = camera.IsActive,
            MainProfileId = camera.MainProfileId,
            SubProfileId = camera.SubProfileId,
            CreatedAt = camera.CreatedAt,
            UpdatedAt = camera.UpdatedAt,
            Profiles = camera.Profiles.Select(profile => ToRecord(profile, camera)).ToList(),
        };

    private static CameraProfileRecord ToRecord(CameraProfile profile, Camera camera) =>
        new()
        {
            CameraId = camera.Id,
            Id = profile.Id,
            Name = profile.Name,
            VideoCodec = profile.Video.Codec,
            Width = profile.Video.Width,
            Height = profile.Video.Height,
            FrameRate = profile.Video.FrameRate,
            BitrateKbps = profile.Video.BitrateKbps,
            AudioCodec = profile.Audio?.AudioCodec,
            AudioSampleRate = profile.Audio?.AudioSampleRate,
            AudioBitrateKbps = profile.Audio?.BitrateKbps,
            RtspUrl = profile.RtspUrl,
        };
}
