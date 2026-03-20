using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Interfaces
{
    public interface IDataHubClient
    {
        Task ReceiveTnT(byte[] data);
        Task ReceiveBook(byte[] data);
        Task ReceiveTnTSimple(byte[] data);
        Task ReceiveCsvLines(string[] data);
    }
}
