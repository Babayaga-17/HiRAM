using ASI.Basecode.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;

namespace ASI.Basecode.WebApp.Controllers;

[Authorize]
public class CategoryController : Controller
{
    public IActionResult Index() => View(Array.Empty<CategoryViewModel>());

    public IActionResult Create() => View();

    public IActionResult Edit(int id)
    {
        // No category can be edited until the list is connected to stored data.
        return NotFound();
    }
}
