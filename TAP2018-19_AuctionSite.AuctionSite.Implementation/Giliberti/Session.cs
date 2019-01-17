using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using TAP2018_19.AlarmClock.Interfaces;
using TAP2018_19.AuctionSite.Interfaces;

namespace Giliberti
{
    /// <summary>
    /// start with a login, end by logout or inactivity of the user owner
    /// user access its session by 
    /// closed -> dropped each five minutes or by explicit request of cleanup
    /// </summary>
    public partial class Session
    {
        [NotMapped] internal IAlarmClock AlarmClock { get; set; }
        [NotMapped] internal AuctionSiteContext Db { get; set; }

        public Session()
        {
            Db = null;
            AlarmClock = null;
        }

        public Session(DateTime validUntil, string username, string siteName)
        {
            Id = siteName+username;
            ValidUntil = validUntil;
            Username = username;
            SiteName = siteName;
            AlarmClock = null;
            Db = null;
        }
        internal void ResetTime(int seconds)
        {
            if (AlarmClock == null)
                throw new UnavailableDbException("State of entity out of context, no data available");
            ValidUntil = AlarmClock.Now.AddSeconds(seconds);
        }
        
        // member functions
        public bool IsValid()
        {
            // controllo che sia correttamente creata e presente sul DB
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (!Db.Sessions.Any(s => s.Id == Id))
                throw new InvalidOperationException(nameof(Session)+" not consistent");

            return ValidUntil.CompareTo(AlarmClock.Now) > 0; // Ritorna maggiore di zero se la scadenza è dopo l'ora attuale
        }

        public void Logout()
        {
            if (Db == null)
                throw new InvalidOperationException("State of entity out of context, no data available");
            SiteFactory.ChecksOnDbConnection(Db);

            if (!Db.Sessions.Any(s => s.Id == Id))
                throw new InvalidOperationException(nameof(Session) + " not consistent");

            ValidUntil = DateTime.Now.ToUniversalTime().AddHours(-24); // Expired
            Db.Sessions.Remove(this);
            Db.SaveChanges();
            Db = null;
            AlarmClock = null;
        }

        public IAuction CreateAuction(string description, DateTime endsOn, double startingPrice)
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);

            if (description == null)
                throw new ArgumentNullException(nameof(description), "is null");
            if (string.IsNullOrEmpty(description))
                throw new ArgumentException("is empty", nameof(description));
            if (startingPrice < 0)
                throw new ArgumentOutOfRangeException(nameof(startingPrice), "is negative");
            if (endsOn.CompareTo(AlarmClock.Now) < 0)
                throw new UnavailableTimeMachineException("endsOn precedes the current ISite's time");
            if (!Db.Sessions.Any(s => s.Id == Id))
                throw new InvalidOperationException(nameof(Session) + " not consistent");

            Site siteSession;
            siteSession = Db.Sites.SingleOrDefault(site => site.Name == SiteName);
            if (siteSession == null)
                throw new InvalidOperationException("the site does not exist anymore");
            User seller;
            seller = Db.Users.SingleOrDefault(s => s.SiteName == siteSession.Name && s.Username == Username);
            if (seller == null)
                throw new InvalidOperationException("the user does not exist anymore");

            var time = siteSession.SessionExpirationInSeconds;
            var auction =
                new Auction(description, endsOn, startingPrice, Username, SiteName) {Seller = seller};
            Db.Auctions.Add(auction);
            ResetTime(time);
            Db.SaveChanges();

            auction.Db = Db;
            auction.AlarmClock = AlarmClock;
            return auction;
        }
    }
}
