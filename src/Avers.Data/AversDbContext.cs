using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

public sealed class AversDbContext : DbContext
{
    public AversDbContext(DbContextOptions<AversDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Street> Streets => Set<Street>();
    public DbSet<House> Houses => Set<House>();
    public DbSet<HouseScheme> HouseSchemes => Set<HouseScheme>();
    public DbSet<HouseSchemeFile> HouseSchemeFiles => Set<HouseSchemeFile>();
    public DbSet<PprEntry> PprEntries => Set<PprEntry>();
    public DbSet<Subscriber> Subscribers => Set<Subscriber>();
    public DbSet<YearPayment> Payments => Set<YearPayment>();
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

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Organization>(e =>
        {
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
            e.Property(x => x.ContractNumber).HasMaxLength(40);
            e.Property(x => x.ContractHolder).HasMaxLength(250);
            e.HasIndex(x => x.Account).IsUnique();
            e.HasIndex(x => x.InnerCode);
            e.HasIndex(x => x.LastName);
            e.Ignore(x => x.FullName);
            e.Ignore(x => x.ShortName);
            e.Ignore(x => x.ContractParty);
        });

        model.Entity<YearPayment>(e =>
        {
            e.Property(x => x.Tariff).HasPrecision(12, 2);
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Source).HasMaxLength(40);
            e.Property(x => x.RecordedBy).HasMaxLength(120);
            e.Property(x => x.PayerName).HasMaxLength(250);
            e.HasIndex(x => new { x.SubscriberId, x.Year, x.Month }).IsUnique();
        });

        model.Entity<WorkTicket>(e =>
        {
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

        model.Entity<Mechanic>().Property(x => x.FullName).HasMaxLength(120).IsRequired();
        model.Entity<Material>().Property(x => x.Name).HasMaxLength(200).IsRequired();
    }
}
