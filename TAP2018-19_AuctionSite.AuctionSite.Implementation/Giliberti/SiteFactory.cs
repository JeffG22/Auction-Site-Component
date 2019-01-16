using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity;
using System.Linq;
using TAP2018_19.AlarmClock.Interfaces;
using TAP2018_19.AuctionSite.Interfaces;

namespace Giliberti
{
    /// <summary>
    /// initialize the system
    /// create or load a site
    /// </summary>
    public class SiteFactory : ISiteFactory
    {
        private const int CleanUpTimeInSec = 5*60*1000;
        private Dictionary<string, Site> LoadedSite;

        private static bool NotValidConnectionString(string cs)
        {
            return string.IsNullOrWhiteSpace(cs) || !cs.Contains("Data Source=");
        }

        private void ChecksOnConnectionString(string connectionString)
        {
            if (null == connectionString)
                throw new ArgumentNullException(nameof(connectionString), "is null");
            if (NotValidConnectionString(connectionString))
                throw new UnavailableDbException(nameof(connectionString) + " malformed");
        }

        private void ChecksOnName(string name)
        {
            if (null == name)
                throw new ArgumentNullException(nameof(name), " is null");
            if (Site.NotValidName(name))
                throw new ArgumentException("It is strictly larger or smaller than the constraint", nameof(name));
        }

        internal static void ChecksOnContextAndClock(AuctionSiteContext db, IAlarmClock alarmClock)
        {
            if (db == null || alarmClock == null)
                // throw new UnavailableDbException("State of entity out of context, no data available"); BUG IOE expected
                throw new InvalidOperationException("State of entity out of context, no data available");
        }

        internal static void ChecksOnDbConnection(AuctionSiteContext db)
        {
            try
            {
                db.Database.Connection.Open();
                db.Database.Connection.Close();
            }
            catch (DbException e)
            {
                throw new UnavailableDbException("Invalid context, it was not possible to connect to the DB", e);
            }
            catch (InvalidOperationException e)
            {
                throw new UnavailableDbException("Invalid context, it was not possible to connect to the DB", e);

            }
        }

        public void Setup(string connectionString)
        {
            // constraints
            ChecksOnConnectionString(connectionString);

            // setup
            Database.Delete(connectionString); // dropping existing previous version, if any
            Database.SetInitializer(new DropCreateDatabaseAlways<AuctionSiteContext>());
            try
            {
                //using (var context = new AuctionSiteContext(connectionString))
                var context = new AuctionSiteContext(connectionString);
                //{
                    context.Database.Create();
                    context.Sites.Create();
                    context.Users.Create();
                    context.Sessions.Create();
                    context.Auctions.Create();
                    context.SaveChanges();
                //}
            }
            catch (Exception e)
            {
                throw new UnavailableDbException("error on DB Setup", e);
            }
            LoadedSite = new Dictionary<string, Site>();
        }

        public void CreateSiteOnDb(string connectionString, string name, int timezone, int sessionExpirationTimeInSeconds,
            double minimumBidIncrement)
        {
            // constraints
            ChecksOnConnectionString(connectionString);
            ChecksOnName(name);
            if (Site.NotValidTimeZone(timezone) || 
                Site.NotValidSessionExpirationTime(sessionExpirationTimeInSeconds) ||
                Site.NotValidMinimumBiddingIncr(minimumBidIncrement))
                throw new ArgumentOutOfRangeException();

            //creation
            //using (var context = new AuctionSiteContext(connectionString))
            var context = new AuctionSiteContext(connectionString);
            //{
                ChecksOnDbConnection(context);
                if(context.Sites.Any(s => s.Name == name))
                    throw new NameAlreadyInUseException(nameof(name), " of site already in use");
                context.Sites.Add(new Site(name, timezone, sessionExpirationTimeInSeconds, minimumBidIncrement));
                context.SaveChanges();
                if (LoadedSite.ContainsKey(name))
                    LoadedSite.Remove(name);
            //}
        }

        public ISite LoadSite(string connectionString, string name, IAlarmClock alarmClock)
        {
            // constraints
            ChecksOnConnectionString(connectionString);
            ChecksOnName(name);
            if (alarmClock == null)
                throw new ArgumentNullException(nameof(alarmClock), " is null");

            // attempt to Load Site
            var context = new AuctionSiteContext(connectionString);
            ChecksOnDbConnection(context);

            var site = context.Sites.Find(name); // if more than one, it throws InvalidOperationException which is okay

            if (site == null)
                throw new InexistentNameException(nameof(name), " corresponding site is not present in the DB");
            if (site.Timezone != alarmClock.Timezone)
                throw new ArgumentException("timezone is not equal to the one of the site", nameof(alarmClock));

            if (LoadedSite.ContainsKey(site.Name))
                LoadedSite.TryGetValue(site.Name, out site);
            else
            {
                LoadedSite.Add(site.Name, site);
                // "injection" of the alarm clock and  the context to make possible the queries inside ISite's methods
                site.AlarmClock = alarmClock;
                site.Db = context; // dispose entrusted to it
            }

            var alarm = alarmClock.InstantiateAlarm(CleanUpTimeInSec);
            var siteName = site.Name;
            // metodo anonimo con due riferimenti per evitare di mantenere contesto aperto
            alarm.RingingEvent += delegate() { this.MakeCleanUpSessionIfExists(alarm, connectionString, siteName); }; 

            return site;
        }

        public int GetTheTimezoneOf(string connectionString, string name)
        {
            // constraints
            ChecksOnConnectionString(connectionString);
            ChecksOnName(name);

            // attempt to Load Site and get its timezone
            int timezone;
            using (var context = new AuctionSiteContext(connectionString))
            {
                ChecksOnDbConnection(context);
                var site = context.Sites.Find(name); // if more than one, it throws InvalidOperationException which is okay
                if (site == null)
                    throw new InexistentNameException(nameof(name), " corresponding site is not present in the DB");
                timezone = site.Timezone;
            }

            return timezone;
        }

        public IEnumerable<string> GetSiteNames(string connectionString)
        {
            // constraints
            ChecksOnConnectionString(connectionString);

            // attempt to Load Site and get each site name
            using (var context = new AuctionSiteContext(connectionString))
            {
                ChecksOnDbConnection(context);
                var sites = context.Sites.Select(s => s.Name).ToList();
                return sites;
            }
        }

        // TODO provare internal dopo
        public void MakeCleanUpSessionIfExists(IAlarm alarm, string connectionString, string siteName)
        {
            ChecksOnName(siteName);
            //using (var context = new AuctionSiteContext(connectionString))
            var context = new AuctionSiteContext(connectionString);
            //{
                ChecksOnDbConnection(context);
                var site = context.Sites.Find(siteName); // if more than one, it throws InvalidOperationException which is okay

                if (site == null)
                    alarm.Dispose(); // prova a vedere se ci sono problemi
                else
                {
                    site.Db = context;
                    site.CleanupSessions();
                }
            //}
        }


    }
}
