using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Authentication;
using ASI.Basecode.WebApp.Models;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using static ASI.Basecode.Resources.Constants.Enums;

namespace ASI.Basecode.WebApp.Controllers
{
    public class AccountController : ControllerBase<AccountController>
    {
        private readonly SessionManager _sessionManager;
        private readonly SignInManager _signInManager;
        private readonly TokenValidationParametersFactory _tokenValidationParametersFactory;
        private readonly TokenProviderOptionsFactory _tokenProviderOptionsFactory;
        private readonly IConfiguration _appConfiguration;
        //private readonly IUserService _userService;
        private readonly IAccountService _accountService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccountController"/> class.
        /// </summary>
        /// <param name="signInManager">The sign in manager.</param>
        /// <param name="localizer">The localizer.</param>
        /// <param name="userService">The user service.</param>
        /// <param name="httpContextAccessor">The HTTP context accessor.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="configuration">The configuration.</param>
        /// <param name="mapper">The mapper.</param>
        /// <param name="tokenValidationParametersFactory">The token validation parameters factory.</param>
        /// <param name="tokenProviderOptionsFactory">The token provider options factory.</param>
        public AccountController(
                            SignInManager signInManager,
                            IHttpContextAccessor httpContextAccessor,
                            ILoggerFactory loggerFactory,
                            IConfiguration configuration,
                            IMapper mapper,
                            IAccountService accountService,
                            TokenValidationParametersFactory tokenValidationParametersFactory,
                            TokenProviderOptionsFactory tokenProviderOptionsFactory) : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            this._sessionManager = new SessionManager(this._session);
            this._signInManager = signInManager;
            this._tokenProviderOptionsFactory = tokenProviderOptionsFactory;
            this._tokenValidationParametersFactory = tokenValidationParametersFactory;
            this._appConfiguration = configuration;
            //this._userService = userService;
            this._accountService = accountService;
        }

        /// <summary>
        /// Login Method
        /// </summary>
        /// <returns>Created response view</returns>
        [HttpGet]
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            //TempData["returnUrl"] = System.Net.WebUtility.UrlDecode(HttpContext.Request.Query["ReturnUrl"]);
            //this._sessionManager.Clear();
            //this._session.SetString("SessionId", System.Guid.NewGuid().ToString());
            //return this.View();

            ViewData["ReturnUrl"] = returnUrl;

            this._sessionManager.Clear();
            this._session.SetString("SessionId", Guid.NewGuid().ToString());

            return View();
        }

        /// <summary>
        /// Authenticate user and signs the user in when successful.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="returnUrl">The return URL.</param>
        /// <returns> Created response view </returns>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl)
        {
            //this._session.SetString("HasSession", "Exist");

            //User user = null;

            //User user = new() { Id = 0, UserId = "0", Name = "Name", Password = "Password" };

            //await this._signInManager.SignInAsync(user);
            //this._session.SetString("UserName", model.UserId);

            //return RedirectToAction("Index", "Home");

            //var loginResult = _userService.AuthenticateUser(model.UserId, model.Password, ref user);
            //if (loginResult == LoginResult.Success)
            //{
            //    // 認証OK
            //    await this._signInManager.SignInAsync(user);
            //    this._session.SetString("UserName", user.Name);
            //    return RedirectToAction("Index", "Home");
            //}
            //else
            //{
            //    // 認証NG
            //    TempData["ErrorMessage"] = "Incorrect UserId or Password";
            //    return View();
            //}
            //return View(); 

            //return null;

            ViewData["ReturnUrl"] = returnUrl;

            Account account = null;

            var loginResult = _accountService.AuthenticateAccount(model.UserId, model.Password, ref account);

            if (loginResult == LoginResult.Success)
            {
                await _signInManager.SignInAsync(account);

                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return LocalRedirect(returnUrl);
                }

                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, "Incorrect email or password.");

            return View(model);

        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult Register(UserViewModel model)
        {
            //try
            //{
            //    _userService.AddUser(model);
            //    return RedirectToAction("Login", "Account");
            //}
            //catch (InvalidDataException ex)
            //{
            //    TempData["ErrorMessage"] = ex.Message;
            //}
            //catch (Exception ex)
            //{
            //    TempData["ErrorMessage"] = Resources.Messages.Errors.ServerError;
            //}
            //return View();

            Debug.WriteLine(PasswordManager.EncryptPassword(model.Password));

            return View();
        }

        /// <summary>
        /// Sign Out current account and return login view.
        /// </summary>
        /// <returns>Created response view</returns>
        [AllowAnonymous]
        public async Task<IActionResult> SignOutUser()
        {
            await this._signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }
    }
}
