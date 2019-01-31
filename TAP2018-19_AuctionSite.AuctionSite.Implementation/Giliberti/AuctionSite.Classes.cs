using System;
using System.Linq;
using TAP2018_19.AlarmClock.Interfaces;
using TAP2018_19.AuctionSite.Interfaces;

namespace Giliberti
{
    /**
     * File with the declarations of the logical classes to represent the permanent object on the db
     */

    public partial class Site : ISite
    {
        
        public string Name { get; }
        public int Timezone { get; }
        public double MinimumBidIncrement { get; }
        public int SessionExpirationInSeconds { get; }

        internal IAlarmClock AlarmClock { get; set; }
        internal string Cs { set; get; }

        // constructors
        public Site(string name, int timezone, int sessionExpirationTimeInSeconds, double minimumBidIncrement)
        {
            Name = name;
            Timezone = timezone;
            SessionExpirationInSeconds = sessionExpirationTimeInSeconds;
            MinimumBidIncrement = minimumBidIncrement;
            AlarmClock = null;
            Cs = null;
        }

        // ovverride di Equals, GetHashCode, == e !=
        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            if (obj.GetType() != typeof(Site)) return false;
            var other = (Site) obj;
            return Name == other.Name;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public static bool operator ==(Site lhs, Site rhs)
        {
            // Check for null.
            // Only the left side is null.
            // Equals handles the case of null on right side.
            return lhs?.Equals(rhs) ?? ReferenceEquals(rhs, null);
        }

        public static bool operator !=(Site lhs, Site rhs)
        {
            return !(lhs == rhs);
        }
    }

    public partial class User : IUser
    {
        public string Username { get; }
        public string SiteName { get; } // attributo aggiuntivo per equals
        internal IAlarmClock AlarmClock { get; set; }
        internal string Cs { get; set; }

        public User(string username, string siteName)
        {
            Username = username;
            SiteName = siteName;
            AlarmClock = null;
            Cs = null;
        }

        // ovverride di Equals, GetHashCode, == e !=
        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            if (obj.GetType() != typeof(User)) return false;
            var other = (User)obj;
            return Username == other.Username && SiteName == other.SiteName;
        }

        public override int GetHashCode()
        {
            return (Username+SiteName).GetHashCode();
        }

        public static bool operator ==(User lhs, User rhs)
        {
            // Check for null.
            // Only the left side is null.
            // Equals handles the case of null on right side.
            return lhs?.Equals(rhs) ?? ReferenceEquals(rhs, null);
        }

        public static bool operator !=(User lhs, User rhs)
        {
            return !(lhs == rhs);
        }
    }

    public partial class Session : ISession {

        public string Id { get; }
        public DateTime ValidUntil { get; set; }
        IUser ISession.User
        {
            get
            {
                if (Cs == null)
                    throw new UnavailableDbException("State of entity out of context, no data available");
                using (var Db = new AuctionSiteContext(Cs))
                {
                    SiteFactory.ChecksOnDbConnection(Db);
                    var thisEntity = Db.Sessions.FirstOrDefault(s => s.Id == Id);
                    if (null == thisEntity)
                        throw new InvalidOperationException(nameof(SessionEntity) + " not consistent");

                    return new User(thisEntity.Username, thisEntity.SiteName) { Cs = Cs, AlarmClock = AlarmClock };
                }
            }
        }
        public string SiteName { get; } // aggiuntivo per evitare query ulteriori
        public string Username { get; } // aggiuntivo per evitare query ulteriori

        internal IAlarmClock AlarmClock { get; set; }
        internal string Cs { get; set; }

        public Session(DateTime validUntil, string username, string siteName)
        {
            Id = siteName + username;
            SiteName = siteName;
            Username = username;
            ValidUntil = validUntil;
            AlarmClock = null;
            Cs = null;
        }

        // ovverride di Equals, GetHashCode, == e !=
        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            if (obj.GetType() != typeof(Session)) return false;
            var other = (Session)obj;
            return Id == other.Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(Session lhs, Session rhs)
        {
            // Check for null.
            // Only the left side is null.
            // Equals handles the case of null on right side.
            return lhs?.Equals(rhs) ?? ReferenceEquals(rhs, null);
        }

        public static bool operator !=(Session lhs, Session rhs)
        {
            return !(lhs == rhs);
        }
    }

    public partial class Auction : IAuction
    {
        public int Id { get; }
        public string Description { get; }
        public DateTime EndsOn { get; }
        public string SiteName { get; } // attributo aggiuntivo per equals
        IUser IAuction.Seller
        {
            get
            {
                if (Cs == null)
                    throw new UnavailableDbException("State of entity out of context, no data available");
                using (var Db = new AuctionSiteContext(Cs))
                {
                    SiteFactory.ChecksOnDbConnection(Db);
                    var thisEntity = Db.Auctions.FirstOrDefault(a => a.Id == Id && a.SiteName == SiteName);
                    if (null == thisEntity)
                        throw new InvalidOperationException(nameof(AuctionEntity) + " does not exist anymore");
                    return new User(thisEntity.Seller.Username, thisEntity.Seller.SiteName) { Cs = Cs, AlarmClock = AlarmClock };
                }
            }
        }

        internal IAlarmClock AlarmClock { get; set; }
        internal string Cs { get; set; }

        public Auction(int id, string description, DateTime endsOn, string siteName)
        {
            Id = id;
            Description = description;
            EndsOn = endsOn;
            SiteName = siteName;
            Cs = null;
            AlarmClock = null;
        }

        // ovverride di Equals, GetHashCode, == e !=
        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            if (obj.GetType() != typeof(Auction)) return false;
            var other = (Auction)obj;
            return Id == other.Id && SiteName == other.SiteName;
        }

        public override int GetHashCode()
        {
            return (Id+SiteName).GetHashCode();
        }

        public static bool operator ==(Auction lhs, Auction rhs)
        {
            // Check for null.
            // Only the left side is null.
            // Equals handles the case of null on right side.
            return lhs?.Equals(rhs) ?? ReferenceEquals(rhs, null);
        }

        public static bool operator !=(Auction lhs, Auction rhs)
        {
            return !(lhs == rhs);
        }

    }
}
