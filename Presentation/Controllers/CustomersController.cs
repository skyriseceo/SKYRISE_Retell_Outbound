using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OSV.Controllers
{

    [Authorize]
    public class CustomersController : Controller // (جمع)
    {
        // الأكشن ده بس اللي هنا
        [HttpGet]
        public IActionResult Index()
        {
            return View(); // هيروح يدور على Views/Customers/Index.cshtml
        }
    }
}
