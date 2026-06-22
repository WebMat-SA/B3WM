using B3WM.Shared.Hotmart;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    [Table("SubscriptionsHotmart")]
    public class SubscriptionsHotmart
    {
        public SubscriptionsHotmart() { }

        public SubscriptionsHotmart(PaymentsReturnItem PaymentReturn, Plans _plan, Users _user)
        {
            SubscriberCode = PaymentReturn.subscriber_code;
            SubscriptionID = PaymentReturn.subscription_id;
            Status = PaymentReturn.status;
            AccessionDate = PaymentReturn.Accession_date;
            RequestDate = PaymentReturn.Request_date;
            Trial = PaymentReturn.trial;
            Plan = _plan;
            ProductID = PaymentReturn.product.id;
            User = _user;
        }

        [Required]
        public string SubscriberCode { get; set; }

        [Required]
        public int SubscriptionID { get; set; }

        [Required]
        public string Status { get; set; }

        [Required]
        public DateTime AccessionDate { get; set; }

        [Required]
        public DateTime RequestDate { get; set; }

        [Required]
        public bool Trial { get; set; }

        [Required, ForeignKey("Plan")]
        public int PlanID { get; set; }
        [ForeignKey("PlanID")]
        public virtual Plans Plan
        {
            get => _Plan;
            set
            {
                _Plan = value;
                if (_Plan != null) PlanID = _Plan.PlansID;
            }
        }
        private Plans _Plan;

        [Required]
        public int ProductID { get; set; }

        [Required, ForeignKey("User")]
        public int UserID { get; set; }
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

    }
}
