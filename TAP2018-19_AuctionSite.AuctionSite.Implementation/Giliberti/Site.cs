using System;
using System.Collections.Generic;
using System.Linq;
using TAP2018_19.AuctionSite.Interfaces;

namespace Giliberti
{
    /// <summary>
    /// Site is a logic class to represent the sites entities in the db
    /// This partial class implements its methods according to the interface ISite
    /// managing user, their sessions and auctions
    /// root of an aggregate (user, session, auction no meaning outside)
    /// </summary>
    public partial class Site
    {

        // constraints
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

        // if all the constraints are respected it will create the user, otherwise it throws exception
        public void CreateUser(string username, string password)
        {
            ChecksOnUsernameAndPassword(username, password);

            if(Db == null)
                throw new InvalidOperationException("It was not possible to reach the Db");
            SiteFactory.ChecksOnDbConnection(Db);
            var siteEntity = Db.Sites.FirstOrDefault(s => s.Name == Name);
            if (null == siteEntity)
                throw new InvalidOperationException("the site is deleted");
            if (Db.Users.Any(u => u.Username == username && u.SiteName == Name))
                throw new NameAlreadyInUseException(nameof(username), " of user already in use");

            Db.Users.Add(new UserEntity(username, password, Name){Site = siteEntity});
            Db.SaveChanges();
        }

        // it allows to sign in if possible. If a session already existing it will be renewed in each cases otherwise a new one is created.
        public ISession Login(string username, string password)
        {
            ChecksOnUsernameAndPassword(username, password);
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (!Db.Sites.Any(s => s.Name == Name))
                throw new InvalidOperationException("the site is deleted");
            var userEntity = Db.Users.SingleOrDefault(u => u.Username == username && u.Password == password && u.SiteName == Name);
            if (null == userEntity) // l'utente non esiste o dati sbagliati
                return null;

            var sessionEntity = Db.Sessions.SingleOrDefault(s => s.Id == Name + username);
            var session = new Session(AlarmClock.Now.AddSeconds(SessionExpirationInSeconds), username, Name)
            {
                AlarmClock = AlarmClock,
                Db = Db
            };

            if (sessionEntity == null) // l'utente esiste e non ha una sessione attiva
            {
                // new session
                sessionEntity = new SessionEntity(AlarmClock.Now.AddSeconds(SessionExpirationInSeconds), username, Name)
                {
                    User = userEntity
                };
                Db.Sessions.Add(sessionEntity);
            }
            else 
                session.ResetTime(SessionExpirationInSeconds); // l'utente esiste e ha una sessione valida oppure no, si rinnova comunque
            Db.SaveChanges();
            return session;
        }

        // if the site is still existing it will delete all the sessions
        public void CleanupSessions()
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            var siteEntity = Db.Sites.SingleOrDefault(s => s.Name == Name);
            if (null == siteEntity)
                throw new InvalidOperationException("the site is deleted");

            var sessionsEntities = Db.Sessions.Where(s => s.SiteName == Name).Select(s => s).ToList();
            foreach (var s in sessionsEntities)
            {
                var tmpSession = new Session(s.ValidUntil, s.Username, s.SiteName) {Db = Db, AlarmClock = AlarmClock};
                if (!tmpSession.IsValid())
                    tmpSession.Logout();
            }
            Db.SaveChanges();
        }

        // it returns the session if it is still valid
        public ISession GetSession(string sessionId)
        {
            if (sessionId == null)
                throw new ArgumentNullException(nameof(sessionId), " is null");

            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (!Db.Sites.Any(s => s.Name == Name))
                throw new InvalidOperationException("the site is deleted");

            var sessionEntity = Db.Sessions.SingleOrDefault(s => s.Id == sessionId);
            if (sessionEntity == null)
                return null;
            var session = new Session(sessionEntity.ValidUntil, sessionEntity.Username, sessionEntity.SiteName)
            {
                AlarmClock = AlarmClock, Db = Db
            };
            return session.IsValid() ? session : null;
        }

        // it returns all the sessions if the site is not deleted
        public IEnumerable<ISession> GetSessions()
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (!Db.Sites.Any(s => s.Name == Name))
                throw new InvalidOperationException("the site is deleted");

            var sessionsEntities = Db.Sessions.Where(s => s.SiteName == Name).Select(s => s).ToList();
            var sessions = new List<Session>();
            foreach (var s in sessionsEntities)
                sessions.Add(new Session(s.ValidUntil, s.Username, s.SiteName){AlarmClock = AlarmClock, Db = Db});
            return sessions;
        }

        // it returns all the users if the site is not deleted
        public IEnumerable<IUser> GetUsers()
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (!Db.Sites.Any(s => s.Name == Name))
                throw new InvalidOperationException("the site is deleted");

            var usersEntities = Db.Users.Where(s => s.SiteName == Name).ToList();
            var users = new List<User>();
            foreach (var u in usersEntities)
                users.Add(new User(u.Username, u.SiteName) {AlarmClock = AlarmClock, Db = Db});

            return users;
        }

        // return the auctions if the site is still present
        public IEnumerable<IAuction> GetAuctions(bool onlyNotEnded)
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            if (!Db.Sites.Any(s => s.Name == Name))
                throw new InvalidOperationException("the site is deleted");

            var auctionsEntities = Db.Auctions.Where(a => a.SiteName == Name).Select(a => a).ToList();
                
            var auctionsList = new List<Auction>();
            foreach (var a in auctionsEntities)
            {
                var auction = new Auction(a.Id, a.Description, a.EndsOn, a.SiteName) {Db = Db, AlarmClock = AlarmClock};
                if (!onlyNotEnded || !auction.IsEnded())
                    auctionsList.Add(auction);
            }

            return auctionsList;
        }

        // delete every corresponded objects since it is the root of an aggregate
        public void Delete()
        {
            SiteFactory.ChecksOnContextAndClock(Db, AlarmClock);
            SiteFactory.ChecksOnDbConnection(Db);
            var siteEntity = Db.Sites.SingleOrDefault(s => s.Name == Name);
            if (null == siteEntity)
                throw new InvalidOperationException("the site is already deleted");

            var sessionsEntities = Db.Sessions.Where(s => s.SiteName == Name).Select(s => s).ToList();
            // disposes the sessions' site and auctions' site
            foreach (var sEntity in sessionsEntities)
            {
                var s = new Session(sEntity.ValidUntil, sEntity.Username, sEntity.SiteName)
                {
                    Db = Db, AlarmClock = AlarmClock
                };
                s.Logout();
            }
            var auctionsEntities = Db.Auctions.Where(a => a.SiteName == Name).Select(a => a).ToList();
            foreach (var aEntity in auctionsEntities)
            {
                var a = new Auction(aEntity.Id, aEntity.Description, aEntity.EndsOn, aEntity.SiteName)
                {
                    Db = Db, AlarmClock = AlarmClock
                };
                a.Delete();
            }

            // disposes the users' site
            var usersEntities = Db.Users.Where(u => u.SiteName == Name).Select(u => u).ToList();
            foreach (var uEntity in usersEntities)
            {
                var u = new User(uEntity.Username, uEntity.SiteName) {Db = Db, AlarmClock = AlarmClock};
                u.Delete();
            }

            Db.Sites.Remove(siteEntity);
            Db.SaveChanges();
        }
    }
}
