using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Exceptions;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Authorization;
using ASI.Basecode.WebApp.Models;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.WebApp.Controllers;

/// <summary>
/// WBS 5.1 User and Role Management (Figma: "Administrator / Users &amp; Roles").
/// Admin only, enforced on the server through the MenuAccess policy for every action,
/// including direct URLs and posted forms.
/// </summary>
[Authorize(Policy = MenuAccess.UserManagementPolicy)]
public class UserAccountController : ControllerBase<UserAccountController>
{
    private readonly IUserAccountService _userAccountService;

    public UserAccountController(
                            IHttpContextAccessor httpContextAccessor,
                            ILoggerFactory loggerFactory,
                            IConfiguration configuration,
                            IUserAccountService userAccountService,
                            IMapper mapper = null) : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        this._userAccountService = userAccountService;
    }

    // 5.12 View user accounts
    [HttpGet]
    public IActionResult Index()
    {
        try
        {
            ViewData["Roles"] = _userAccountService.GetRoles();
            return View(_userAccountService.GetUserAccounts());
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load user accounts.");
            TempData["UserAccountLoadError"] = "Unable to load user accounts. Please try again.";
            ViewData["Roles"] = new List<RoleListItem>();
            return View(new List<UserAccountListItem>());
        }
    }

    // 5.12 View user account
    [HttpGet]
    public IActionResult Details(int id)
    {
        try
        {
            var userAccount = _userAccountService.GetUserAccountById(id);

            if (userAccount == null)
                return NotFound();

            ViewData["IsSelf"] = userAccount.UserId == CurrentUserId;
            return View(userAccount);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load user account {UserId}.", id);
            return StatusCode(500, "Unable to load the user account. Please try again.");
        }
    }

    // 5.11 Create user account + 5.14 assign role
    [HttpGet]
    public IActionResult Create()
    {
        return FormView(new UserAccountFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(UserAccountFormViewModel model)
    {
        model.UserId = 0;

        if (string.IsNullOrEmpty(model.Password))
            ModelState.AddModelError(nameof(UserAccountFormViewModel.Password), "Password is required.");

        if (!ModelState.IsValid)
            return FormView(model);

        try
        {
            var userId = _userAccountService.AddUserAccount(model, CurrentUserId, UserId, ClientIp);
            TempData["UserAccountSuccess"] = "The user account was created.";

            return RedirectToAction(nameof(Details), new { id = userId });
        }
        catch (UserAccountValidationException exception)
        {
            foreach (var error in exception.Errors)
                ModelState.AddModelError(error.Key, error.Value);

            return FormView(model);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to create a user account.");
            ModelState.AddModelError(string.Empty, "Unable to save the user account. Please try again.");

            return FormView(model);
        }
    }

    // 5.13 Edit user account + 5.15 change role
    [HttpGet]
    public IActionResult Edit(int id)
    {
        try
        {
            var userAccount = _userAccountService.GetUserAccountById(id);

            if (userAccount == null)
                return NotFound();

            return FormView(new UserAccountFormViewModel(userAccount));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load user account {UserId}.", id);
            return StatusCode(500, "Unable to load the user account. Please try again.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(UserAccountFormViewModel model)
    {
        // Password fields are not part of the edit form.
        ModelState.Remove(nameof(UserAccountFormViewModel.Password));
        ModelState.Remove(nameof(UserAccountFormViewModel.ConfirmPassword));

        if (model.UserId <= 0)
            return NotFound();

        if (!ModelState.IsValid)
            return FormView(model);

        try
        {
            if (!_userAccountService.EditUserAccount(model, CurrentUserId, UserId, ClientIp))
                return NotFound();

            TempData["UserAccountSuccess"] = "The user account was updated.";

            return RedirectToAction(nameof(Details), new { id = model.UserId });
        }
        catch (UserAccountValidationException exception)
        {
            foreach (var error in exception.Errors)
                ModelState.AddModelError(error.Key, error.Value);

            return FormView(model);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to update user account {UserId}.", model.UserId);
            ModelState.AddModelError(string.Empty, "Unable to save the changes. Please try again.");

            return FormView(model);
        }
    }

    // 5.16 Activate user account
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Activate(int id, string version) => ChangeStatus(id, version, true);

    // 5.17 Deactivate user account
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Deactivate(int id, string version) => ChangeStatus(id, version, false);

    // 5.18 Reset user account access
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ResetAccess(int id)
    {
        try
        {
            var userAccount = _userAccountService.GetUserAccountById(id);
            if (userAccount == null)
            {
                TempData["UserAccountError"] = "The user account no longer exists.";
                return RedirectToAction(nameof(Index));
            }

            var temporaryPassword = _userAccountService.ResetUserAccountAccess(id, CurrentUserId, UserId, ClientIp);
            if (temporaryPassword == null)
            {
                TempData["UserAccountError"] = "The user account no longer exists.";
                return RedirectToAction(nameof(Index));
            }

            // The temporary password is rendered once in this response only: not redirected, cached, or stored in TempData.
            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            ViewData["TemporaryPassword"] = temporaryPassword;

            return View(userAccount);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to reset access for user account {UserId}.", id);
            TempData["UserAccountError"] = "Unable to reset account access. Please try again.";

            return RedirectToAction(nameof(Details), new { id });
        }
    }

    // 5.19 Manage menu/function access (role-based)
    [HttpGet]
    public IActionResult RoleAccess()
    {
        try
        {
            return View(_userAccountService.GetRoles());
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load roles.");
            TempData["UserAccountLoadError"] = "Unable to load roles. Please try again.";

            return View(new List<RoleListItem>());
        }
    }

    private IActionResult ChangeStatus(int id, string version, bool activate)
    {
        try
        {
            if (_userAccountService.SetUserAccountStatus(id, activate, version, CurrentUserId, UserId, ClientIp))
            {
                TempData["UserAccountSuccess"] = activate ? "The user account was activated." : "The user account was deactivated.";
            }
            else
            {
                TempData["UserAccountError"] = "The user account no longer exists.";
                return RedirectToAction(nameof(Index));
            }
        }
        catch (UserAccountValidationException exception)
        {
            TempData["UserAccountError"] = string.Join(" ", exception.Errors.Values);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to change the status of user account {UserId}.", id);
            TempData["UserAccountError"] = "Unable to change the account status. Please try again.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private IActionResult FormView(UserAccountFormViewModel model)
    {
        try
        {
            // Active roles, plus the user's current role even if it was deactivated later.
            ViewData["Roles"] = _userAccountService.GetRoles()
                .Where(role => role.IsActive || role.RoleId == model.RoleId)
                .ToList();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load roles.");
            ViewData["Roles"] = new List<RoleListItem>();
            ModelState.AddModelError(string.Empty, "Unable to load roles. Please try again.");
        }

        ViewData["IsSelf"] = model.IsEdit && model.UserId == CurrentUserId;
        return View(model.IsEdit ? "Edit" : "Create", model);
    }

    private int CurrentUserId => int.TryParse(UserId, out var id) ? id : 0;

    private string ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();
}
