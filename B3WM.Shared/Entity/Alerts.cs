using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json.Serialization;
using System.ComponentModel;

namespace B3WM.Shared.Entity
{
    [Table("Alerts")]
    public class Alerts
    {
        public Alerts()
        {
            IsActive = true;
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AlertsID { get; set; }

        public double Value { get; set; }

        public bool IsActive { get; set; }
        public UnitsType UnitType { get; set; }

        private BindType _Bind;
        public BindType Bind 
        { 
            get => _Bind;
            set
            {
                _Bind = value;

                if (_Bind == BindType.AuctionTimeRemoved || _Bind == BindType.V1MinAbove || _Bind == BindType.AuctionIn || _Bind == BindType.AuctionOut || _Bind == BindType.TicksQuantityAbove || _Bind == BindType.MarketClose || _Bind == BindType.MovimentShaker)
                    UnitType = UnitsType.Value;

                if (_Bind == BindType.AuctionIn || _Bind == BindType.AuctionOut || _Bind == BindType.AuctionTimeRemoved || _Bind == BindType.MovimentShaker)
                    Value = 0;

                if (_Bind == BindType.AgentParticipating || _Bind == BindType.NotoriousVolume)
                    UnitType = UnitsType.Percent;
            }
        }

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
    }

    public enum BindType
    {
        [Display(Description = "Relógio de reilão removido")]
        [Description("Relógio de reilão removido")]
        AuctionTimeRemoved,
        [Display(Description = "Distorção de alta (VWAP)")]
        [Description("Distorção de alta (VWAP)")]
        DistortionUp,
        [Display(Description = "Distorção de baixa (VWAP)")]
        [Description("Distorção de baixa (VWAP)")]
        DistortionDown,
        [Display(Description = "Volatilidade no último minuto")]
        [Description("Volatilidade no último minuto")]
        V1MinAbove,
        [Display(Description = "Proximidade do leilão de alta")]
        [Description("Proximidade do leilão de alta")]
        AuctionUp,
        [Display(Description = "Proximidade do leilão de baixa")]
        [Description("Proximidade do leilão de baixa")]
        AuctionDown,
        [Display(Description = "Entrada em leilão")]
        [Description("Entrada em leilão")]
        AuctionIn,
        [Display(Description = "Saída de leilão")]
        [Description("Saída de leilão")]
        AuctionOut,
        [Display(Description = "Valor do Spread Acima de")]
        [Description("Valor do Spread Acima de")]
        SpreadUp,
        [Display(Description = "Quantidades de Negócios Acima de")]
        [Description("Quantidades de Negócios Acima de")]
        TicksQuantityAbove,
        [Display(Description = "Tempo para Fechamento do Mercado (16:55)")]
        [Description("Tempo para Fechamento do Mercado (16:55)")]
        MarketClose,
        [Display(Description = "Agente Participante (Volume) maior que")]
        [Description("Agente Participante (Volume) maior que")]
        AgentParticipating,
        [Display(Description = "Ações Sequenciais do mesmo Agente (Até 3 segundos)")]
        [Description("Ações Sequenciais do mesmo Agente (Até 3 segundos)")]
        SequentialActions,
        [Display(Description = "Renovação de lote por volume")]
        [Description("Renovação de lote por volume")]
        OfferRenewalVolume,
        [Display(Description = "Reconhecimento de Padrão de 'Compra Caro, Vendo Barato'")]
        [Description("Reconhecimento de Padrão de 'Compra Caro, Vendo Barato'")]
        MovimentShaker,
        [Display(Description = "Volume diário distoante em relação ao volume médio dos últimos dias")]
        [Description("Volume diário distoante em relação ao volume médio dos últimos dias")]
        NotoriousVolume,
        [Display(Description = "Variação em relação ao fechamento do dia anterior")]
        [Description("Variação em relação ao fechamento do dia anterior")]
        Variation,
    }

    public enum UnitsType
    {
        [Display(Description = "Reais")]
        [Description("Reais")]
        Value,
        [Display(Description = "Porcentagem")]
        [Description("Porcentagem")]
        Percent
    }
}
