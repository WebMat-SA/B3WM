using B3WM.Shared.Extensions;

namespace B3WM.Client.Services
{
    public class OAccountService : IObserver<UsersTokens>, IDisposable
    {

        public event Action ActionNext;

        public OAccountService(IAccountService _accountService)
        {
            Subscribe(_accountService);
        }

        public virtual void OnNext(UsersTokens user)
        {
            ActionNext?.Invoke();
        }

        public virtual void OnCompleted() { }
        public virtual void OnError(Exception exception) { }

        private IDisposable unsubscriber;

        public virtual void Subscribe(IObservable<UsersTokens> provider)
        {
            if (provider != null)
                unsubscriber = provider.Subscribe(this);
        }

        public virtual void Unsubscribe()
        {
            unsubscriber.Dispose();
        }

        public void Dispose()
        {
            Unsubscribe();
        }
    }
}
