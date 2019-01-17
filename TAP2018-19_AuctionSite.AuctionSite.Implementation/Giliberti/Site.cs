using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using TAP2018_19.AlarmClock.Interfaces;
using TAP2018_19.AuctionSite.Interfaces;

namespace Giliberti
{
    /// <summary>
    /// managing user, THEIR sessions and auctions
    /// root of an aggregate (user, session, auction no meaning outside)
    /// </summary>
    public partial class Site
    {
        [NotMapped] public IAlarmClock AlarmClock { get; set; }
        [NotMapped] internal AuctionSiteContext Db { set; get; }

        // constructors
        public Site()
        {
            AlarmClock = null;
            Db = null;
        }
        public Site(string name, int timezone, int sessionExpirationTimeInSeconds, double minimumBidIncrement)
        {
            Name = name;
            Timezone = timezone;
            SessionExpirationInSeconds = sessionExpirationTimeInSeconds;
            MinimumBidIncrement = minimumBidIncrement;
            AlarmClock = null;
            Db = null;
        }

        // contraints
        internal static bool NotValidName(string name)
        {
            return name.Length < DomainConstraints.MinSiteName || name.Length > DomainConstraints.MaxSiteName || string.IsNullOrWhiteSpace(name);
        }
        internal static bool NotValidTimeZone(int timezone)
        {
            return timezone < DomainConstraints.MinTimeZone || timezone > DomainConstraints.MaxTimeZone;
        }
        internal static bool NotValidSessionExpirationTime(int time)
        {
            return time <= 0;
        }
        internal static bool NotValidMinimumBiddingIncr(double amount)
        {
            return amount <= 0;
        }
        private void ChecksOnUsernameAndPassword(string username, string password)
        {
            if (null == username)
                throw new ArgumentNullException(nameof(username), " is null");
            if (null == password)
                throw new ArgumentNullException(nameof(password), " is null");
            if (User.NotValidUsername(username))
                throw new ArgumentException("Its length is strictly smaller or larger than the constraint", nameof(username));
            if (User.NotValidPassword(password))
                throw new ArgumentException("Its length is strictly smaller or larger than the constraint", nameof(username));
        }

        public void CreateUser(string username, string password)
        {
            ChecksOnUsernameAndPassword(username, password);

            if(Db == null)
                throw new InvalidOperationException("It was not possible to reach the Db");
            SiteFactory.ChecksOnDbConnection(Db);
            if (!Db.Sites.Any(s => s.Name == Name))
                throw new InvalidOperationException("the site is deleted");
            if (Db.Users.Any(u => u.Username == username && u.SiteName == Name))
                throw new NameAlreadyInUseException(nameof(username), " of user already in use");

            Db.Users.Add(new User(username, password, Name){Site = this});
            Db.SaveChanges();
        }

        public ISession Login(string username, string password)
        {
            ChecksOnUsernameAndPassword(username, password);
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (!Db.Sites.Any(s => s.Name == Name))
                throw new InvalidOperationException("the site is deleted");
            if (!Db.Users.Any(u => (u.Username == username && u.Password == password && u.SiteName == Name))) // l'utente non esiste o dati sbagliati
                return null;

            var session = Db.Sessions.SingleOrDefault(s => s.Id == Name + username);

            if (session == null ) // l'utente esiste e non ha una sessione attiva
            {
                // new session
                var user = Db.Users.SingleOrDefault(us => us.Username == username && us.SiteName == Name);
                if (null == user) throw new InvalidOperationException("user not found");
                session = new Session(AlarmClock.Now.AddSeconds(SessionExpirationInSeconds), username, Name)
                {
                    User = user
                };
                Db.Sessions.Add(session);
                session.AlarmClock = AlarmClock;
                session.Db = Db;
            }
            else
            {
                session.Db = Db;
                session.AlarmClock = AlarmClock;
                // l'utente esiste e ha una sessione valida oppure no, si rinnova comunque
                session.ResetTime(SessionExpirationInSeconds);
            }
            Db.SaveChanges();
            return session;
        }

        public void CleanupSessions()
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (!Db.Sites.Any(s => s.Name == Name))
                throw new InvalidOperationException("the site is deleted");

            var sessions = Db.Sessions.Where(s => s.SiteName == Name).Select(s => s).ToList();
            foreach (var s in sessions)
            {
                s.Db = Db;
                s.AlarmClock = AlarmClock;
                if (!s.IsValid())
                    s.Logout();
            }
            Db.SaveChanges();
        }

        public ISession GetSession(string sessionId)
        {
            if (sessionId == null)
                throw new ArgumentNullException(nameof(sessionId), " is null");

            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (!Db.Sites.Any(s => s.Name == Name))
                throw new InvalidOperationException("the site is deleted");

            var session = Db.Sessions.SingleOrDefault(s => s.Id == sessionId);
            if (session == null)
                return null;

            session.AlarmClock = AlarmClock;
            session.Db = Db;
            return session.IsValid() ? session : null;
        }

        public IEnumerable<ISession> GetSessions()
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (!Db.Sites.Any(s => s.Name == Name))
                throw new InvalidOperationException("the site is deleted");

            var sessions = Db.Sessions.Where(s => s.SiteName == Name).Select(s => s).ToList();
            foreach (var s in sessions)
            {
                s.Db = Db;
                s.AlarmClock = AlarmClock;
                // yield return s; TODO vedere se cambia
            }

            return sessions;
        }

        public IEnumerable<IUser> GetUsers()
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (!Db.Sites.Any(s => s.Name == Name))
                throw new InvalidOperationException("the site is deleted");

            var users = Users.Select(s => s).ToList();
            foreach (var u in users)
            {
                u.Db = Db;
                u.AlarmClock = AlarmClock;
            }

            return users;
        }

        public IEnumerable<IAuction> GetAuctions(bool onlyNotEnded)
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (!Db.Sites.Any(s => s.Name == Name))
                throw new InvalidOperationException("the site is deleted");

            var auctions = Db.Auctions.Where(a => a.SiteName == Name).Select(a => a).ToList();
                
            var auctionsList = new List<Auction>();
            foreach (var a in auctions)
            {
                a.Db = Db;
                a.AlarmClock = AlarmClock;
                if (!onlyNotEnded || !a.IsEnded())
                    auctionsList.Add(a);
            }

            return auctionsList;
        }

        public void Delete()
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (!Db.Sites.Any(s => s.Name == Name))
                throw new InvalidOperationException("the site is already deleted");

            var sessions = Db.Sessions.Where(s => s.SiteName == Name).Select(s => s).ToList();
            // disposes the sessions' site and auctions' site
            foreach (var s in sessions)
            {
                s.Db = Db;
                s.AlarmClock = AlarmClock;
                s.Logout();
            }
            var auctions = Db.Auctions.Where(a => a.SiteName == Name).Select(a => a).ToList();
            foreach (var a in auctions)
            {
                a.Db = Db;
                a.AlarmClock = AlarmClock;
                a.Delete();
            }

            // disposes the users' site
            var users = Db.Users.Where(u => u.SiteName == Name).Select(u => u).ToList();
            foreach (var u in users)
            {
                u.Db = Db;
                u.AlarmClock = AlarmClock;
                u.Delete();
            }

            Db.Sites.Remove(this);
            Db.SaveChanges();
            AlarmClock = null;
            Db = null;
        }
    }
}
