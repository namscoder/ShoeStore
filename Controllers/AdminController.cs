using Microsoft.AspNetCore.Mvc;

namespace ShoeStore.Controllers
{
    public class AdminController : Controller
    {
        [Route("Admin")]
        public IActionResult Index()
        {
            return View();
        }
    }
}