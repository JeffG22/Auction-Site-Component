using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using TAP2018_19.AuctionSite.Interfaces;

namespace Giliberti
{
    public class AuctionSiteContext : DbContext
    {

        // codici SqlError
        private const int SqlPrimaryKeyConstraint = 2627; // violation of primary key constraint
        private const int SqlUniqueConstraint = 2601; // violation of primary unique index constraint

        // DbSet
        public DbSet<SiteEntity> Sites { get; set; }
        public DbSet<UserEntity> Users { get; set; }
        public DbSet<AuctionEntity> Auctions { get; set; }
        public DbSet<SessionEntity> Sessions { get; set; }

        public AuctionSiteContext(string cs) : base(cs)
        {
        }

        protected override void OnModelCreating(DbModelBuilder builder)
        {
            builder.Entity<SiteEntity>().Property(p => p.RowVersion).IsRowVersion();
            builder.Entity<UserEntity>().Property(p => p.RowVersion).IsRowVersion();
            builder.Entity<AuctionEntity>().Property(p => p.RowVersion).IsRowVersion();
            builder.Entity<SessionEntity>().Property(p => p.RowVersion).IsRowVersion();
        }

        public override int SaveChanges()
        {
            try
            {
                return base.SaveChanges();
            }
            catch (DbUpdateConcurrencyException error)
            {
                Debug.WriteLine("entity: "+error.Entries.First().Entity);
                Debug.WriteLine("value: "+error.Entries.First().CurrentValues);
                throw new ConcurrentChangeException("Attempt to update an entity which has been concurrently modified", error);
            }
            catch (DbEntityValidationException error)
            {
                throw new ArgumentException("Validation of entity property failed", error);
            }
            catch (DbUpdateException error)
            {
                if (!(error.GetBaseException() is SqlException sqlException))
                    throw new UnavailableDbException("Failure to persist or retrieve data to/from DB", error);
                switch (sqlException.Number)
                {
                    case SqlPrimaryKeyConstraint:
                        throw new NameAlreadyInUseException(error.Entries.ToString(), "Attempt to insert a duplicated primary key", error);
                    case SqlUniqueConstraint:
                        throw new NameAlreadyInUseException(error.Entries.ToString(), "Attempt to insert a duplicated unique index", error);
                    default:
                        throw new UnavailableDbException("sqlException occurred sending updates to the database", error);
                }
            }
        }

    }
}
