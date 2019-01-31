using System;
using System.Linq;
using TAP2018_19.AuctionSite.Interfaces;

namespace Giliberti
{
    /// <summary>
    /// Session is a logic class to represent the sessions entities in the db
    /// This partial class implements its methods according to the interface ISession
    /// starts with a login, ends by logout or inactivity of the user owner
    /// closed -> dropped each five minutes or by explicit request of cleanup
    /// </summary>
    public partial class Session
    {
        // to reset the expiration time
        internal void ResetTime(int seconds)
        {
            SiteFactory.ChecksOnCsAndClock(Cs, AlarmClock);
            using (var Db = new AuctionSiteContext(Cs))
            {
                SiteFactory.ChecksOnDbConnection(Db);
                var sessionEntity = Db.Sessions.SingleOrDefault(s => s.Id == Id);
                if (sessionEntity == null)
                    throw new InvalidOperationException("the session does not exist anymore");
                sessionEntity.ValidUntil = AlarmClock.Now.AddSeconds(seconds);
                ValidUntil = sessionEntity.ValidUntil;
                Db.SaveChanges();
            }
        }
        
        // to checks if it is valid that is correctly present on the db and not expired
        public bool IsValid()
        {
            // controllo che sia correttamente creata e presente sul DB
            SiteFactory.ChecksOnCsAndClock(Cs, AlarmClock);
            using (var Db = new AuctionSiteContext(Cs))
            {
                SiteFactory.ChecksOnDbConnection(Db);
                var sessionEntity = Db.Sessions.SingleOrDefault(s => s.Id == Id);
                if (null == sessionEntity) // sessione cancellata, logout effettuato
                    return false;
                return sessionEntity.ValidUntil.CompareTo(AlarmClock.Now) > 0; // Ritorna maggiore di zero se la scadenza è dopo l'ora attuale
            }
        }

        // to end the session if it is still present
        public void Logout()
        {
            if (Cs == null)
                throw new InvalidOperationException("State of entity out of context, no data available");
            using (var Db = new AuctionSiteContext(Cs))
            {
                SiteFactory.ChecksOnDbConnection(Db);
                var sessionEntity = Db.Sessions.SingleOrDefault(s => s.Id == Id);
                if (null == sessionEntity) // sessione cancellata, logout effettuato
                    throw new InvalidOperationException(nameof(SessionEntity) + " not consistent");

                Db.Sessions.Remove(sessionEntity);
                Db.SaveChanges();
            }
        }

        // after the constraints it creates the auction, renews the the session, returns the auction
        public IAuction CreateAuction(string description, DateTime endsOn, double startingPrice)
        {
            SiteFactory.ChecksOnCsAndClock(Cs, AlarmClock);
            using (var Db = new AuctionSiteContext(Cs))
            {
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

                var siteEntity = Db.Sites.SingleOrDefault(site => site.Name == SiteName);
                if (siteEntity == null)
                    throw new InvalidOperationException("the site does not exist anymore");
                var sellerEntity = Db.Users.SingleOrDefault(s => s.SiteName == siteEntity.Name && s.Username == Username);
                if (sellerEntity == null)
                    throw new InvalidOperationException("the user does not exist anymore");

                var time = siteEntity.SessionExpirationInSeconds;
                var auctionEntity =
                    new AuctionEntity(description, endsOn, startingPrice, Username, SiteName) { Seller = sellerEntity };
                Db.Auctions.Add(auctionEntity);
                ResetTime(time);
                Db.SaveChanges();

                var auction = new Auction(auctionEntity.Id, auctionEntity.Description, auctionEntity.EndsOn,
                        auctionEntity.SiteName)
                    { Cs = Cs, AlarmClock = AlarmClock };
                return auction;
            }
        }
    }
}
