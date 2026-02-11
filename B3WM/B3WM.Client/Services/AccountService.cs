using B3WM.Shared.Entity;
using B3WM.Shared.Extensions;
using Blazored.SessionStorage;
using System.Net.Http.Json;
using System.Text.Json;

namespace B3WM.Client.Services
{
    public class AccountService : IAccountService
    {
        private readonly HttpClient httpClient;
        private readonly ISyncSessionStorageService sessionStorage;

        private List<IObserver<UsersTokens>> observers;
        public bool isLogged => UserLogged != null;

        public AccountService(HttpClient _httpClient, ISyncSessionStorageService sessionStorageService)
        {
            httpClient = _httpClient;
            sessionStorage = sessionStorageService;
            observers = new List<IObserver<UsersTokens>>();
        }

        public IDisposable Subscribe(IObserver<UsersTokens> observer)
        {
            if (!observers.Contains(observer))
                observers.Add(observer);
            return new Unsubscriber(observers, observer);
        }

        public UsersTokens UserToken
        {
            get => sessionStorage.GetItem<UsersTokens>("UserToken");
            private set
            {
                sessionStorage.SetItem<UsersTokens>("UserToken", value);
                UpdateObservers();
            }
        }


        public Users UserLogged
        {
            get => (UserToken == null) ? null : UserToken.User;
        }

        public string Token
        {
            get => (UserToken == null) ? null : UserToken.Token;
        }

        private void UpdateObservers()
        {
            foreach (var observer in observers)
            {
                observer.OnNext(UserToken);
            }
        }

        public async Task<bool> Login(Users _user)
        {

            // criptografa... segurança chofem.
            _user.EncryptPassword();

            var response = await httpClient.PostAsJsonAsync("gc/Account/Login", new { email = _user.Email, password = _user.Password });
            var responseStr = await response.Content.ReadAsStringAsync();

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // Deserialize Result<JsonElement> so we can rehydrate the nested object using System.Text.Json
            var resultObj = JsonSerializer.Deserialize<Result<JsonElement>>(responseStr, options);

            if (resultObj == null)
                throw new Exception("Invalid response from server.");

            if (resultObj.IsValid)
            {
                // Use JsonElement.Deserialize<T>() to convert the nested JSON to UsersTokens
                var subItem = resultObj.Data.Deserialize<UsersTokens>(options);
                if (subItem == null)
                    throw new Exception("Failed to deserialize UsersTokens from response.");

                UserToken = subItem;

                await Task.Delay(2000);
            }
            else
            {
                throw new Exception(resultObj.Data.ToString());
            }

            _user = new Users() { Email = _user.Email };

            return true;

        }

        public void LogOut()
        {
            sessionStorage.Clear();
            UpdateObservers();
        }

        public async Task<bool> Signin(Users _user, string OldPassword, string Token)
        {
            //criptografa... segurança chofem.
            _user.EncryptPassword();

            //se nao tem token nem oldPassword == novo usuario
            if (string.IsNullOrEmpty(OldPassword) && string.IsNullOrEmpty(Token))
            {
                //requisicao para novo usuario

                var response = await httpClient.PostAsJsonAsync("gc/Account/SignIn", new { email = _user.Email, password = _user.Password, fullname = _user.FullName, phone = _user.Phone });

                var responseStr = await response.Content.ReadAsStringAsync();

                var resultObj = System.Text.Json.JsonSerializer.Deserialize<Result<object>>(responseStr);

                if (resultObj.IsValid)
                    return bool.Parse(resultObj.Data.ToString());//return ((Newtonsoft.Json.Linq.JToken)resultObj.Data).ToObject<bool>();
                else
                    throw new Exception(resultObj.Data.ToString());

            }
            else
            {

                HttpResponseMessage response = null;

                if (!string.IsNullOrEmpty(Token))
                {
                    //esqueci minha senha
                    response = await httpClient.PostAsJsonAsync("gc/Account/NewPassword", new { token = Token, password = _user.Password, oldPassword = "", email = "", phone = "" });
                }
                else
                {
                    //alterar senha -> alterar dados
                    response = await httpClient.PostAsJsonAsync("gc/Account/NewPassword", new { email = _user.Email, oldPassword = OldPassword, password = _user.Password, phone = _user.Phone, token = "" });
                }

                var responseStr = await response.Content.ReadAsStringAsync();

                var resultObj = System.Text.Json.JsonSerializer.Deserialize<Result<object>>(responseStr);

                if (resultObj.IsValid)
                    return bool.Parse(resultObj.Data.ToString()); //return ((Newtonsoft.Json.Linq.JToken)resultObj.Data).ToObject<bool>();
                else
                    throw new Exception(resultObj.Data.ToString());
            }

            return false;
        }

