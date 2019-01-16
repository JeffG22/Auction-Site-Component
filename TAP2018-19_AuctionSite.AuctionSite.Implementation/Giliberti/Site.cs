using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
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

        public Site()
        {
            this.AlarmClock = null;
            this.Db = null;
        }
        public Site(string name, int timezone, int sessionExpirationTimeInSeconds, double minimumBidIncrement)
        {
            this.Name = name;
            this.Timezone = timezone;
            this.SessionExpirationInSeconds = sessionExpirationTimeInSeconds;
            this.MinimumBidIncrement = minimumBidIncrement;
            this.AlarmClock = null;
            this.Db = null;
        }

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

        public void CreateUser(string username, string password)
        {
            ChecksOnUsernameAndPassword(username, password);

            if(Db == null)
                throw new UnavailableDbException("It was not possible to reach the Db");
            SiteFactory.ChecksOnDbConnection(Db);
            if (Db.Sites.Any(s => s.Name == this.Name))
            {
                throw new InvalidOperationException("the site is deleted");
            }

            if (Db.Users.Any(u => (u.Username == username && u.SiteName == this.Name)))
                throw new NameAlreadyInUseException(nameof(username), " of user already in use");

            Db.Users.Add(new User(username, password, this.Name));
            Db.SaveChanges();
        }

        public ISession Login(string username, string password)
        {
            ChecksOnUsernameAndPassword(username, password);
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (Db.Sites.Any(s => s.Name == this.Name))
            {
                throw new InvalidOperationException("the site is deleted");
            }

            if (!Db.Users.Any(u => (u.Username == username && u.Password == password && u.SiteName == this.Name))) // l'utente non esiste
                return null;

            var session = Db.Sessions.Where(s => s.Id == Name+username).Select(s => s).SingleOrDefault(); // sessione

            if (session == null ) // l'utente esiste e non ha una sessione attiva
            {
                // new session
                session = new Session(Name + username, AlarmClock.Now.AddSeconds(SessionExpirationInSeconds), username, Name);
                Db.Sessions.Add(session);
                session.AlarmClock = AlarmClock;
            }
            else
            {
                session.AlarmClock = AlarmClock;
                if (session.IsValid()) // l'utente esiste ed ha una sessione attiva valida
                {
                    session.ResetTime(SessionExpirationInSeconds);
                    Db.SaveChanges();
                }
                else // 2 - riattivo quella che non era ancora cancellata, possibile concorrenza con cancellazione o altro
                {
                    session.ResetTime(SessionExpirationInSeconds);
                    // TODO alla fine verificare se if e else sono uguali
                }
            }
            Db.SaveChanges();
            session.Db = Db; // dispose entrusted to it?
            return session;
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

        public void CleanupSessions()
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock); // TODO se non ce l'ho devo gestirmela avendo il timezone
            SiteFactory.ChecksOnDbConnection(Db);
            if (Db.Sites.Any(s => s.Name == this.Name))
            {
                throw new InvalidOperationException("the site is deleted");
            }

            var sessions = Db.Sessions.Where(s => s.SiteName == this.Name).Select(s => s);
            foreach (var s in sessions)
            {
                s.AlarmClock = AlarmClock;
                s.Db = Db;
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
            if (Db.Sites.Any(s => s.Name == this.Name))
            {
                throw new InvalidOperationException("the site is deleted");
            }

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
            if (Db.Sites.Any(s => s.Name == this.Name))
            {
                throw new InvalidOperationException("the site is deleted");
            }

            var sessions = Db.Sessions.Where(s => s.SiteName == this.Name).Select(s => s).ToList();
            foreach (var s in sessions)
            {
                s.Db = Db;
                s.AlarmClock = AlarmClock;
                // yield return s;
            }

            return sessions;
        }

        public IEnumerable<IUser> GetUsers()
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (Db.Sites.Any(s => s.Name == this.Name))
            {
                throw new InvalidOperationException("the site is deleted");
            }

            var users = this.Users.Select(s => s).ToList();
            foreach (var u in users)
            {
                u.Db = Db;
                u.AlarmClock = AlarmClock;
                //yield return u;
            }

            return users;
        }

        public IEnumerable<IAuction> GetAuctions(bool onlyNotEnded)
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (Db.Sites.Any(s => s.Name == this.Name))
            {
                throw new InvalidOperationException("the site is deleted");
            }

            var auctions = Db.Auctions.Where(a => a.SiteName == this.Name).Select(a => a).ToList();
                
            var auctionsList = new List<Auction>();
            foreach (var a in auctions)
            {
                a.Db = Db;
                a.AlarmClock = AlarmClock;
                if (!onlyNotEnded || a.IsEnded())
                    auctionsList.Add(a);
            }

            return auctionsList;
        }

        public void Delete()
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (Db.Sites.Any(s => s.Name == this.Name))
            {
                throw new InvalidOperationException("the site is already deleted");
            }

            var sessions = Db.Sessions.Where(s => s.SiteName == this.Name).Select(s => s);
            // disposes the sessions' site and auctions' site
            foreach (var s in sessions)
            {
                s.Db = Db;
                s.AlarmClock = AlarmClock;
                s.Logout();
            }
            var auctions = Db.Auctions.Where(a => a.SiteName == this.Name).Select(a => a);
            foreach (var a in auctions)
            {
                a.Db = Db;
                a.AlarmClock = AlarmClock;
                a.Delete();
            }

            // disposes the users' site
            foreach (var u in this.Users)
            {
                u.Db = Db;
                u.AlarmClock = AlarmClock;
                u.Delete();
            }

            Db.Sites.Remove(this);
            Db.SaveChanges();
            Db = null;
            AlarmClock = null;
        }
    }
}
