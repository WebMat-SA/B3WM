using B3WM.Shared.Entity;
using B3WM.Shared.Extensions;
using static B3WM.Shared.Entity.Ticks2;

namespace B3WM.Shared.Models
{
    /// <summary>
    /// Bubbles para IndexedDB. KeyPath "id" (case-sensitive). Sem timeframe.
    /// </summary>
    public class BubbleStorageItem
    {
        public double Price { get; set; }

        public int Agent { get; set; }

        public decimal Amount { get; set; }

        public DateTime Date { get; set; }

        public ActionType ActionType { get; set; }

        public string Symbol { get; set; }

        public override string ToString()
        {
            return $"Price: {Price}, Agent: {((Ticks2.Agents)Agent).Description()}, Amount: {Amount}, Date: {Date}, ActionType: {ActionType}, Symbol: {Symbol}";
        }
    }
}
