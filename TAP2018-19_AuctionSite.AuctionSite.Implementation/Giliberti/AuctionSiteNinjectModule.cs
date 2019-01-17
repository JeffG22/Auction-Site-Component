using System.Data.Entity;
using Ninject.Modules;
using TAP2018_19.AuctionSite.Interfaces;

namespace Giliberti
{
    public class AuctionSiteNinjectModule : NinjectModule
    {
        public override void Load()
        {
            Database.SetInitializer(new DropCreateDatabaseAlways<AuctionSiteContext>());
            // SingletonScope
            Bind<ISiteFactory>().To<SiteFactory>().InSingletonScope();
            // Default: Transient Scope
            Bind<ISite>().To<Site>();
            Bind<ISession>().To<Session>();
            Bind<IUser>().To<User>();
            Bind<IAuction>().To<Auction>();
        }
    }
}
