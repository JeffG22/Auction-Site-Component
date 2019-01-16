using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ninject.Modules;
using TAP2018_19.AuctionSite.Interfaces;

namespace Giliberti
{
    public class AuctionSiteNinjectModule : NinjectModule
    {
        public override void Load()
        {
            Database.SetInitializer<AuctionSiteContext>(new DropCreateDatabaseAlways<AuctionSiteContext>());
            // SingletonScope
            this.Bind<ISiteFactory>().To<SiteFactory>().InSingletonScope();
            // Default: Transient Scope
            this.Bind<ISite>().To<Site>();
            this.Bind<ISession>().To<Session>();
            this.Bind<IUser>().To<User>();
            this.Bind<IAuction>().To<Auction>();
        }
    }
}
