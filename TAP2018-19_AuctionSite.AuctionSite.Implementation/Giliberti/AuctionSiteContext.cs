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
        internal readonly string Cs;

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
            Cs = cs;
        }

        protected override void OnModelCreating(DbModelBuilder builder)
        {
            // TODO capire come funziona e se ho qualcosa da fare, se cancello un commento non cancello la segnalazione o viceversa?
            // builder.Entity<Commento>().HasRequired(p => p.Segnalazione).WithMany(b => b.Commentos).WillCascadeOnDelete(false);
            // TODO vedere fluent API
            // TODO gestire i cascade e update del database
            // TODO le cose per gestire la concorrenza tipo IsRowVersion e Concurrency Token

        }

        public override int SaveChanges()
        {
            try
            {
                return base.SaveChanges();
            }
            // TODO se volessi recuperare lo stato http://www.binaryintellect.net/articles/c1bff938-1789-4501-8161-3f38bc465a8b.aspx
            catch (DbUpdateConcurrencyException error)
            {
                // considerare di vedere quali entità hanno creato problemi con Entries o visto che passo error sarebbe una ripetizione
                throw new ConcurrentChangeException("Attempt to update an entity which has been concurrently modified", error);
            }
            catch (DbEntityValidationException error)
            {
                throw new ArgumentException("Validation of entity property failed", error);
            }
            catch (DbUpdateException error)
            {
                var sqlException = error.GetBaseException() as SqlException;

                if (sqlException == null)
                    throw new UnavailableDbException("Failure to persist or retrieve data to/from DB", error);
                if (sqlException.ErrorCode == SqlPrimaryKeyConstraint)
                    throw new NameAlreadyInUseException(error.Entries.ToString(), "Attempt to insert a duplicated primary key", error);
                else if (sqlException.ErrorCode == SqlUniqueConstraint)
                    throw new NameAlreadyInUseException(error.Entries.ToString(), "Attempt to insert a duplicated unique index", error);
                else
                    throw new UnavailableDbException("sqlException occurred sending updates to the database", error);

            }
        }

    }
}
