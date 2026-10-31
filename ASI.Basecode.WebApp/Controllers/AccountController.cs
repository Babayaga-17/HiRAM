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
        private const string ResetEmailKey = "ResetEmail";
        private const string ResetTokenKey = "ResetTokenId";

        private readonly SessionManager _sessionManager;
        private readonly SignInManager _signInManager;
        private readonly TokenValidationParametersFactory _tokenValidationParametersFactory;
        private readonly TokenProviderOptionsFactory _tokenProviderOptionsFactory;
        private readonly IConfiguration _appConfiguration;
        private readonly IAccountService _accountService;
        private readonly IPasswordResetService _passwordResetService;

        public AccountController(
                            SignInManager signInManager,
                            IHttpContextAccessor httpContextAccessor,
                            ILoggerFactory loggerFactory,
                            IConfiguration configuration,
                            IMapper mapper,
                            IAccountService accountService,
                            IPasswordResetService passwordResetService,
                            TokenValidationParametersFactory tokenValidationParametersFactory,
                            TokenProviderOptionsFactory tokenProviderOptionsFactory) : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            this._sessionManager = new SessionManager(this._session);
            this._signInManager = signInManager;
            this._tokenProviderOptionsFactory = tokenProviderOptionsFactory;
            this._tokenValidationParametersFactory = tokenValidationParametersFactory;
            this._appConfiguration = configuration;
            this._accountService = accountService;
            this._passwordResetService = passwordResetService;
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return LocalRedirect(returnUrl);
                }

                return RedirectToAction("Index", "Dashboard");
            }

            ViewData["ReturnUrl"] = returnUrl;

            this._sessionManager.Clear();
            this._session.SetString("SessionId", Guid.NewGuid().ToString());

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl)
        {
            ViewData["ReturnUrl"] = returnUrl;

            Account account = null;

            var loginResult = _accountService.AuthenticateAccount(model.UserId, model.Password, ref account);

            if (loginResult == LoginResult.Success)
            {
                await _signInManager.SignInAsync(account, model.RememberMe);

                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return LocalRedirect(returnUrl);
                }

                return RedirectToAction("Index", "Dashboard");
            }

            ModelState.AddModelError(string.Empty, "Incorrect email or password.");

            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            var result = await _passwordResetService.SendResetCodeAsync(email);

            if (result == SendResetCodeStatus.Cooldown)
            {
                TempData["ResendError"] =
                    "A code was requested recently. Please wait before requesting another.";

                return RedirectToAction("VerifyCode", new { email });
            }

            if (result != SendResetCodeStatus.Sent)
            {
                ViewBag.Error = result == SendResetCodeStatus.AccountNotFound
                    ? "Account cannot be found."
                    : "Unable to send a code. Please try again.";

                return View();
            }
            return RedirectToAction("VerifyCode", new { email });
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult VerifyCode(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return RedirectToAction(nameof(ForgotPassword));

            email = email.Trim();

            ViewBag.Email = email;

            var timing = _passwordResetService.GetTiming(email);

            return View(timing);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult VerifyCode(string email, string code)
        {
            var tokenId = _passwordResetService.VerifyCode(email, code);
            if (tokenId == null)
            {
                ViewBag.Email = email?.Trim();
                ViewBag.Error = "Invalid or expired code.";

                var timing = _passwordResetService.GetTiming(email);

                return View(timing);
            }

            _session.SetString(ResetEmailKey, email.Trim());
            _session.SetString(ResetTokenKey, tokenId.Value.ToString());
            return RedirectToAction("NewPassword");
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendCode(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return RedirectToAction(nameof(ForgotPassword));

            email = email.Trim();

            // Starting another request must not retain prior reset authorization.
            _session.Remove(ResetEmailKey);
            _session.Remove(ResetTokenKey);

            var result = await _passwordResetService.SendResetCodeAsync(email);

            switch (result)
            {
                case SendResetCodeStatus.Sent:
                    TempData["ResendSuccess"] =
                        "A new code has been sent. Use the latest code.";
                    break;

                case SendResetCodeStatus.Cooldown:
                    TempData["ResendError"] =
                        "Please wait until resending is available.";
                    break;

                default:
                    TempData["ResendError"] =
                        "Unable to send a new code. Please try again.";
                    break;
            }

            return RedirectToAction(nameof(VerifyCode), new { email });
        }

        // Step 2: choose the new password
        [HttpGet]
        [AllowAnonymous]
        public IActionResult NewPassword()
        {
            if (string.IsNullOrEmpty(_session.GetString(ResetEmailKey)))
                return RedirectToAction("ForgotPassword");

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult NewPassword(string newPassword, string confirmPassword)
        {
            var email = _session.GetString(ResetEmailKey);
            if (string.IsNullOrEmpty(email) || !int.TryParse(_session.GetString(ResetTokenKey), out var tokenId))
                return RedirectToAction("ForgotPassword");

            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 8)
            {
                ViewBag.Error = "Password must be at least 8 characters.";
                return View();
            }
            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "Passwords don't match.";
                return View();
            }

            if (!_passwordResetService.SetNewPassword(email, tokenId, newPassword))
            {
                _session.Remove(ResetEmailKey);
                _session.Remove(ResetTokenKey);
                TempData["Error"] = "Your reset session expired. Please request a new code.";
                return RedirectToAction("ForgotPassword");
            }

            _session.Remove(ResetEmailKey);
            _session.Remove(ResetTokenKey);
            TempData["Success"] = "Password updated. Please sign in.";
            return RedirectToAction("Login");
        }

        // Cancel button
        [HttpGet]
        [AllowAnonymous]
        public IActionResult CancelReset()
        {
            _session.Remove(ResetEmailKey);
            _session.Remove(ResetTokenKey);
            return RedirectToAction("Login");
        }

        // -------------------------------------

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
            Debug.WriteLine(PasswordManager.EncryptPassword(model.Password));

            return View();
        }

        [AllowAnonymous]
        public async Task<IActionResult> SignOutUser()
        {
            await this._signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }
    }
}