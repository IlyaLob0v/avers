using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

public sealed class AversDbContext : DbContext
{
    public AversDbContext(DbContextOptions<AversDbContext> options) : base(options) { }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AdvanceRevisions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AdvanceRevisions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void AdvanceRevisions()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Modified && e.Metadata.FindProperty("Revision") is not null))
        {
            var revision = entry.Property("Revision");
            revision.CurrentValue = checked((long)revision.OriginalValue! + 1);
        }
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Street> Streets => Set<Street>();
    public DbSet<House> Houses => Set<House>();
    public DbSet<HouseScheme> HouseSchemes => Set<HouseScheme>();
    public DbSet<HouseSchemeFile> HouseSchemeFiles => Set<HouseSchemeFile>();
    public DbSet<PprEntry> PprEntries => Set<PprEntry>();
    public DbSet<Subscriber> Subscribers => Set<Subscriber>();
    public DbSet<YearPayment> Payments => Set<YearPayment>();
    public DbSet<PaymentYearTotal> PaymentYearTotals => Set<PaymentYearTotal>();
    public DbSet<PaymentOperation> PaymentOperations => Set<PaymentOperation>();
    public DbSet<JackPayment> JackPayments => Set<JackPayment>();
    public DbSet<WorkTicket> Tickets => Set<WorkTicket>();
    public DbSet<CourtCase> CourtCases => Set<CourtCase>();
    public DbSet<SubscriberDocument> SubscriberDocuments => Set<SubscriberDocument>();
    public DbSet<UnifiedReceipt> UnifiedReceipts => Set<UnifiedReceipt>();
    public DbSet<Mechanic> Mechanics => Set<Mechanic>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<IntercomPanel> IntercomPanels => Set<IntercomPanel>();
    public DbSet<IntercomTicket> IntercomTickets => Set<IntercomTicket>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();
    public DbSet<ChangeLogEntry> ChangeLog => Set<ChangeLogEntry>();
    public DbSet<AccessLogEntry> AccessLog => Set<AccessLogEntry>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDbFunction(typeof(SubscriberSearch).GetMethod(nameof(SubscriberSearch.PhoneDigits))!)
            .HasName("avers_phone_digits").IsNullable(false);
        model.Entity<Organization>(e =>
        {
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Branch).HasMaxLength(200);
            e.Property(x => x.Address).HasMaxLength(300);
            e.Property(x => x.Phones).HasMaxLength(200);
        });

        model.Entity<Street>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        model.Entity<House>(e =>
        {
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.HasOne(x => x.Street).WithMany(x => x.Houses).HasForeignKey(x => x.StreetId).OnDelete(DeleteBehavior.NoAction);
            e.Property(x => x.Number).HasMaxLength(20).IsRequired();
            e.Property(x => x.PzhreuIndex).HasMaxLength(16);
            e.Property(x => x.PzhreuAccount).HasMaxLength(32);
            e.Property(x => x.Zone).HasMaxLength(80);
            e.HasIndex(x => new { x.StreetId, x.Number }).IsUnique();
            e.HasIndex(x => x.PzhreuAccount);
            e.Ignore(x => x.AddressLine);
        });

        model.Entity<HouseSchemeFile>(e =>
        {
            e.HasKey(x => x.HouseSchemeId);
            e.HasOne(x => x.Scheme).WithOne().HasForeignKey<HouseSchemeFile>(x => x.HouseSchemeId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<Subscriber>(e =>
        {
            e.Property(x => x.LastPaymentDate).IsConcurrencyToken();
            e.HasOne(x => x.House).WithMany(x => x.Subscribers).HasForeignKey(x => x.HouseId).OnDelete(DeleteBehavior.NoAction);
            e.Property(x => x.LastName).HasMaxLength(80).IsRequired();
            e.Property(x => x.FirstName).HasMaxLength(80);
            e.Property(x => x.Patronymic).HasMaxLength(80);
            e.Property(x => x.Account).HasMaxLength(32).IsRequired();
            e.Property(x => x.InnerCode).HasMaxLength(32).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(40);
            e.Property(x => x.Apartment).HasMaxLength(16);
            e.Property(x => x.Section).HasMaxLength(8);
            e.Property(x => x.PzhreuIndex).HasMaxLength(16);
            e.Property(x => x.PzhreuAccount).HasMaxLength(40);
            e.Property(x => x.OekAccount).HasMaxLength(20);
            e.Property(x => x.ContractNumber).HasMaxLength(40);
            e.Property(x => x.ContractHolder).HasMaxLength(250);
            e.Property(x => x.Inn).HasMaxLength(12);
            e.Property(x => x.Kpp).HasMaxLength(9);
            e.Property(x => x.Ogrn).HasMaxLength(15);
            e.Property(x => x.LegalAddress).HasMaxLength(250);
            e.Property(x => x.Director).HasMaxLength(250);
            e.HasIndex(x => x.Account).IsUnique();
            e.HasIndex(x => x.InnerCode);
            e.HasIndex(x => x.OekAccount);
            e.HasIndex(x => x.LastName);
            e.Ignore(x => x.FullName);
            e.Ignore(x => x.ShortName);
            e.Ignore(x => x.ContractParty);
            e.Ignore(x => x.ClientKind);
        });

        model.Entity<YearPayment>(e =>
        {
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.Property(x => x.Tariff).HasPrecision(12, 2);
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Source).HasMaxLength(40);
            e.Property(x => x.RecordedBy).HasMaxLength(120);
            e.Property(x => x.PayerName).HasMaxLength(250);
            e.Property(x => x.ImportedFromJack).HasDefaultValue(false);
            e.HasIndex(x => new { x.SubscriberId, x.Year, x.Month }).IsUnique();
            e.HasIndex(x => new { x.Year, x.Month });
            e.HasIndex(x => x.PaidAt);
        });

        model.Entity<PaymentYearTotal>(e =>
        {
            e.HasKey(x => new { x.SubscriberId, x.Year });
            e.Property(x => x.Charged).HasPrecision(18, 2);
            e.Property(x => x.Paid).HasPrecision(18, 2);
            e.HasOne<Subscriber>().WithMany().HasForeignKey(x => x.SubscriberId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<JackPayment>(e =>
        {
            e.ToTable("payment_history", "jack_archive");
            e.Property(x => x.Id).HasColumnName("payid").ValueGeneratedNever();
            e.Property(x => x.SubscriberId).HasColumnName("subscriber_id");
            e.Property(x => x.Period).HasColumnName("period").HasColumnType("timestamp without time zone");
            e.Property(x => x.PaidAt).HasColumnName("paid_at").HasColumnType("timestamp without time zone");
            e.Property(x => x.Amount).HasColumnName("amount");
            e.Property(x => x.PayTypeId).HasColumnName("pay_type_id");
            e.Property(x => x.ConnectionTypeId).HasColumnName("connection_type_id");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp without time zone");
            e.Property(x => x.Source).HasColumnName("source");
            e.Property(x => x.RecordedBy).HasColumnName("recorded_by");
            e.HasIndex(x => new { x.SubscriberId, x.Period });
            e.HasIndex(x => x.PaidAt);
        });

        model.Entity<WorkTicket>(e =>
        {
            e.HasOne(x => x.House).WithMany(x => x.Tickets).HasForeignKey(x => x.HouseId).OnDelete(DeleteBehavior.NoAction);
            e.Property(x => x.Fault).HasMaxLength(500);
            e.Property(x => x.WorkDone).HasMaxLength(500);
            e.Property(x => x.Mechanic).HasMaxLength(120);
            e.HasIndex(x => x.Number).IsUnique();
            e.HasIndex(x => x.Date);
            e.HasIndex(x => new { x.HouseId, x.Date });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.AcceptedByUserId).OnDelete(DeleteBehavior.SetNull);
            e.Ignore(x => x.StatusLabel);
            e.Ignore(x => x.KindLabel);
        });

        model.Entity<CourtCase>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.OrderNumber).HasMaxLength(40);
            e.Property(x => x.StateDuty).HasPrecision(12, 2);
            e.Property(x => x.WritSentTo).HasMaxLength(40);
            e.Property(x => x.Receipts).HasMaxLength(Data.CourtCases.MaxTextLength);
            e.Property(x => x.Notes).HasMaxLength(Data.CourtCases.MaxTextLength);
        });

        model.Entity<SubscriberDocument>(e =>
        {
            e.Property(x => x.Kind).HasMaxLength(40);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Format).HasMaxLength(10);
            e.Property(x => x.AddedBy).HasMaxLength(200);
            e.HasOne(x => x.Subscriber).WithMany().HasForeignKey(x => x.SubscriberId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<UnifiedReceipt>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Paid).HasPrecision(12, 2);
            e.Property(x => x.NOs).HasMaxLength(20);
            e.Property(x => x.NKass).HasMaxLength(10);
            e.Property(x => x.NOper).HasMaxLength(20);
            e.Property(x => x.NKvit).HasMaxLength(20);
        });

        model.Entity<AppUser>(e =>
        {
            e.Property(x => x.Login).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.Login).IsUnique();
        });

        model.Entity<AppSetting>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(80);
            e.Property(x => x.Value).HasMaxLength(400).IsRequired();
        });

        model.Entity<ChangeLogEntry>(e =>
        {
            e.Property(x => x.UserName).HasMaxLength(200);
            e.Property(x => x.Action).HasMaxLength(100);
            e.Property(x => x.Target).HasMaxLength(400);
        });

        model.Entity<AccessLogEntry>(e =>
        {
            e.Property(x => x.UserName).HasMaxLength(200);
            e.Property(x => x.Action).HasMaxLength(100);
            e.Property(x => x.Target).HasMaxLength(400);
        });

        model.Entity<Mechanic>().Property(x => x.FullName).HasMaxLength(120).IsRequired();
        model.Entity<Material>().Property(x => x.Name).HasMaxLength(200).IsRequired();
        model.Entity<HouseScheme>().HasOne(x => x.House).WithMany(x => x.Schemes).HasForeignKey(x => x.HouseId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<PprEntry>().HasOne(x => x.House).WithMany(x => x.Ppr).HasForeignKey(x => x.HouseId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<IntercomPanel>().HasOne(x => x.House).WithMany(x => x.Intercoms).HasForeignKey(x => x.HouseId).OnDelete(DeleteBehavior.NoAction);
        model.Entity<IntercomTicket>().HasOne(x => x.House).WithMany().HasForeignKey(x => x.HouseId).OnDelete(DeleteBehavior.NoAction);
    }
}
