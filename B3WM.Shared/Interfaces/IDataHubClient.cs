using B3WM.Shared.Entity;
using B3WM.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace B3WM.Shared.Interfaces
{
    public interface IDataHubClient
    {
        Task ReceiveOnCloseBar(BarStorageItem data);
        Task ReceiveOnBubble(BubbleStorageItem data);
        Task ReceiveOnStructure(StructureStorageItem data);
        Task ReceiveOnForecast(AdjustmentForecastItem data);
        Task ReceiveThrottlingData(ThrottlingData data);
        Task ReceiveOnIndicatorValue(IndicatorValue data);
    }
}
