using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Data.Entity.Core.Objects.DataClasses;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
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

        public Session(string id, DateTime validUntil, string username, string siteName )
        {
            Id = id;
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
        private void ChecksOnContextAndClock()
        {
            if (Db == null || AlarmClock == null)
                throw new UnavailableDbException("State of entity out of context, no data available");
        }

        private void ChecksOnDbConnection()
        {
            try
            {
                Db.Database.Connection.Open();
                Db.Database.Connection.Close();
            }
            catch (DbException e)
            {
                throw new UnavailableDbException("Invalid context, it was not possible to connect to the DB", e);
            }
        }
        // member functions
        public bool IsValid()
        {
            // controllo che sia correttamente creata e presente sul DB
            ChecksOnContextAndClock();
            ChecksOnDbConnection();

            if (!Db.Sessions.Any(s => s.Id == this.Id))
                throw new InvalidOperationException(nameof(Session)+" not consistent");

            return ValidUntil.CompareTo(AlarmClock.Now) < 0; // istanza precedente al parametro
        }

        public void Logout()
        {
            if (Db == null)
                throw new UnavailableDbException("State of entity out of context, no data available");
            ChecksOnDbConnection();

            if (!Db.Sessions.Any(s => s.Id == this.Id))
                throw new InvalidOperationException(nameof(Session) + " not consistent");

            ValidUntil = DateTime.Now.ToUniversalTime().AddHours(-24); // Expired
            Db.Sessions.Remove(this);
            Db.SaveChanges();
            Db = null;
            AlarmClock = null;
        }

        public IAuction CreateAuction(string description, DateTime endsOn, double startingPrice)
        {
            ChecksOnContextAndClock();
            ChecksOnDbConnection();

            if (description == null)
                throw new ArgumentNullException(nameof(description), "is null");
            if (string.IsNullOrEmpty(description))
                throw new ArgumentException("is empty", nameof(description));
            if (startingPrice < 0)
                throw new ArgumentOutOfRangeException(nameof(startingPrice), "is negative");
            if (endsOn.CompareTo(AlarmClock.Now) <= 0)
                throw new UnavailableTimeMachineException("endsOn precedes the current ISite's time");
            if (!Db.Sessions.Any(s => s.Id == this.Id))
                throw new InvalidOperationException(nameof(Session) + " not consistent");

            var auction = new Auction(description, endsOn, startingPrice, this.Username, this.SiteName);
            Db.Auctions.Add(auction);
            this.ResetTime(this.Site.SessionExpirationInSeconds);
            Db.SaveChanges();

            auction.Db = Db;
            auction.AlarmClock = AlarmClock;
            return auction;
        }
    }
}
