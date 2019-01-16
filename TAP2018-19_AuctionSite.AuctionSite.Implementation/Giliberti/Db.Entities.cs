using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using TAP2018_19.AuctionSite.Interfaces;

namespace Giliberti
{
    // TODO assegnazione foreign key
    // TODO verifica set

    public partial class Site : ISite
    {
        [Key]
        [MinLength(DomainConstraints.MinSiteName), MaxLength(DomainConstraints.MaxSiteName)]
        public string Name { set; get; }
        [Required]
        [Range(DomainConstraints.MinTimeZone, DomainConstraints.MaxTimeZone)]
        public int Timezone { set; get; } // TimeZone: required - get the time
        [Required]
        public double MinimumBidIncrement { set; get; } // MinInc: required - minimum increment allowed
        [Required]
        public int SessionExpirationInSeconds { set; get; } // TimeOut: required - inactive session

        // navigation properties
        public virtual ICollection<User> Users { set; get; }
    }

    public partial class User : IUser
    {
        [Key, Column(Order = 1)]
        [MinLength(DomainConstraints.MinUserName), MaxLength(DomainConstraints.MaxUserName)]
        public string Username { set; get; }
        [MinLength(DomainConstraints.MinUserPassword)] // no MaxLenght per hashing
        public string Password { set; get; } // TODO gestione hashing, set e get

        // foreign key
        [Key, ForeignKey("Site"), Column(Order = 0)]
        public string SiteName { set; get; }

        // navigation properties
        /*[ForeignKey("SiteName")]*/ public virtual Site Site { set; get; }
        public virtual ICollection<Session> Sessions { set; get; }
        // Auction associate, necessario join esplicito per capire seller/winner
        public virtual ICollection<Auction> Auctions { set; get; } 
    }

    public partial class Session : ISession
    {
        [Key] public string Id { set; get; }
        [Required] public DateTime ValidUntil { set; get; }

        // foreign key
        [ForeignKey("User"), Column(Order = 0)]
        [Required]
        public string SiteName { set; get; }
        [ForeignKey("User"), Column(Order = 1)]
        [Required]
        public string Username { set; get; }
        
        // navigation properties
        public virtual User User { set; get; }

        IUser ISession.User
        {
            get
            {
                if (Db == null)
                    throw new UnavailableDbException("State of entity out of context, no data available");
                SiteFactory.ChecksOnDbConnection(Db);
                if (!Db.Sessions.Any(s => s.Id == Id))
                    throw new InvalidOperationException(nameof(Session) + " not consistent");
                return User;
            }
        }
    }

    public partial class Auction : IAuction
    {
        [NotMapped] internal const int MaxAuctionDesc = 1000;

        [Key, Column(Order = 2)]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { set; get; }
        [StringLength(MaxAuctionDesc)]
        public string Description { set; get; }
        [Required]
        public DateTime EndsOn { set; get; }
        public bool FirstBid { set; get; }
        public double CurrentPrice { set; get; }
        public double HighestPrice { set; get; }

        // Foreign Key
        [Key, Column(Order = 0), ForeignKey("Seller")]
        public string SiteName { set; get; }
        [ForeignKey("Seller"), Column(Order = 1)]
        [Required]
        public string SellerUsername { set; get; }


        [Column(Order = 3), ForeignKey("Winner")]
        public string SiteNameWinner { set; get; }
        [Column(Order = 4), ForeignKey("Winner")]
        public string WinnerUsername { set; get; }

        // Navigation properties
        public virtual User Seller { set; get; }
        public virtual User Winner { set; get; } // da determinare successivamente

        IUser IAuction.Seller
        {
            get
            {
                if (Db == null)
                    throw new UnavailableDbException("State of entity out of context, no data available");
                SiteFactory.ChecksOnDbConnection(Db);
                if (!Db.Auctions.Any(a => a.Id == Id && a.SiteName == SiteName))
                    throw new InvalidOperationException("the auction does not exist anymore");
                return Seller;
            }
        }
    }
}
