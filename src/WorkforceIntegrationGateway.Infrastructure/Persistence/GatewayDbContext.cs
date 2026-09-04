using Microsoft.EntityFrameworkCore;

namespace WorkforceIntegrationGateway.Infrastructure.Persistence;

public sealed class GatewayDbContext(DbContextOptions<GatewayDbContext> options) : DbContext(options)
{
    internal DbSet<VerificationRequestRecord> VerificationRequests => Set<VerificationRequestRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var request = modelBuilder.Entity<VerificationRequestRecord>();
        request.ToTable("verification_requests", "gateway", table =>
        {
            table.HasCheckConstraint("ck_verification_requests_client_reference", "client_reference ~ '^[A-Za-z0-9._-]{1,100}$'");
            table.HasCheckConstraint("ck_verification_requests_employee_reference", "employee_reference ~ '^[A-Za-z0-9._-]{1,100}$'");
            table.HasCheckConstraint("ck_verification_requests_employer_reference", "employer_reference ~ '^[A-Za-z0-9._-]{1,100}$'");
            table.HasCheckConstraint("ck_verification_requests_status", "status = 'Pending'");
        });
        request.HasKey(item => item.Id);
        request.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        request.Property(item => item.ClientReference).HasColumnName("client_reference").HasMaxLength(100).IsRequired();
        request.Property(item => item.EmployeeReference).HasColumnName("employee_reference").HasMaxLength(100).IsRequired();
        request.Property(item => item.EmployerReference).HasColumnName("employer_reference").HasMaxLength(100).IsRequired();
        request.Property(item => item.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        request.Property(item => item.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone").IsRequired();

        var requestedData = modelBuilder.Entity<RequestedDataRecord>();
        requestedData.ToTable("verification_request_data", "gateway", table =>
        {
            table.HasCheckConstraint(
                "ck_verification_request_data_value",
                "value IN ('EmploymentStatus', 'JobTitle', 'EmploymentDates')");
            table.HasCheckConstraint("ck_verification_request_data_ordinal", "ordinal BETWEEN 0 AND 2");
        });
        requestedData.HasKey(item => new { item.VerificationRequestId, item.Value });
        requestedData.HasIndex(item => new { item.VerificationRequestId, item.Ordinal }).IsUnique();
        requestedData.Property(item => item.VerificationRequestId).HasColumnName("verification_request_id");
        requestedData.Property(item => item.Value).HasColumnName("value").HasMaxLength(32).IsRequired();
        requestedData.Property(item => item.Ordinal).HasColumnName("ordinal").IsRequired();
        requestedData.HasOne(item => item.VerificationRequest)
            .WithMany(item => item.RequestedData)
            .HasForeignKey(item => item.VerificationRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
