using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    public class Striking
    {
        public Striking()
        {
            Strikers = new Dictionary<Ticks2.Agents, int>();
            Counter = 0;
        }

        public Dictionary<Ticks2.Agents, int> Strikers { get; set; }

        public double Percent { get; set; }

        [JsonIgnore]
        public int Counter { get; set; }

        public void Calculate(int Count)
        {
            Percent = (Counter * 100.0d) / Count;
        }

        public void AddStriker(Ticks2.Agents agent)
        {
            if (Strikers.ContainsKey(agent))
                Strikers[agent]++;
            else
                Strikers.Add(agent, 1);
        }

    }

    //melhoras proxima versão
    //== separar quem são os maiores agressores de trocação por compra / venda
}
