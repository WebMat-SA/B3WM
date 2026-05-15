using B3WM.Shared.Entity;
using B3WM.Shared.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Interfaces
{
    public interface IDataHubClient
    {
        #region Remover quando o client não tiver mais recebendo data nem ticks do rtd, pois tudo será processado no server
        Task ReceiveTnT(byte[] data);
        Task ReceiveBook(byte[] data);
        Task ReceiveTnTSimple(byte[] data);
        Task ReceiveCsvLines(string data);
        Task ReceiveTnTProfit(Ticks2[] data);
        #endregion


        Task ReceiveOnCloseBar(BarStorageItem data);
        Task ReceiveOnBubble(BubbleStorageItem data);
        Task ReceiveOnVolume(VolumeLevelStorageItem data);
    }
}
