using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Extensions
{
    public class UnloggedException : Exception
    {
        public UnloggedException(string msg) : base(msg) { }
    }
}
