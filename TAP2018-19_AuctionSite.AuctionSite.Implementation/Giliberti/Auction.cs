using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using TAP2018_19.AlarmClock.Interfaces;
using TAP2018_19.AuctionSite.Interfaces;

namespace Giliberti
{
    public partial class Auction
    {
        internal bool IsEnded()
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            return EndsOn.CompareTo(AlarmClock.Now) < 0; // se minore di zero allora la fine dell'asta è antecedente l'ora attuale
        }

        private void ChecksOnSession(Session session, string sellerUsername)
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
                session.SiteName == SiteName && session.Username == sellerUsername) // logged user is the seller
                throw new ArgumentException("user not valid");
        }

        private bool BidIsNotAccepted(string username, string winnerUsername, bool firstBid, double offer, double minimum, double currentPrice, double highestPrice)
        {
            return username == winnerUsername &&
                   offer < highestPrice + minimum || // sono il vincitore corrente, offerta troppo bassa
                   username != winnerUsername && offer < currentPrice && firstBid || // prima offerta, troppo bassa
                   username != winnerUsername && offer < currentPrice + minimum && !firstBid; // non è la prima, troppo bassa
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

            if (!(session is Session))
                throw new InvalidOperationException("the session is out of the context");
            var s = (Session) session;
            s.Db = Db;
            s.AlarmClock = AlarmClock;

            var sessionEntity = Db.Sessions.SingleOrDefault(sE => sE.Id == s.Id);
            if (null == sessionEntity)
                throw new ArgumentException("the session is not valid: it does not exist anymore");

            var siteEntity = Db.Sites.SingleOrDefault(site => site.Name == sessionEntity.SiteName);
            if (siteEntity == null)
                throw new InvalidOperationException("the site does not exist anymore");

            if (!Db.Users.Any(u => u.Username == sessionEntity.Username && u.SiteName == sessionEntity.SiteName))
                throw new InvalidOperationException("the user does not exist anymore");

            var auctionEntity = Db.Auctions.SingleOrDefault(a => a.Id == Id && a.SiteName == SiteName);
            if (null == auctionEntity)
                throw new InvalidOperationException("the auction does not exist anymore");
            if (IsEnded())
                throw new InvalidOperationException("the auction is already closed");
            
            ChecksOnSession(s, auctionEntity.SiteName);

            // the bid is valid
            var minimum = siteEntity.MinimumBidIncrement;
            var time = siteEntity.SessionExpirationInSeconds;
            var currentUsername = sessionEntity.Username;
            var winning = auctionEntity.WinnerUsername;
            var firstBid = auctionEntity.FirstBid;
            var currentPrice = auctionEntity.CurrentPrice;
            var highestPrice = auctionEntity.HighestPrice;


            s.ResetTime(time);
            Db.SaveChanges();

            if (BidIsNotAccepted(currentUsername, winning, firstBid, offer, minimum, currentPrice, highestPrice))
                return false;

            if (!firstBid && winning != currentUsername && offer <= highestPrice)
            {
                currentPrice = offer + minimum < highestPrice ? offer + minimum : highestPrice;
                auctionEntity.CurrentPrice = currentPrice;
            }
            else
            {
                if (!firstBid && winning != currentUsername)
                {
                    currentPrice = offer < highestPrice + minimum ? offer : highestPrice + minimum;
                    auctionEntity.CurrentPrice = currentPrice;
                }

                auctionEntity.HighestPrice = offer;
                auctionEntity.WinnerUsername = currentUsername;
;
            }
            auctionEntity.FirstBid = false;
            Db.SaveChanges();
            return true;
        }

        double IAuction.CurrentPrice()
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            var auctionEntity = Db.Auctions.SingleOrDefault(a => a.Id == Id && a.SiteName == SiteName);
            if (auctionEntity == null)
                throw new InvalidOperationException("the auction does not exist anymore");
            return auctionEntity.CurrentPrice;
        }

        public IUser CurrentWinner()
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            var auctionEntity = Db.Auctions.SingleOrDefault(a => a.Id == Id && a.SiteName == SiteName);
            if (auctionEntity == null)
                throw new InvalidOperationException("the auction does not exist anymore");

            if (null == auctionEntity.WinnerUsername)
                return null;

            var userEntity = Db.Users.SingleOrDefault(u => u.Username == auctionEntity.WinnerUsername && u.SiteName == SiteName);
            return null == userEntity ? null : new User(userEntity.Username, userEntity.SiteName) { Db = Db, AlarmClock = AlarmClock};
        }

        public void Delete()
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            var auctionEntity = Db.Auctions.SingleOrDefault(a => a.Id == Id && a.SiteName == SiteName);
            if (auctionEntity == null)
                throw new InvalidOperationException("the auction does not exist anymore");

            Db.Auctions.Remove(auctionEntity);
            Db.SaveChanges();
        }

    }
}
