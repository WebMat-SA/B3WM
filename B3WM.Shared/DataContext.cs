using B3WM.Shared.Entity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace B3WM.Shared
{
    public class DataContext : DbContext, IDataContext
    {
        //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        //{
        //    //#warning To protect potentially sensitive information in your connection string, 
        //    //you should move it out of source code.See http://go.microsoft.com/fwlink/?LinkId=723263 
        //    //for guidance on storing connection strings.

        //    optionsBuilder.UseMySql(ConnectionString); //DefaultCommandTimeout 

        //}

        public DataContext(DbContextOptions<DataContext> options) : base(options) { }

        public DbSet<Alerts> Alerts { get; set; }
        public DbSet<Analysis> Analyses { get; set; }
        public DbSet<Bars> Bars { get; set; }
        public DbSet<BookInfo> BookInfo { get; set; }
        public DbSet<Credentials> Credentials { get; set; }
        public DbSet<CredentialsCustomers> CredentialsCustomers { get; set; }
        public DbSet<Customers> Customers { get; set; }
        public DbSet<Links> Links { get; set; }
        public DbSet<Plans> Plans { get; set; }
        public DbSet<Reminders> Reminders { get; set; }
        public DbSet<Ticks> Ticks { get; set; }
        public DbSet<Users> Users { get; set; }
        public DbSet<VWapDistortions> VWapDistortions { get; set; }
        public DbSet<Balances> Balances { get; set; }
        public DbSet<BalancesAgents> BalancesAgents { get; set; }
        public DbSet<SubscriptionsHotmart> SubscriptionsHotmart { get; set; }
        public DbSet<Setups> Setups { get; set; }
        public DbSet<SetupsCustomers> SetupsCustomers { get; set; }
        public DbSet<AuctionVolumes> AuctionVolumes { get; set; }
        public DbSet<AuctionVolumesAgents> AuctionVolumesAgents { get; set; }

        public DbSet<Sectors> Sectors { get; set; }
        public DbSet<SubSectors> SubSectors { get; set; }
        public DbSet<Segments> Segments { get; set; }

        public DbSet<SequentialActions> SequentialActions { get; set; }

        public DbSet<NotoriousOffers> NotoriousOffers { get; set; }
        public DbSet<NotoriousOffers2> NotoriousOffers2 { get; set; }
        public DbSet<MovimentShakers> MovimentShakers { get; set; }
        public DbSet<SequentialNotorious> SequentialNotorious { get; set; }

        public DbSet<OfferRenews> OfferRenews { get; set; }
        public DbSet<OfferRenewBreakups> OfferRenewBreakups { get; set; }

        public DbSet<SequentialActionsResumes> SequentialActionsResumes { get; set; }

        public DbSet<TicksCross> TicksCross { get; set; }
        public DbSet<Trends> Trends { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SubscriptionsHotmart>().HasKey(c => new { c.SubscriberCode, c.SubscriptionID });

            base.OnModelCreating(modelBuilder);
        }

    }

    public interface IDataContext
    {
        DbSet<Alerts> Alerts { get; set; }
        DbSet<Analysis> Analyses { get; set; }
        DbSet<Bars> Bars { get; set; }
        DbSet<BookInfo> BookInfo { get; set; }
        DbSet<Credentials> Credentials { get; set; }
        DbSet<CredentialsCustomers> CredentialsCustomers { get; set; }
        DbSet<Customers> Customers { get; set; }
        DbSet<Links> Links { get; set; }
        DbSet<Plans> Plans { get; set; }
        DbSet<Reminders> Reminders { get; set; }
        DbSet<Ticks> Ticks { get; set; }
        DbSet<Users> Users { get; set; }
        DbSet<VWapDistortions> VWapDistortions { get; set; }
        DbSet<Balances> Balances { get; set; }
        DbSet<BalancesAgents> BalancesAgents { get; set; }
        DbSet<SubscriptionsHotmart> SubscriptionsHotmart { get; set; }
        DbSet<Setups> Setups { get; set; }
        DbSet<SetupsCustomers> SetupsCustomers { get; set; }
        DbSet<AuctionVolumes> AuctionVolumes { get; set; }
        DbSet<AuctionVolumesAgents> AuctionVolumesAgents { get; set; }
        DbSet<Sectors> Sectors { get; set; }
        DbSet<SubSectors> SubSectors { get; set; }
        DbSet<Segments> Segments { get; set; }
        DbSet<SequentialActions> SequentialActions { get; set; }
        DbSet<NotoriousOffers> NotoriousOffers { get; set; }
        DbSet<NotoriousOffers2> NotoriousOffers2 { get; set; }
        DbSet<MovimentShakers> MovimentShakers { get; set; }
        DbSet<SequentialNotorious> SequentialNotorious { get; set; }
        DbSet<OfferRenews> OfferRenews { get; set; }
        DbSet<OfferRenewBreakups> OfferRenewBreakups { get; set; }
        DbSet<SequentialActionsResumes> SequentialActionsResumes { get; set; }
        DbSet<TicksCross> TicksCross { get; set; }
        DbSet<Trends> Trends { get; set; }
    }
}
