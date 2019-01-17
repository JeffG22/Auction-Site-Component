using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using TAP2018_19.AlarmClock.Interfaces;
using TAP2018_19.AuctionSite.Interfaces;

namespace Giliberti
{
    /// <summary>
    /// To act on a Site it must have a valid session
    /// Only one at the same time
    /// </summary>
    public partial class User
    {
        [NotMapped] internal IAlarmClock AlarmClock { get; set; }
        [NotMapped] internal AuctionSiteContext Db { get; set; }

        public User()
        {
            Db = null;
            AlarmClock = null;
        }

        public User(string username, string password, string name)
        {
            Username = username;
            Password = password;
            SiteName = name;
            AlarmClock = null;
            Db = null;
        }
        internal static bool NotValidUsername(string username)
        {
            return username.Length < DomainConstraints.MinUserName || username.Length > DomainConstraints.MaxUserName || string.IsNullOrWhiteSpace(username);
        }
        internal static bool NotValidPassword(string password)
        {
            return password.Length < DomainConstraints.MinUserPassword;
        }

        public IEnumerable<IAuction> WonAuctions()
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (!Db.Users.Any(u => u.Username == Username && u.SiteName == SiteName))
                throw new InvalidOperationException("the user does not exist anymore");

            var auctions = Db.Auctions.Where(a => a.SiteName == SiteName && a.WinnerUsername == Username)
                .Select(a => a).ToList();
            var auctionList = new List<Auction>();

            foreach (var a in auctions)
            {
                a.Db = Db;
                a.AlarmClock = AlarmClock;
                if (a.IsEnded())
                    auctionList.Add(a);
            }

            return auctionList;
        }

        public void Delete()
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (!Db.Users.Any(u => u.Username == Username && u.SiteName == SiteName))
                throw new InvalidOperationException("the user does not exist anymore");

            // owner of auctions not ended yet, winner of auctions not ended yet -> IOE exception
            var anySellerAuctions = Db.Auctions.Where(a => a.SellerUsername == Username && a.SiteName == SiteName);
            var anyWinnerAuctions = Db.Auctions.Where(a => a.WinnerUsername == Username && a.SiteName == SiteName);
            var deletable = true;
            foreach (var a in anySellerAuctions)
            {
                a.Db = Db;
                a.AlarmClock = AlarmClock;
                if (a.IsEnded()) continue;
                deletable = false;
                break;
            }
            if (!deletable)
                throw new InvalidOperationException("the user's auction(s) is not ended yet");
            foreach (var a in anyWinnerAuctions)
            {
                a.Db = Db;
                a.AlarmClock = AlarmClock;
                if (a.IsEnded()) continue;
                deletable = true;
                break;
            }
            if (!deletable)
                throw new InvalidOperationException("the user is winning an auction not ended yet");

            // ended owned auctions are disposed, ended won auctions are updated
            foreach (var a in anySellerAuctions)
                a.Delete();

            foreach (var a in anySellerAuctions)
            {
                a.WinnerUsername = null;
                a.Winner = null;
            }

            var sessions = Db.Sessions.Where(s => s.SiteName == SiteName && s.Username == Username).Select(s => s);
            foreach (var s in sessions)
                Db.Sessions.Remove(s);

            // the user is removed and the Db and AlarmClock must be null
            Db.Users.Remove(this);
            Db.SaveChanges();
            Db = null;
            AlarmClock = null;

        }
    }
}
