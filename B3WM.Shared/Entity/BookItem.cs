using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    public class BookItem
    {
        public Ticks2.ActionType Type { get; set; }
        public Ticks2.Agents Agent { get; set; }
        public int Volume { get; set; }
        public double Value { get; set; }
    }

    public class BookItemComparer : IEqualityComparer<BookItem>
    {
        // Products are equal if their names and product numbers are equal.
        public bool Equals(BookItem x, BookItem y)
        {

            //Check whether the compared objects reference the same data.
            if (Object.ReferenceEquals(x, y)) return true;

            //Check whether any of the compared objects is null.
            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
                return false;

            //Check whether the products' properties are equal.
            return x.Agent == y.Agent && x.Volume == y.Volume && x.Type == y.Type && x.Value == x.Value;
        }

        // If Equals() returns true for a pair of objects
        // then GetHashCode() must return the same value for these objects.

        public int GetHashCode(BookItem x)
        {
            //Check whether the object is null
            if (Object.ReferenceEquals(x, null)) return 0;

            //Get hash code for the Name field if it is not null.
            int hashProductName = x.Agent.GetHashCode();

            //Get hash code for the Code field.
            int hashProductCode = x.Volume.GetHashCode();

            //Calculate the hash code for the product.
            return hashProductName ^ hashProductCode;
        }
    }
}
