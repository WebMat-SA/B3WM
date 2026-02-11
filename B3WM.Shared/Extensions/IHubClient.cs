using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Extensions
{
    public interface IHubClient
    {
        Task UpdateTickList(IEnumerable<B3WM.Shared.Entity.Ticks> tickList);
        Task UpdateTimesAndSalesList(IEnumerable<B3WM.Shared.Entity.TimesAndSales> tickList, DateTime date);

        Task LogOut(string messageToClient);
    }
}
