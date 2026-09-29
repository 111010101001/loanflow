using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoanFlow.Infrastructure.Ledger.Configuration;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> message)
    {
        message.ToTable("OutboxMessages");
        message.HasKey(m => m.Id);
        message.Property(m => m.RoutingKey).HasMaxLength(100);
        message.Property(m => m.Type).HasMaxLength(200);
        message.Property(m => m.LastError).HasMaxLength(2000);

        // TODO(step 4): the relay keeps asking for "unprocessed, oldest first". A filtered index
        // covers exactly those rows and stays small as processed rows pile up:
        //   message.HasIndex(m => m.OccurredAtUtc).HasFilter("[ProcessedAtUtc] IS NULL");
    }
}
