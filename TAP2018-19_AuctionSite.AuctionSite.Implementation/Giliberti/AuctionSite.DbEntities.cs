using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TAP2018_19.AuctionSite.Interfaces;

namespace Giliberti
{
    /// <summary>
    /// Each of this classes declares a db relationship to implement the corresponding permanent object (model)
    /// </summary>

    public class SiteEntity
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
        public virtual ICollection<UserEntity> Users { set; get; }

        // constructors
        public SiteEntity()
        {

        }
        public SiteEntity(string name, int timezone, int sessionExpirationTimeInSeconds, double minimumBidIncrement)
        {
            Name = name;
            Timezone = timezone;
            SessionExpirationInSeconds = sessionExpirationTimeInSeconds;
            MinimumBidIncrement = minimumBidIncrement;
        }
    }

    public class UserEntity
    {
        [Key, Column(Order = 1)]
        [MinLength(DomainConstraints.MinUserName), MaxLength(DomainConstraints.MaxUserName)]
        public string Username { set; get; }
        [MinLength(DomainConstraints.MinUserPassword)] // no MaxLenght per hashing
        public string Password { set; get; }

        // foreign key
        [Key, ForeignKey("Site"), Column(Order = 0)]
        public string SiteName { set; get; }

        // navigation properties
        public virtual SiteEntity Site { set; get; }
        public virtual ICollection<SessionEntity> Sessions { set; get; }
        // Auction associate, necessario join esplicito per capire seller/winner
        public virtual ICollection<AuctionEntity> Auctions { set; get; }

        // constructors
        public UserEntity()
        {
        }

        public UserEntity(string username, string password, string siteName)
        {
            Username = username;
            Password = password;
            SiteName = siteName;
        }
    }

    public class SessionEntity
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
        public virtual UserEntity User { set; get; }

        // constructors
        public SessionEntity()
        {

        }
        public SessionEntity(DateTime validUntil, string username, string siteName)
        {
            Id = siteName + username;
            ValidUntil = validUntil;
            Username = username;
            SiteName = siteName;
        }
    }

    public class AuctionEntity
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
        public string WinnerUsername { set; get; }

        // Foreign Key
        [Key, Column(Order = 0), ForeignKey("Seller")]
        public string SiteName { set; get; }
        [ForeignKey("Seller"), Column(Order = 1)]
        [Required]
        public string SellerUsername { set; get; }

        // Navigation properties
        public virtual UserEntity Seller { set; get; }

        // constructors
        public AuctionEntity()
        {

        }
        public AuctionEntity(string description, DateTime endsOn, double startingPrice, string username, string siteName)
        {
            Description = description;
            EndsOn = endsOn;
            SellerUsername = username;
            SiteName = siteName;
            FirstBid = true;
            CurrentPrice = startingPrice;
            HighestPrice = startingPrice;
            WinnerUsername = null;
        }
    }
}

