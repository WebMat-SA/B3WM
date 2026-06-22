using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;

namespace B3WM.Shared.Entity
{
    [Table("Bars")]
    public class Bars 
    {
        [Key]
        [JsonIgnore]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BarsID { get; set; }

        [Required]
        [JsonPropertyName("Data")]
        public DateTime Date { get; set; }
        [Required]
        [JsonPropertyName("Abertura")]
        public double Open { get; set; }
        [Required]
        [JsonPropertyName("Alta")]
        public double High { get; set; } 
        [Required]
        [JsonPropertyName("Baixa")]
        public double Low { get; set; } 
        [Required]
        [JsonPropertyName("Fechamento")]
        public double Close { get; set; } 
        [Required]
        [JsonPropertyName("Volume")]
        public long Volume { get; set; }
        [JsonIgnore]
        [Required]
        public long TickVolume { get; set; }
        [JsonIgnore]
        [Required]
        public int Spread { get; set; }
        [JsonIgnore]
        [Required]
        public int TypeTime
        {
            get => (int)_TypeTime;
            set => _TypeTime = (TypeTimes)value;
        }

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

        [JsonIgnore]
        [NotMapped]
        public TypeTimes _TypeTime { get; set; }

        [JsonIgnore]
        [NotMapped]
        public double FinancialVolume
        {
            get => ((Open + High + Low + Close) * Volume) / 4.0d;
        }

        public enum TypeTimes
        {
            [Description("1min")]
            OneMin = 1,
            [Description("5min")]
            FiveMin = 5,
            [Description("15min")]
            FifiteenMin = 15,
            [Description("30min")]
            ThirtyMin = 30,
            [Description("60min")]
            SixtyMin = 60,
            [Description("Diario")]
            Daily = 1440,
            [Description("Semanal")]
            Weekly = 10080,
            [Description("Mensal")]
            Monthly = 43200,
        }



        public override string ToString()
        {
            return $"Close:{Close.ToString("0.00")} - Low:{Low.ToString("0.00")} - High:{High.ToString("0.00")} - Open:{Open.ToString("0.00")} - Amplitude: {(High - Low).ToString("0.00")} - Date:{Date.ToString("dd/MM/yyyy HH:mm:ss")}";
        }
    }

}
