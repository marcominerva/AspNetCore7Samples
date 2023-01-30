using Microsoft.AspNetCore.Mvc;

namespace AspNetCore7Samples.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ErrorsController : ControllerBase
{
    [HttpGet("notfound")]
    public IActionResult GetNotFound() => NotFound();

    [HttpGet("{code:int}")]
    public IActionResult GetStatusCode(int code) => StatusCode(code);

    [HttpGet("exception")]
    public IActionResult GetError() => throw new Exception("Incredible error");
}
