using Microsoft.AspNetCore.Mvc;

namespace RealTimeEcom.WebApp.Controllers
{
    public class HomeController : Controller
    {
        [HttpGet]
        public IActionResult Index() => View();
    }
}
