using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
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
using System.IO;
using System.Threading.Tasks;
using ZXing;
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
        private readonly IBorrowerProfileService _borrowerProfileService;

        public AccountController(
                            SignInManager signInManager,
                            IHttpContextAccessor httpContextAccessor,
                            ILoggerFactory loggerFactory,
                            IConfiguration configuration,
                            IMapper mapper,
                            IAccountService accountService,
                            IPasswordResetService passwordResetService,
                            IBorrowerProfileService borrowerProfileService,
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
            this._borrowerProfileService = borrowerProfileService;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string returnUrl) // Reviewed : Xyan
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
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl) // Reviewed : Xyan, Refactored : Xyan
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)    
                return View(model);

            var loginResult = _accountService.AuthenticateAccount(model.UserId, model.Password);

            if (loginResult.Result != LoginResult.Success)
            {
                var message = loginResult.FailureReason switch
                {
                    LoginFailureReason.PendingApproval => "Your registration is awaiting admin approval.",
                    LoginFailureReason.Inactive => "Your account is inactive. Contact the administrator.",
                    LoginFailureReason.Suspended => "Your account access is suspended. Contact the administrator.",
                    LoginFailureReason.Disabled => "Your account is disabled. Contact the administrator.",
                    LoginFailureReason.TemporarilyLocked => "Your account is temporarily locked. Please try again later.",
                    LoginFailureReason.InvalidCredentials => "Incorrect email or password.",
                    _ => "Unable to sign in. Please try again."
                };

                ModelState.AddModelError(string.Empty, message);
                return View(model);
            }

            await _signInManager.SignInAsync(loginResult.Account, model.RememberMe);
            await _accountService.RecordSuccessfulLoginAsync(loginResult.Account.AccountId);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) // Checks if returnUrl [is empty] and [is local]
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction("Index", "Dashboard");
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
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            var model = new BorrowerRegistrationViewModel();
            LoadRegistrationDepartments(model);
            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(3 * 1024 * 1024)]
        public async Task<IActionResult> Register(BorrowerRegistrationViewModel model, IFormFile profilePhoto)
        {
            if (profilePhoto != null && profilePhoto.Length > ASI.Basecode.Resources.Constants.ProfilePhotoPolicy.MaxBytes)
                ModelState.AddModelError("ProfilePhoto", "Profile picture must be 2 MB or smaller.");
            if (ModelState.IsValid)
            {
                try
                {
                    using var stream = profilePhoto?.OpenReadStream();
                    var emailSent = await _borrowerProfileService.RegisterAsync(model, stream, profilePhoto?.FileName, HttpContext.RequestAborted);
                    TempData["RegistrationSubmitted"] = true;
                    if (!emailSent) TempData["RegistrationEmailWarning"] = "Your request was saved, but the confirmation email could not be sent. Please contact your administrator if you need an update.";
                    return RedirectToAction(nameof(AwaitingApproval));
                }
                catch (System.ComponentModel.DataAnnotations.ValidationException exception)
                {
                    ModelState.AddModelError(string.Empty, exception.Message);
                }
                catch (Exception exception) when (!HttpContext.RequestAborted.IsCancellationRequested)
                {
                    _logger.LogError(exception, "Unable to complete borrower registration.");
                    ModelState.AddModelError(string.Empty, "Unable to submit your registration. Please try again.");
                }
            }
            if (profilePhoto != null) ViewData["PhotoReselectMessage"] = "Please select your profile picture again after correcting the errors.";
            ModelState.SetModelValue(nameof(model.Password), string.Empty, string.Empty);
            ModelState.SetModelValue(nameof(model.ConfirmPassword), string.Empty, string.Empty);
            model.Password = null;
            model.ConfirmPassword = null;
            LoadRegistrationDepartments(model);
            return View(model);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> MyProfile()
        {
            var userIdClaim = User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out var userId))
                return Challenge();

            var model = await _accountService.GetProfileAsync(userId, HttpContext.RequestAborted);

            if (model == null)
                return NotFound();

            return View(model);
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult AwaitingApproval()
        {
            if (TempData["RegistrationSubmitted"] is not true) return RedirectToAction(nameof(Register));
            return View();
        }
        private void LoadRegistrationDepartments(BorrowerRegistrationViewModel model)
        {
            try { model.Departments = _borrowerProfileService.GetDepartments(); }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Unable to load registration departments.");
                model.DepartmentError = "Departments could not be loaded. Please refresh and try again.";
            }
        }
        [AllowAnonymous]
        public async Task<IActionResult> SignOutUser()
        {
            await this._signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }
    }
}

