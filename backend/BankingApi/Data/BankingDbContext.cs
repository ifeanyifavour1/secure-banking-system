
using Microsoft.EntityFrameworkCore;
using BankingApi.Models;


namespace BankingApi.Data
{
    public class BankingDbContext : DbContext
    {

        public BankingDbContext(DbContextOptions<BankingDbContext> options)
        : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<BankingApi.Models.Transaction> Transactions { get; set; }
        public DbSet<TransactionEntry> TransactionEntries { get; set; }
        public DbSet<TransactionState> TransactionStates { get; set; }
        public DbSet<AccountLimit> AccountLimits { get; set; }
        public DbSet<AuditLogEntry> AuditLogEntries { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.UserId);

                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.FirstName).HasColumnName("first_name");
                entity.Property(e => e.LastName).HasColumnName("last_name");
                entity.Property(e => e.Email).HasColumnName("email");
                entity.Property(e => e.PhoneNumber).HasColumnName("phone_number");
                entity.Property(e => e.PasswordHash).HasColumnName("password_hash").HasColumnType("bytea");
                entity.Property(e => e.PasswordSalt).HasColumnName("password_salt").HasColumnType("bytea");
                entity.Property(e => e.DateOfBirth).HasColumnName("date_of_birth").HasColumnType("date");
                entity.Property(e => e.NationalId).HasColumnName("national_id");
                entity.Property(e => e.AddressLine1).HasColumnName("address_line1");
                entity.Property(e => e.AddressLine2).HasColumnName("address_line2");
                entity.Property(e => e.City).HasColumnName("city");
                entity.Property(e => e.Country).HasColumnName("country");
                entity.Property(e => e.PostalCode).HasColumnName("postal_code");
                entity.Property(e => e.Role).HasColumnName("role");
                entity.Property(e => e.MfaEnabled).HasColumnName("mfa_enabled");
                entity.Property(e => e.MfaSecret).HasColumnName("mfa_secret").HasColumnType("bytea");
                entity.Property(e => e.IsActive).HasColumnName("is_active");
                entity.Property(e => e.IsLocked).HasColumnName("is_locked");
                entity.Property(e => e.FailedLoginCount).HasColumnName("failed_login_count");
                entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at").HasColumnType("timestamp with time zone");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.NationalId).IsUnique();
            });

            modelBuilder.Entity<Account>(entity =>
            {
                entity.ToTable("accounts");
                entity.HasKey(e => e.AccountId);

                entity.Property(e => e.AccountId).HasColumnName("account_id");
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.AccountNumber).HasColumnName("account_number");
                entity.Property(e => e.AccountType).HasColumnName("account_type");
                entity.Property(e => e.Currency).HasColumnName("currency");
                entity.Property(e => e.Balance).HasColumnName("balance").HasPrecision(18, 2);
                entity.Property(e => e.AvailableBalance).HasColumnName("available_balance").HasPrecision(18, 2);
                entity.Property(e => e.InterestRate).HasColumnName("interest_rate").HasPrecision(5, 4);
                entity.Property(e => e.Status).HasColumnName("status");
                entity.Property(e => e.OpenedAt).HasColumnName("opened_at").HasColumnType("timestamp with time zone");
                entity.Property(e => e.ClosedAt).HasColumnName("closed_at").HasColumnType("timestamp with time zone");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

                entity.HasIndex(e => e.AccountNumber).IsUnique();

                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<BankingApi.Models.Transaction>(entity =>
            {
                entity.ToTable("transactions");
                entity.HasKey(e => e.TransactionId);

                entity.Property(e => e.TransactionId).HasColumnName("transaction_id");
                entity.Property(e => e.ReferenceNumber).HasColumnName("reference_number");
                entity.Property(e => e.TransactionType).HasColumnName("transaction_type");
                entity.Property(e => e.Amount).HasColumnName("amount").HasPrecision(18, 2);
                entity.Property(e => e.Currency).HasColumnName("currency");
                entity.Property(e => e.Description).HasColumnName("description");
                entity.Property(e => e.StateId).HasColumnName("state_id");
                entity.Property(e => e.SourceAccountId).HasColumnName("source_account_id");
                entity.Property(e => e.DestAccountId).HasColumnName("dest_account_id");
                entity.Property(e => e.InitiatedBy).HasColumnName("initiated_by");
                entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasColumnType("inet");
                entity.Property(e => e.Channel).HasColumnName("channel");
                entity.Property(e => e.ScheduledAt).HasColumnName("scheduled_at").HasColumnType("timestamp with time zone");
                entity.Property(e => e.ProcessedAt).HasColumnName("processed_at").HasColumnType("timestamp with time zone");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

                entity.HasIndex(e => e.ReferenceNumber).IsUnique();

                entity.HasOne<TransactionState>()
                    .WithMany()
                    .HasForeignKey(e => e.StateId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<Account>()
                    .WithMany()
                    .HasForeignKey(e => e.SourceAccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<Account>()
                    .WithMany()
                    .HasForeignKey(e => e.DestAccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(e => e.InitiatedBy)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TransactionState>(entity =>
            {
                entity.ToTable("transaction_states");
                entity.HasKey(e => e.StateId);

                entity.Property(e => e.StateId).HasColumnName("state_id");
                entity.Property(e => e.StateName).HasColumnName("state_name");
                entity.Property(e => e.Description).HasColumnName("description");

                entity.HasIndex(e => e.StateName).IsUnique();
            });

            modelBuilder.Entity<TransactionEntry>(entity =>
            {
                entity.ToTable("transaction_entries");
                entity.HasKey(e => e.EntryId);

                entity.Property(e => e.EntryId).HasColumnName("entry_id");
                entity.Property(e => e.TransactionId).HasColumnName("transaction_id");
                entity.Property(e => e.AccountId).HasColumnName("account_id");
                entity.Property(e => e.EntryType).HasColumnName("entry_type");
                entity.Property(e => e.Amount).HasColumnName("amount").HasPrecision(18, 2);
                entity.Property(e => e.BalanceBefore).HasColumnName("balance_before").HasPrecision(18, 2);
                entity.Property(e => e.BalanceAfter).HasColumnName("balance_after").HasPrecision(18, 2);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");

                entity.HasOne<BankingApi.Models.Transaction>()
                    .WithMany()
                    .HasForeignKey(e => e.TransactionId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<Account>()
                    .WithMany()
                    .HasForeignKey(e => e.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AccountLimit>(entity =>
            {
                entity.ToTable("account_limits");
                entity.HasKey(e => e.LimitId);

                entity.Property(e => e.LimitId).HasColumnName("limit_id");
                entity.Property(e => e.AccountId).HasColumnName("account_id");
                entity.Property(e => e.LimitType).HasColumnName("limit_type");
                entity.Property(e => e.MaxAmount).HasColumnName("max_amount").HasPrecision(18, 2);
                entity.Property(e => e.CurrentUsage).HasColumnName("current_usage").HasPrecision(18, 2);
                entity.Property(e => e.UsageResetAt).HasColumnName("usage_reset_at").HasColumnType("timestamp with time zone");
                entity.Property(e => e.IsActive).HasColumnName("is_active");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

                entity.HasIndex(e => new { e.AccountId, e.LimitType }).IsUnique();

                entity.HasOne<Account>()
                    .WithMany()
                    .HasForeignKey(e => e.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AuditLogEntry>(entity =>
            {
                entity.ToTable("audit_log");
                entity.HasKey(e => e.LogId);

                entity.Property(e => e.LogId).HasColumnName("log_id");
                entity.Property(e => e.EventType).HasColumnName("event_type");
                entity.Property(e => e.EntityType).HasColumnName("entity_type");
                entity.Property(e => e.EntityId).HasColumnName("entity_id");
                entity.Property(e => e.Action).HasColumnName("action");
                entity.Property(e => e.PerformedBy).HasColumnName("performed_by");
                entity.Property(e => e.OldValues).HasColumnName("old_values").HasColumnType("jsonb");
                entity.Property(e => e.NewValues).HasColumnName("new_values").HasColumnType("jsonb");
                entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasColumnType("inet");
                entity.Property(e => e.UserAgent).HasColumnName("user_agent");
                entity.Property(e => e.AdditionalInfo).HasColumnName("additional_info").HasColumnType("jsonb");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");

                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(e => e.PerformedBy)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }

    }
}