using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    public class TWapNotorious
    {
        public enum Type
        {
            StopedTWap,
            WhenOnMovimentPrice,
        }


        public TWapAgent TWapAverage { get; set; }
        public Type TypeNotion { get; set; }
    }
    
}
