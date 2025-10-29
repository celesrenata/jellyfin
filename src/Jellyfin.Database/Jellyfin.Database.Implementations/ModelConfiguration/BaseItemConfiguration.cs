using System;
using Jellyfin.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Jellyfin.Database.Implementations.ModelConfiguration;

/// <summary>
/// Configuration for BaseItem.
/// </summary>
public class BaseItemConfiguration : IEntityTypeConfiguration<BaseItemEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BaseItemEntity> builder)
    {
        builder.HasKey(e => e.Id);

        // PATCH: Add value converters for datetime columns to handle VARCHAR to DateTime conversion
        var dateTimeConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v,
            v => v.HasValue ? DateTime.Parse(v.ToString()!, System.Globalization.CultureInfo.InvariantCulture) : null);

        builder.Property(e => e.DateCreated).HasConversion(dateTimeConverter);
        builder.Property(e => e.DateModified).HasConversion(dateTimeConverter);
        builder.Property(e => e.DateLastRefreshed).HasConversion(dateTimeConverter);
        builder.Property(e => e.DateLastSaved).HasConversion(dateTimeConverter);
        builder.Property(e => e.DateLastMediaAdded).HasConversion(dateTimeConverter);
        builder.Property(e => e.PremiereDate).HasConversion(dateTimeConverter);
        builder.Property(e => e.EndDate).HasConversion(dateTimeConverter);
        builder.Property(e => e.StartDate).HasConversion(dateTimeConverter);

        // TODO: See rant in entity file.
        // builder.HasOne(e => e.Parent).WithMany(e => e.DirectChildren).HasForeignKey(e => e.ParentId);
        // builder.HasOne(e => e.TopParent).WithMany(e => e.AllChildren).HasForeignKey(e => e.TopParentId);
        // builder.HasOne(e => e.Season).WithMany(e => e.SeasonEpisodes).HasForeignKey(e => e.SeasonId);
        // builder.HasOne(e => e.Series).WithMany(e => e.SeriesEpisodes).HasForeignKey(e => e.SeriesId);
        builder.HasMany(e => e.Peoples);
        builder.HasMany(e => e.UserData);
        builder.HasMany(e => e.ItemValues);
        builder.HasMany(e => e.MediaStreams);
        builder.HasMany(e => e.Chapters);
        builder.HasMany(e => e.Provider);
        builder.HasMany(e => e.Parents);
        builder.HasMany(e => e.Children);
        builder.HasMany(e => e.DirectChildren).WithOne(e => e.DirectParent).HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.LockedFields);
        builder.HasMany(e => e.TrailerTypes);
        builder.HasMany(e => e.Images);

        builder.HasIndex(e => e.Path);
        builder.HasIndex(e => e.ParentId);
        builder.HasIndex(e => e.PresentationUniqueKey);
        builder.HasIndex(e => new { e.Id, e.Type, e.IsFolder, e.IsVirtualItem });

        // covering index
        builder.HasIndex(e => new { e.TopParentId, e.Id });
        // series
        builder.HasIndex(e => new { e.Type, e.SeriesPresentationUniqueKey, e.PresentationUniqueKey, e.SortName });
        // series counts
        // seriesdateplayed sort order
        builder.HasIndex(e => new { e.Type, e.SeriesPresentationUniqueKey, e.IsFolder, e.IsVirtualItem });
        // live tv programs
        builder.HasIndex(e => new { e.Type, e.TopParentId, e.StartDate });
        // covering index for getitemvalues
        builder.HasIndex(e => new { e.Type, e.TopParentId, e.Id });
        // used by movie suggestions
        builder.HasIndex(e => new { e.Type, e.TopParentId, e.PresentationUniqueKey });
        // latest items
        builder.HasIndex(e => new { e.Type, e.TopParentId, e.IsVirtualItem, e.PresentationUniqueKey, e.DateCreated });
        builder.HasIndex(e => new { e.IsFolder, e.TopParentId, e.IsVirtualItem, e.PresentationUniqueKey, e.DateCreated });
        // resume
        builder.HasIndex(e => new { e.MediaType, e.TopParentId, e.IsVirtualItem, e.PresentationUniqueKey });

        builder.HasData(new BaseItemEntity()
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Type = "PLACEHOLDER",
            Name = "This is a placeholder item for UserData that has been detacted from its original item",
        });
    }
}
