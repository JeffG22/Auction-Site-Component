using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TAP2018_19.AlarmClock.Interfaces;
using TAP2018_19.AuctionSite.Interfaces;

namespace Giliberti
{
    // TODO assegnazione foreign key e correzione in session
    public partial class Site : ISite
    {
        [Key, MinLength(DomainConstraints.MinSiteName), MaxLength(DomainConstraints.MaxSiteName)]
        public string Name { get; } // Name: unique, not null, not empty - identification -> PK
        [Required, Range(DomainConstraints.MinTimeZone, DomainConstraints.MaxTimeZone)]
        public int Timezone { get; } // TimeZone: required - get the time
        [Required]
        public double MinimumBidIncrement { get; } // MinInc: required - minimum increment allowed
        [Required]
        public int SessionExpirationInSeconds { get; } // TimeOut: required - inactive session

        // navigation properties
        public virtual ICollection<Auction> Auctions { set; get; }
        public virtual ICollection<User> Users { set; get; }
        public virtual ICollection<Session> Sessions { set; get; }
    }

    public partial class User : IUser
    {
        [Key, Column(Order = 1), MinLength(DomainConstraints.MinUserName), MaxLength(DomainConstraints.MaxUserName)]
        public string Username { get; }
        [MinLength(DomainConstraints.MinUserPassword)] // no MaxLenght per hashing
        public string Password { set; get; } // TODO gestione hashing, set e get

        // foreign key
        [Key, Column(Order = 2), ForeignKey("Site")]
        public string SiteName { get; }
        [ForeignKey("Session")]
        public string SessionId { set; get; }

        // navigation properties
        [ForeignKey("SiteName")] public virtual Site Site { get; }
        [ForeignKey("SessionId")] public virtual Session Session { set; get; }
        // Auction associate, necessario join esplicito per capire seller/winner
        public virtual ICollection<Auction>Auction { set; get; } 
    }

    public partial class Session : ISession
    {
        [Key] public string Id { get; }
        [Required] public DateTime ValidUntil { set; get; }

        // foreign key
        [Required, ForeignKey("User"), Column(Order = 1)]
        public string Username { get; }
        [Required, ForeignKey("User"), Column(Order = 2)]
        public string SiteName { get; }

        // navigation propreties
        public virtual Site Site { get; } 
        public virtual IUser User { get; } // TODO correzione
    }

    public partial class Auction : IAuction
    {
        [NotMapped] internal const int MaxAuctionDesc = 1000;

        [Key, Column(Order = 1)]
        public int Id { get; }
        [StringLength(MaxAuctionDesc)]
        public string Description { get; }
        [Required]
        public DateTime EndsOn { get; }
        public bool FirstBid { set; get; }
        public double CurrentPrice { set; get; }
        public double HighestPrice { set; get; }

        // Foreign Key
        [Key, Column(Order = 2), ForeignKey("Site")]
        public string SiteName { get; }
        [Required, ForeignKey("Seller"), Column(Order = 1)]
        public string SellerUsername { get; }
        [ForeignKey("Winner"), Column(Order = 1)]
        public string WinnerUsername { set; get; }

        // Navigation properties
        [ForeignKey("SiteName")]
        public virtual Site Site { get; }
        [ForeignKey("SellerUsername")]
        public virtual IUser Seller { get; }
        [ForeignKey("WinnerUsername")]
        public virtual User Winner { set; get; } // da determinare successivamente

    }
}
