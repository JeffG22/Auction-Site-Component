using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using TAP2018_19.AlarmClock.Interfaces;
using TAP2018_19.AuctionSite;
using TAP2018_19.AuctionSite.Interfaces;
using ArgumentOutOfRangeException = System.ArgumentOutOfRangeException;

namespace Giliberti
{
    public partial class Auction
    {
        [NotMapped] internal IAlarmClock AlarmClock { get; set; }
        [NotMapped] internal AuctionSiteContext Db { get; set; }

        public Auction(string description, DateTime endsOn, double startingPrice, string username, string siteName)
        {
            this.Description = description;
            this.EndsOn = endsOn;
            this.SellerUsername = username;
            this.SiteName = siteName;
            this.FirstBid = false;
            this.CurrentPrice = startingPrice;
            this.HighestPrice = startingPrice;
            this.WinnerUsername = null;
        }

        internal bool IsEnded()
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            return EndsOn.CompareTo(AlarmClock.Now) < 0;
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
            if (((Session)session).SiteName != this.SiteName || // user from a different site
                ((Session)session).SiteName == this.SiteName && ((Session)session).Username == this.SellerUsername) // logged user is the seller
                throw new ArgumentException("user not valid");
        }

        private bool BidIsNotAccepted(string username, double offer, double minimum)
        {
            return username == this.WinnerUsername && offer < HighestPrice + minimum ||
                   username != this.WinnerUsername && offer < CurrentPrice && CurrentPrice.Equals(HighestPrice) ||
                   username != this.WinnerUsername && offer < CurrentPrice + minimum &&
                   !CurrentPrice.Equals(HighestPrice);
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
            if (!Db.Auctions.Any(a => a.Id == this.Id && a.SiteName == this.SiteName))
                throw new InvalidOperationException("the auction does not exist anymore");

            if (!(session is Session)) // TODO verificare se serve anche altrove
                throw new InvalidOperationException("the session is out of the context");
            var s = (Session) session;
            s.Db = Db;
            s.AlarmClock = AlarmClock;
            ChecksOnSession(s);
            
            // the bid is valid
            s.ResetTime(this.Site.SessionExpirationInSeconds);
            Db.SaveChanges();

            Site siteAuction = null;
            siteAuction = Db.Sites.SingleOrDefault(site => site.Name == s.SiteName);
            if (siteAuction == null)
                throw new InvalidOperationException("the site does not exist anymore");
            var minimum = siteAuction.MinimumBidIncrement;

            if (BidIsNotAccepted(s.Username, offer, minimum))
                return false;

            if (!this.FirstBid && this.WinnerUsername != s.Username && offer <= HighestPrice)
                this.CurrentPrice = offer + minimum < HighestPrice ? offer + minimum : HighestPrice;
            else
            {
                HighestPrice = offer;
                this.WinnerUsername = s.Username;
                this.Winner = Db.Users.Single(u => u.Username == WinnerUsername && u.SiteName == SiteName);
                if (!this.FirstBid)
                    this.CurrentPrice = (offer < HighestPrice + minimum) ? offer : HighestPrice + minimum;
            }
            this.FirstBid = false;
            Db.SaveChanges();
            return true;
        }

        double IAuction.CurrentPrice()
        {
            return this.CurrentPrice;
        }

        public IUser CurrentWinner()
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (!Db.Auctions.Any(a => a.Id == this.Id && a.SiteName == this.SiteName))
                throw new InvalidOperationException("the auction does not exist anymore");

            if (null == this.Winner)
                return null;
            if (!Db.Users.Any(u => u.Username == this.WinnerUsername && u.SiteName == this.SiteName)) // winner has been deleted
                return null;
            return Winner;
        }

        public void Delete()
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (!Db.Auctions.Any(a => a.Id == this.Id && a.SiteName == this.SiteName))
                throw new InvalidOperationException("the auction does not exist anymore");

            Db.Auctions.Remove(this);
            Db.SaveChanges();
            Db = null;
            AlarmClock = null;
        }

    }
}
