using Microsoft.EntityFrameworkCore;

namespace MollyNvr.Infrastructure.Persistence;

/// <summary>SQLite context: cameras, profiles, recording and the storage pool.</summary>
public sealed class MollyNvrDbContext(DbContextOptions<MollyNvrDbContext> options) : DbContext(options)
{
    public DbSet<CameraRecord> Cameras => Set<CameraRecord>();

    public DbSet<CameraProfileRecord> CameraProfiles => Set<CameraProfileRecord>();

    public DbSet<RecordingSettingsRecord> RecordingSettings => Set<RecordingSettingsRecord>();

    public DbSet<StorageSettingsRecord> StorageSettings => Set<StorageSettingsRecord>();

    public DbSet<RecordingSegmentRecord> RecordingSegments => Set<RecordingSegmentRecord>();

    public DbSet<RecordingEventRecord> RecordingEvents => Set<RecordingEventRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CameraRecord>(entity =>
        {
            entity.ToTable("cameras");
            entity.HasKey(static camera => camera.Id);
            entity.Property(static camera => camera.Name).HasMaxLength(100).IsRequired();
            entity.Property(static camera => camera.Host).HasMaxLength(253).IsRequired();
            entity.Property(static camera => camera.VendorKey).HasMaxLength(32).IsRequired();
            entity.Property(static camera => camera.Vendor).HasMaxLength(100).IsRequired();
            entity.Property(static camera => camera.Model).HasMaxLength(100).IsRequired();
            entity.Property(static camera => camera.Firmware).HasMaxLength(100).IsRequired();
            entity.Property(static camera => camera.SerialNumber).HasMaxLength(100);
            entity.Property(static camera => camera.IsActive).HasDefaultValue(true);
            entity.Property(static camera => camera.MainProfileId).HasMaxLength(64).IsRequired();
            entity.Property(static camera => camera.SubProfileId).HasMaxLength(64).IsRequired();
            entity.Property(static camera => camera.Credentials).IsRequired();
            entity.HasMany(static camera => camera.Profiles)
                .WithOne(static profile => profile.Camera)
                .HasForeignKey(static profile => profile.CameraId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CameraProfileRecord>(entity =>
        {
            entity.ToTable("camera_profiles");
            entity.HasKey(static profile => new { profile.CameraId, profile.Id });
            entity.Property(static profile => profile.Id).HasMaxLength(64);
            entity.Property(static profile => profile.Name).HasMaxLength(100).IsRequired();
            entity.Property(static profile => profile.VideoCodec).HasMaxLength(32).IsRequired();
            entity.Property(static profile => profile.AudioCodec).HasMaxLength(32);
            entity.Property(static profile => profile.RtspUrl).HasMaxLength(2048).IsRequired();
        });

        modelBuilder.Entity<RecordingSettingsRecord>(entity =>
        {
            entity.ToTable("recording_settings");
            entity.HasKey(static settings => settings.CameraId);
            entity.Property(static settings => settings.ProfileId).HasMaxLength(64).IsRequired();
            entity.Property(static settings => settings.ContinuousSchedule).HasMaxLength(400).IsRequired();
            entity.Property(static settings => settings.EventSchedule).HasMaxLength(400).IsRequired();
            entity.Property(static settings => settings.EventTypes).HasMaxLength(500).IsRequired();
            entity.HasOne<CameraRecord>()
                .WithOne()
                .HasForeignKey<RecordingSettingsRecord>(static settings => settings.CameraId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StorageSettingsRecord>(entity =>
        {
            entity.ToTable("storage_settings");
            entity.HasKey(static settings => settings.Id);
            entity.Property(static settings => settings.Path).HasMaxLength(1024).IsRequired();
        });

        modelBuilder.Entity<RecordingSegmentRecord>(entity =>
        {
            entity.ToTable("recording_segments");
            entity.HasKey(static segment => segment.Id);
            entity.Property(static segment => segment.Path).HasMaxLength(1024).IsRequired();
            entity.HasIndex(static segment => segment.StartedAt);
            entity.HasIndex(static segment => segment.CameraId);
        });

        modelBuilder.Entity<RecordingEventRecord>(entity =>
        {
            entity.ToTable("recording_events");
            entity.HasKey(static recordingEvent => recordingEvent.Id);
            entity.Property(static recordingEvent => recordingEvent.Type).HasMaxLength(100).IsRequired();
            entity.HasIndex(static recordingEvent => new { recordingEvent.CameraId, recordingEvent.StartedAt });
        });
    }
}
