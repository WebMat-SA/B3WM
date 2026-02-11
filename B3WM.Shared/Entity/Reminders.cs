using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    [Table("Reminders")]
    public class Reminders
    {
        [JsonIgnore]
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RemindersID { get; set; }

        [ForeignKey("Customer")]
        public int? CustomerID { get; set; }

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

        private string _Text;
        public string Text 
        {
            get
            {
                string _text = _Text;
                if (Customer != null)
                    _text = _text.Replace("{paper}", Customer.Name);
                
                _text = _text.Replace("{date}", (Date.Date == DateTime.Today) ? $"hoje, {Date.ToShortDateString()}!" : $"{Date.ToLongDateString()}");

                return _text;
            }
            set => _Text = value; 
        }

        public DateTime Date { get; set; }

        [JsonIgnore]
        [Required, ForeignKey("User")]
        public int UserID { get; set; }
        [JsonIgnore]
        [ForeignKey("UserID")]
        public virtual Users User
        {
            get => _User;
            set
            {
                _User = value;
                if (_User != null) UserID = _User.UsersID;
            }
        }
        private Users _User;

        public Reminders Clone(DateTime dateTime)
        {
            var result = (Reminders)this.MemberwiseClone();
            result.Date = dateTime;
            return result;
        }
    }
}
