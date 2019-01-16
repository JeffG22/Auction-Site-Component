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
    /*
     * User_Session_Target: : Multiplicity is not valid in Role 'User_Session_Target' in relationship 'User_Session'. Because the Dependent Role properties are not the key properties, the upper bound of the multiplicity of the Dependent Role must be '*'.
       Auction_Seller_Target_Auction_Seller_Source: : The number of properties in the Dependent and Principal Roles in a relationship constraint must be identical.
       Auction_Winner_Target_Auction_Winner_Source: : The number of properties in the Dependent and Principal Roles in a relationship constraint must be identical.
       User_Session_Target: : Multiplicity is not valid in Role 'User_Session_Target' in relationship 'User_Session'. Because the Dependent Role properties are not the key properties, the upper bound of the multiplicity of the Dependent Role must be '*'.
       Auction_Seller_Target_Auction_Seller_Source: : The number of properties in the Dependent and Principal Roles in a relationship constraint must be identical.
       Auction_Winner_Target_Auction_Winner_Source: : The number of properties in the Dependent and Principal Roles in a relationship constraint must be identical.
       
         */
    public partial class Site : ISite
    {
        [Key, MinLength(DomainConstraints.MinSiteName), MaxLength(DomainConstraints.MaxSiteName)]
        public string Name { set; get; } // Name: unique, not null, not empty - identification -> PK
        [Required, Range(DomainConstraints.MinTimeZone, DomainConstraints.MaxTimeZone)]
        public int Timezone { set; get; } // TimeZone: required - get the time
        [Required]
        public double MinimumBidIncrement { set; get; } // MinInc: required - minimum increment allowed
        [Required]
        public int SessionExpirationInSeconds { set; get; } // TimeOut: required - inactive session

        // navigation properties
        public virtual ICollection<Auction> Auctions { set; get; }
        public virtual ICollection<User> Users { set; get; }
    }

    public partial class User : IUser
    {
        [Key, Column(Order = 1), MinLength(DomainConstraints.MinUserName), MaxLength(DomainConstraints.MaxUserName)]
        public string Username { set; get; }
        [MinLength(DomainConstraints.MinUserPassword)] // no MaxLenght per hashing
        public string Password { set; get; } // TODO gestione hashing, set e get

        // foreign key
        [Key, ForeignKey("Site"), Column(Order = 0)]
        public string SiteName { set; get; }
        [ForeignKey("Session")]
        public string SessionId { set; get; }

        // navigation properties
        /*[ForeignKey("SiteName")] */public virtual Site Site { set; get; }
        /*[ForeignKey("SessionId")] */public virtual Session Session { set; get; }
        // Auction associate, necessario join esplicito per capire seller/winner
        public virtual ICollection<Auction> Auctions { set; get; } 
    }

    public partial class Session : ISession
    {
        [Key] public string Id { set; get; }
        [Required] public DateTime ValidUntil { set; get; }

        // foreign key
        [Required, ForeignKey("User"), Column(Order = 0)]
        public string SiteName { set; get; }
        [Required, ForeignKey("User"), Column(Order = 1)]
        public string Username { set; get; }
        

        // navigation propreties
        //[ForeignKey("SiteName")] public virtual Site Site { set; get; } // posso non metterlo in relazione con il sito
        public virtual User User { set; get; }

        [NotMapped] // TODO serve?
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

        [Key, Column(Order = 1)]
        public int Id { set; get; }
        [StringLength(MaxAuctionDesc)]
        public string Description { set; get; }
        [Required]
        public DateTime EndsOn { set; get; }
        public bool FirstBid { set; get; }
        public double CurrentPrice { set; get; }
        public double HighestPrice { set; get; }

        // Foreign Key
        [Key, Column(Order = 0), ForeignKey("Site")]
        public string SiteName { set; get; }
        [Required, ForeignKey("Seller"), Column(Order = 2)]
        public string SellerUsername { set; get; }
        [ForeignKey("Winner"), Column(Order = 3)]
        public string WinnerUsername { set; get; }

        // Navigation properties
        [ForeignKey("SiteName")]
        public virtual Site Site { set; get; }
        [ForeignKey("SellerUsername")]
        public virtual User Seller { set; get; }
        [ForeignKey("WinnerUsername")]
        public virtual User Winner { set; get; } // da determinare successivamente

        [NotMapped]
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
