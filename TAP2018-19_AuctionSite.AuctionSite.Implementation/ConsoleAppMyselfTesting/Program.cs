using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Giliberti;

namespace ConsoleAppMyselfTesting
{
    class Program
    {
        static void Main(string[] args)
        {
            var sf = new SiteFactory();
            sf.Setup(
                "Data Source=.\\SQLEXPRESS;Initial Catalog=GilibertiDb;Integrated Security=True;MultipleActiveResultSets=True;");
        }
    }
}
