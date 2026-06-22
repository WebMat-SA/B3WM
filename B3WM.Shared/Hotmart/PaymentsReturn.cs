using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace B3WM.Shared.Hotmart
{
    public class PaymentsReturn
    {
        public List<PaymentsReturnItem> items { get; set; }
        public PaymentsReturnPageInfo page_info { get; set; }

    }

    public class PaymentsReturnPageInfo
    {
        public int total_results { get; set; }
        public string next_page_token { get; set; }
        public string prev_page_token { get; set; }
        public int results_per_page { get; set; }
    }

    public class PaymentsReturnItem
    {
        public string subscriber_code { get; set; }
        public int subscription_id { get; set; }
        public string status { get; set; }
        public long accession_date { get; set; }
        public long request_date { get; set; }
        public bool trial { get; set; }

        public PaymentsReturnItemPlan plan { get; set; }
        public PaymentsReturnItemProduct product { get; set; }
        public PaymentsReturnItemPrice price { get; set; }
        public PaymentsReturnItemSubscriber subscriber { get; set; }

        [JsonIgnore]
        public DateTime Accession_date { get => new DateTime(1970, 01, 01, 0, 0, 0, DateTimeKind.Utc).Add(TimeSpan.FromMilliseconds(accession_date)); }

        [JsonIgnore]
        public DateTime Request_date { get => new DateTime(1970, 01, 01, 0, 0, 0, DateTimeKind.Utc).Add(TimeSpan.FromMilliseconds(request_date)); }

    }

    public class PaymentsReturnItemPlan
    {
        public string name { get; set; }
    }

    public class PaymentsReturnItemProduct
    {
        public int id { get; set; }
        public string name { get; set; }
        public string ucode { get; set; }
    }

    public class PaymentsReturnItemPrice
    {
        public double value { get; set; }
        public string currency_code { get; set; }
    }

    public class PaymentsReturnItemSubscriber
    {
        public string name { get; set; }

        public string email { get; set; }
        public string ucode { get; set; }

    }
}
