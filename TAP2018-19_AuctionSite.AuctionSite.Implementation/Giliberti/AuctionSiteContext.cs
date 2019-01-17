using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using TAP2018_19.AuctionSite.Interfaces;

namespace Giliberti
{
    public class AuctionSiteContext : DbContext
    {
        // connection string per evitare riferimenti in IAlarm al contesto, ne viene passata una copia ai metodi anonimi
        //internal readonly string Cs;

        // codici SqlError
        private const int SqlPrimaryKeyConstraint = 2627; // violation of primary key constraint
        private const int SqlUniqueConstraint = 2601; // violation of primary unique index constraint

        // DbSet
        public DbSet<Site> Sites { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Auction> Auctions { get; set; }
        public DbSet<Session> Sessions { get; set; }

        public AuctionSiteContext(string cs) : base(cs)
        {
            //Cs = cs;
        }

        protected override void OnModelCreating(DbModelBuilder builder)
        {

        }

        public override int SaveChanges()
        {
            try
            {
                return base.SaveChanges();
            }
            catch (DbUpdateConcurrencyException error)
            {
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
