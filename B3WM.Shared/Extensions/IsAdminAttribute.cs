using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Extensions
{
    public class IsAdminAttribute: Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class PersistStateAttribute : Attribute
    {
    }
}
