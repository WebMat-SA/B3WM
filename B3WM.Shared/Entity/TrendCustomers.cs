using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    public class TrendCustomers
    {
        public List<Trends> UpTrends { get; set; }
        public List<Trends> DownTrends { get; set; }

        public List<Customers> UpCustomers => UpTrends.GroupBy(q => q.Customer).Select(q=>q.Key).ToList();
        public List<Customers> DownCustomers => DownTrends.GroupBy(q => q.Customer).Select(q=>q.Key).ToList();

        public TrendCustomers()
        {
            UpTrends = new List<Trends>();
            DownTrends = new List<Trends>();
        }
    }
}
