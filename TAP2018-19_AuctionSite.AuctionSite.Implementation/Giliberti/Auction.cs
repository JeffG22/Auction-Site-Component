using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using TAP2018_19.AlarmClock.Interfaces;
using TAP2018_19.AuctionSite.Interfaces;

namespace Giliberti
{
    public partial class Auction
    {
        [NotMapped] internal IAlarmClock AlarmClock { get; set; }
        [NotMapped] internal AuctionSiteContext Db { get; set; }

        public Auction()
        {
            Db = null;
            AlarmClock = null;
        }

        public Auction(string description, DateTime endsOn, double startingPrice, string username, string siteName)
        {
            Description = description;
            EndsOn = endsOn;
            SellerUsername = username;
            SiteName = siteName;
            FirstBid = true;
            CurrentPrice = startingPrice;
            HighestPrice = startingPrice;
            WinnerUsername = null;
        }

        internal bool IsEnded()
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            return EndsOn.CompareTo(AlarmClock.Now) < 0; // se minore di zero allora la fine dell'asta è antecedente l'ora attuale
        }

        private void ChecksOnSession(Session session)
        {
            try
            {
                if (!session.IsValid())
                    throw new ArgumentException("The session is not valid");
            }
            catch (InvalidOperationException e)
            {
                throw new ArgumentException("The session is not valid", e);
            }

            // user's validity
            if (session.SiteName != SiteName || // user from a different site
                session.SiteName == SiteName && session.Username == SellerUsername) // logged user is the seller
                throw new ArgumentException("user not valid");
        }

        private bool BidIsNotAccepted(string username, double offer, double minimum)
        {
            return username == WinnerUsername &&
                   offer < HighestPrice + minimum || // sono il vincitore corrente, offerta troppo bassa
                   username != WinnerUsername && offer < CurrentPrice && FirstBid || // prima offerta, troppo bassa
                   username != WinnerUsername && offer < CurrentPrice + minimum && !FirstBid; // non è la prima, troppo bassa
        }

        public bool BidOnAuction(ISession session, double offer)
        {
            // constraints
            if (null == session)
                throw new ArgumentNullException(nameof(session), "is null");
            if (offer < 0)
                throw new ArgumentOutOfRangeException(nameof(offer), "offer is negative");

            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (IsEnded())
                throw new InvalidOperationException("the auction is already closed");
            if (!Db.Auctions.Any(a => a.Id == Id && a.SiteName == SiteName))
                throw new InvalidOperationException("the auction does not exist anymore");
            if (!(session is Session))
                throw new InvalidOperationException("the session is out of the context");

            var s = (Session) session;
            s.Db = Db;
            s.AlarmClock = AlarmClock;
            ChecksOnSession(s);

            // the bid is valid
            Site siteAuction = null;
            siteAuction = Db.Sites.SingleOrDefault(site => site.Name == s.SiteName);
            if (siteAuction == null)
                throw new InvalidOperationException("the site does not exist anymore");
            var minimum = siteAuction.MinimumBidIncrement;
            var time = siteAuction.SessionExpirationInSeconds;

            s.ResetTime(time);
            Db.SaveChanges();

            if (BidIsNotAccepted(s.Username, offer, minimum))
                return false;

            if (!FirstBid && WinnerUsername != s.Username && offer <= HighestPrice)
                CurrentPrice = offer + minimum < HighestPrice ? offer + minimum : HighestPrice;
            else
            {
                if (!FirstBid && WinnerUsername != s.Username)
                    CurrentPrice = offer < HighestPrice + minimum ? offer : HighestPrice + minimum;

                HighestPrice = offer;
                SiteNameWinner = s.SiteName;
                WinnerUsername = s.Username;
                try
                {
                    Winner = Db.Users.Single(u => u.Username == WinnerUsername && u.SiteName == SiteName);
                }
                catch (ArgumentNullException e)
                {
                    throw new InvalidOperationException("the user does not exist anymore", e);
                }
            }
            FirstBid = false;
            Db.SaveChanges();
            return true;
        }

        double IAuction.CurrentPrice()
        {
            return CurrentPrice;
        }

        public IUser CurrentWinner()
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (!Db.Auctions.Any(a => a.Id == Id && a.SiteName == SiteName))
                throw new InvalidOperationException("the auction does not exist anymore");

            if (null == Winner)
                return null;
            if (!Db.Users.Any(u => u.Username == WinnerUsername && u.SiteName == SiteName)) // winner has been deleted
                return null;
            return Winner;
        }

        public void Delete()
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (!Db.Auctions.Any(a => a.Id == Id && a.SiteName == SiteName))
                throw new InvalidOperationException("the auction does not exist anymore");

            Db.Auctions.Remove(this);
            Db.SaveChanges();
            Db = null;
            AlarmClock = null;
        }

    }
}
