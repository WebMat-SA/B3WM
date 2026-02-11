using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using System.Text.Json.Serialization;

namespace B3WM.Shared.Entity
{
    [Table("BookInfo")]
    public class BookInfo
    {
        [Key]
        [JsonIgnore]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BookInfoID { get; set; }
        [Required]
        public DateTime Date { get; set; }
        [Required]
        public double Price { get; set; }
        [Required]
        public long Volume { get; set; }
        [Required]
        public long Volume_Real { get; set; }
        [Required]
        public BookInfoType Type { get; set; }

        [JsonIgnore]
        [Required, ForeignKey("Customer")]
        public int CustomerID { get; set; }
        [JsonIgnore]
        [ForeignKey("CustomerID")]
        public virtual Customers Customer
        {
            get => _Customer;
            set
            {
                _Customer = value;
                if (_Customer != null) CustomerID = _Customer.CustomersID;
            }
        }
        private Customers _Customer;
    }
    public enum BookInfoType
    {
        Book_Sell,
        Book_Buy,
        Book_Sell_Market,
        Book_Buy_Market,
    }
}
