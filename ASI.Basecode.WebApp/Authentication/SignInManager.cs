using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.WebApp.Extensions.Configuration;
using ASI.Basecode.WebApp.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using static ASI.Basecode.Resources.Constants.Enums;

namespace ASI.Basecode.WebApp.Authentication
{
    /// <summary>
    /// SignInManager
    /// </summary>
    public class SignInManager
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Gets or sets the account.
        /// </summary>
        public LoginAccount account { get; set; }

        /// <summary>
        /// Initializes a new instance of the SignInManager class.
        /// </summary>
        public SignInManager()
        {
        }

        /// <summary>
        /// Initializes a new instance of the SignInManager class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="accountService">The account service.</param>
        /// <param name="httpContextAccessor">The HTTP context accessor.</param>
        public SignInManager(IConfiguration configuration,
                             IHttpContextAccessor httpContextAccessor)
        {
            this._configuration = configuration;
            this._httpContextAccessor = httpContextAccessor;
            account = new LoginAccount();
        }

        /// <summary>
        /// Gets the claims identity.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <returns>The successfully completed task</returns>
        public Task<ClaimsIdentity> GetClaimsIdentity(string username, string password)
        {
            ClaimsIdentity claimsIdentity = null;
            Account accountData = new();

            account.loginResult = LoginResult.Success;//TODO this._accountService.AuthenticateUser(username, password, ref userData);

            if (account.loginResult == LoginResult.Failed)
            {
                return Task.FromResult<ClaimsIdentity>(null);
            }

            account.accountData = accountData;
            claimsIdentity = CreateClaimsIdentity(accountData);
            return Task.FromResult(claimsIdentity);
        }

        /// <summary>
        /// Creates the claims identity.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <returns>Instance of ClaimsIdentity</returns>
        public ClaimsIdentity CreateClaimsIdentity(Account account)
        {
            if (account == null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            if (account.User == null)
            {
                throw new InvalidOperationException(
                    "The account's related user was not loaded.");
            }

            if (account.Role == null)
            {
                throw new InvalidOperationException(
                    "The account's related role was not loaded.");
            }

            var fullName = string.Join(" ", new[] { account.User.FirstName, account.User.MiddleName, account.User.LastName, account.User.Suffix }.Where(part => !string.IsNullOrWhiteSpace(part)));

            if (string.IsNullOrWhiteSpace(fullName))
            {
                fullName = account.User.UserCode ?? account.Email ?? account.UserId.ToString();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, account.UserId.ToString(), ClaimValueTypes.String, Const.Issuer),
                new Claim(ClaimTypes.Name, fullName, ClaimValueTypes.String, Const.Issuer),
                new Claim(ClaimTypes.Email, account.Email ?? string.Empty, ClaimValueTypes.String, Const.Issuer),
                new Claim(ClaimTypes.Role, account.Role.RoleName, ClaimValueTypes.String, Const.Issuer),

                new Claim("AccountId", account.AccountId.ToString(), ClaimValueTypes.String, Const.Issuer),
                new Claim("UserId", account.UserId.ToString(), ClaimValueTypes.String, Const.Issuer),
                new Claim("UserCode", account.User.UserCode ?? string.Empty, ClaimValueTypes.String, Const.Issuer)
            };

            return new ClaimsIdentity(claims, Const.AuthenticationScheme);
        }

        /// <summary>
        /// Creates the claims principal.
        /// </summary>
        /// <param name="identity">The identity.</param>
        /// <returns>Created claims principal</returns>
        public IPrincipal CreateClaimsPrincipal(ClaimsIdentity identity)
        {
            var identities = new List<ClaimsIdentity>();
            identities.Add(identity);
            return this.CreateClaimsPrincipal(identities);
        }

        /// <summary>
        /// Creates the claims principal.
        /// </summary>
        /// <param name="identities">The identities.</param>
        /// <returns>Created claims principal</returns>
        public IPrincipal CreateClaimsPrincipal(IEnumerable<ClaimsIdentity> identities)
        {
            var principal = new ClaimsPrincipal(identities);
            return principal;
        }

        /// <summary>
        /// Signs in user asynchronously
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="isPersistent">if set to <c>true</c> [is persistent].</param>
        public async Task SignInAsync(Account account, bool isPersistent = false)
        {
            var claimsIdentity = this.CreateClaimsIdentity(account);
            var principal = this.CreateClaimsPrincipal(claimsIdentity);
            await this.SignInAsync(principal, isPersistent);
        }

        /// <summary>
        /// Signs in user asynchronously
        /// </summary>
        /// <param name="principal">The principal.</param>
        /// <param name="isPersistent">if set to <c>true</c> [is persistent].</param>
        public async Task SignInAsync(IPrincipal principal, bool isPersistent = false)
        {
            var token = _configuration.GetTokenAuthentication();
            await _httpContextAccessor
                .HttpContext
                .SignInAsync(
                            Const.AuthenticationScheme,
                            (ClaimsPrincipal)principal,
                            new AuthenticationProperties
                            {
                                ExpiresUtc = DateTime.UtcNow.AddMinutes(token.ExpirationMinutes),
                                IsPersistent = isPersistent,
                                AllowRefresh = false
                            });
        }

        /// <summary>
        /// Signs out user asynchronously
        /// </summary>
        public async Task SignOutAsync()
        {
            var token = _configuration.GetTokenAuthentication();
            await _httpContextAccessor.HttpContext.SignOutAsync(Const.AuthenticationScheme);
        }
    }
}
