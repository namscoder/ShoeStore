using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShoeStore.Models;

namespace ShoeStore.Controllers
{
    [Authorize(Roles = AppRoles.Admin)]
    public class AdminController : Controller
    {
        [Route("Admin")]
        public IActionResult Index()
        {
            return View();
        }
    }
}