        public async Task<bool> ForgetPassword(string email)
        {
            var response = await httpClient.GetFromJsonAsync<Result<object>>("gc/Account/ForgetPassword?email=" + email);

            //var responseStr = await response.Content.ReadAsStringAsync();

            //var resultObj = Newtonsoft.Json.JsonConvert.DeserializeObject<Result<object>>(responseStr);

            if (response.IsValid)
                return bool.Parse(response.Data.ToString());//return ((Newtonsoft.Json.Linq.JToken)resultObj.Data).ToObject<bool>();
            else
                throw new Exception(response.Data.ToString());

            return false;
        }

        public async Task<bool> ActiveAccount(string token)
        {
            var response = await httpClient.PostAsJsonAsync("gc/Account/ActivateAccount", new { token = token });

            var responseStr = await response.Content.ReadAsStringAsync();

            var resultObj = System.Text.Json.JsonSerializer.Deserialize<Result<object>>(responseStr);

            if (resultObj.IsValid)
                return bool.Parse(resultObj.Data.ToString());//return ((Newtonsoft.Json.Linq.JToken)resultObj.Data).ToObject<bool>();
            else
                throw new Exception(resultObj.Data.ToString());

            return false;
        }



        public void AddCredCust(CredentialsCustomers item, string credToken)
        {
            var userLogged = UserLogged;

            var cred = userLogged.Credentials.FirstOrDefault(q => q.Code == credToken);

            //remove da lista
            userLogged.Credentials.Remove(cred);

            //popula
            cred.WatchListCustomers.Add(item);

            //devolve pra lista
            userLogged.Credentials.Add(cred);

            var temp = UserToken.Clone();
            temp.User = userLogged;

            UserToken = temp;
        }

        public void AddCredCust(List<CredentialsCustomers> item, string credToken)
        {
            var userLogged = UserLogged;

            var cred = userLogged.Credentials.FirstOrDefault(q => q.Code == credToken);

            //remove da lista
            userLogged.Credentials.Remove(cred);

            foreach (var i in item)
            {
                //popula
                cred.WatchListCustomers.Add(i);

            }

            //devolve pra lista
            userLogged.Credentials.Add(cred);

            var temp = UserToken.Clone();
            temp.User = userLogged;

            UserToken = temp;
        }

        public void DelCredCust(string paper, string credToken)
        {
            var userLogged = UserLogged;

            var cred = userLogged.Credentials.FirstOrDefault(q => q.Code == credToken);

            //remove da lista
            userLogged.Credentials.Remove(cred);

            //popula
            cred.WatchListCustomers.Remove(cred.WatchListCustomers.First(q => q.Customer.Symbol == paper));

            //devolve pra lista
            userLogged.Credentials.Add(cred);

            var temp = UserToken.Clone();
            temp.User = userLogged;

            UserToken = temp;
        }

        public void DelCredCust(List<CredentialsCustomers> item, string credToken)
        {
            var userLogged = UserLogged;

            var cred = userLogged.Credentials.FirstOrDefault(q => q.Code == credToken);

            //remove da lista
            userLogged.Credentials.Remove(cred);
            foreach (var paper in item)
            {
                //popula
                cred.WatchListCustomers.Remove(cred.WatchListCustomers.First(q => q.Customer.Symbol.ToUpper() == paper.Customer.Symbol.ToUpper()));
            }

            //devolve pra lista
            userLogged.Credentials.Add(cred);

            var temp = UserToken.Clone();
            temp.User = userLogged;

            UserToken = temp;
        }

        public void UpdateCredential(List<Credentials> item)
        {

            var userLogged = UserLogged;

            //devolve pra lista
            userLogged.Credentials = item;

            var temp = UserToken.Clone();
            temp.User = userLogged;

            UserToken = temp;
        }

        public void UpdateAlerts(List<Alerts> items)
        {

            var userLogged = UserLogged;

            //devolve pra lista
            userLogged.Alerts = items;

            var temp = UserToken.Clone();
            temp.User = userLogged;

            UserToken = temp;
        }
    }

    class Unsubscriber : IDisposable
    {
        private List<IObserver<UsersTokens>> _observers;
        private IObserver<UsersTokens> _observer;

        public Unsubscriber(List<IObserver<UsersTokens>> observers, IObserver<UsersTokens> observer)
        {
            this._observers = observers;
            this._observer = observer;
        }

        public void Dispose()
        {
            if (_observer != null && _observers.Contains(_observer))
                _observers.Remove(_observer);
        }
    }

    public interface IAccountService : IObservable<UsersTokens>
    {
        Task<bool> Login(Users _user);
        void LogOut();
        Task<bool> Signin(Users _user, string oldPassword, string Token);
        Task<bool> ForgetPassword(string email);

        Task<bool> ActiveAccount(string Token);

        void AddCredCust(CredentialsCustomers item, string credToken);
        void AddCredCust(List<CredentialsCustomers> item, string credToken);
        void DelCredCust(string item, string credToken);
        void DelCredCust(List<CredentialsCustomers> item, string credToken);

        void UpdateCredential(List<Credentials> item);

        void UpdateAlerts(List<Alerts> itens);

        bool isLogged { get; }

        UsersTokens UserToken { get; }

        //event Action UpdateScreen;
        Users UserLogged { get; }

        public string Token { get; }
    }
}
