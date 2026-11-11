using ASI.Basecode.Resources.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.ServiceModels.Reservation;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ASI.Basecode.WebApp.Controllers;

[Authorize(Roles = RoleNames.Borrower)]
public class EquipmentReservationController : ControllerBase<EquipmentReservationController>
{
    private const string SelectionKey = "SelectedEquipmentIds";

    private readonly IEquipmentService _equipmentService;
    private readonly IEquipmentReservationService _equipmentReservationService;
    private readonly IReservationService _reservationService;

    public EquipmentReservationController(
                            IHttpContextAccessor httpContextAccessor, 
                            ILoggerFactory loggerFactory, 
                            IConfiguration configuration,
                            IEquipmentService equipmentService,
                            IEquipmentReservationService equipmentReservationService,
                            IReservationService reservationService,
                            IMapper mapper = null) : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        _equipmentService = equipmentService;
        _reservationService = reservationService;
        _equipmentReservationService = equipmentReservationService;
    }

    // Static samples for the details UI; no reservation records are loaded.
    [HttpGet]
    public IActionResult ReservationDetails(int id, string returnUrl = null)
    {
        returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        ViewData["ReservationReturnUrl"] = returnUrl;
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var borrowerUserId))
            return Challenge();

        var reservation = _reservationService.GetReservationDetails(id, borrowerUserId);

        if (reservation == null)
            return NotFound();

        return View(reservation);
    }

    [HttpGet]
    public IActionResult Cancel(int id, string returnUrl = null)
    {
        returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        ViewData["ReservationReturnUrl"] = returnUrl;
        if (!int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var borrowerUserId))
            return Challenge();

        var reservation =
            _reservationService.GetReservationDetails(id, borrowerUserId);

        if (reservation == null)
            return NotFound();

        if (reservation.Status != ReservationStatuses.Pending)
        {
            TempData["ReservationError"] =
                "Only pending reservations can be cancelled.";

            return RedirectToAction(nameof(ReservationDetails), new { id, returnUrl });
        }

        ViewData["Reservation"] = reservation;

        return View(new ReservationCancelFormModel
        {
            RowVersion = Convert.ToBase64String(reservation.RowVersion)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Cancel(int id, [Bind("Reason,RowVersion")] ReservationCancelFormModel model, string returnUrl = null)
    {
        returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        ViewData["ReservationReturnUrl"] = returnUrl;
        if (!int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var borrowerUserId))
            return Challenge();

        var reservation =
            _reservationService.GetReservationDetails(id, borrowerUserId);

        if (reservation == null)
            return NotFound();

        if (reservation.Status != ReservationStatuses.Pending)
        {
            TempData["ReservationError"] =
                "Only pending reservations can be cancelled.";

            return RedirectToAction(nameof(ReservationDetails), new { id, returnUrl });
        }

        if (ModelState.IsValid)
        {
            try
            {
                var error =
                    _equipmentReservationService.Cancel(id, model, borrowerUserId);

                if (error == null)
                {
                    TempData["ReservationSuccess"] =
                        "Reservation cancelled.";

                    return RedirectToAction(nameof(ReservationDetails), new { id, returnUrl });
                }

                ModelState.AddModelError(string.Empty, error);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception,
                    "Unable to cancel reservation {ReservationId}.", id);

                ModelState.AddModelError(string.Empty,
                    "Unable to cancel your reservation. Reload it and try again.");
            }
        }

        ViewData["Reservation"] = reservation;
        return View(model);
    }

    private void LoadEquipment(ReservationFormModel model)
    {
        model.Equipment = new List<EquipmentListItem>();
        foreach (var id in (model.EquipmentIds ?? new List<int>()).Distinct())
        {
            var item = _equipmentService.GetBorrowableEquipmentById(id);
            if (item == null || item.Status == EquipmentAvailability.Unavailable)
            {
                ViewData["SelectionError"] = "Some selected equipment can no longer be borrowed. Return to Browse Equipment and review your selection.";
                continue;
            }
            model.Equipment.Add(item);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ChangeEquipment(int? id, [Bind("EquipmentIds,Pickup,ExpectedReturn,Purpose,RowVersion")] ReservationEditFormModel model)
    {
        if (!int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var borrowerUserId))
            return Challenge();

        if (id.HasValue)
        {
            var reservation = _reservationService
                .GetReservationDetails(id.Value, borrowerUserId);

            if (reservation == null)
                return NotFound();

            if (reservation.Status != ReservationStatuses.Pending)
            {
                TempData["ReservationError"] =
                    "Only pending reservations can be edited.";

                return RedirectToAction(
                    nameof(ReservationDetails), new { id = id.Value });
            }

            HttpContext.Session.SetString(
                $"ReservationDraft:Edit:{id.Value}",
                JsonSerializer.Serialize(model));

            HttpContext.Session.SetString(
                $"SelectedEquipmentIds:Edit:{id.Value}",
                string.Join(",", model.EquipmentIds));
        }
        else
        {
            HttpContext.Session.SetString("ReservationDraft:Create", JsonSerializer.Serialize<ReservationFormModel>(model));
            HttpContext.Session.SetString(SelectionKey, string.Join(",", model.EquipmentIds));
        }

        return RedirectToAction("Index", "BrowseEquipment", new { reservationId = id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DiscardEdit(int id, string returnUrl = null)
    {
        returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        ViewData["ReservationReturnUrl"] = returnUrl;
        if (!int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var borrowerUserId))
            return Challenge();

        var reservation =
            _reservationService.GetReservationDetails(id, borrowerUserId);

        if (reservation == null)
            return NotFound();

        HttpContext.Session.Remove($"ReservationDraft:Edit:{id}");
        HttpContext.Session.Remove($"SelectedEquipmentIds:Edit:{id}");

        return RedirectToAction(nameof(ReservationDetails), new { id, returnUrl });
    }

    private static List<int> ParseSelection(string value)
    {
        return (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }
}
