// Controllers/ProcessController.cs
using Microsoft.AspNetCore.Mvc;
using Practice4.Models;
using Practice4.Services;

namespace Practice4.Controllers;

[ApiController]
[Route("api/process")]
public class ProcessController : ControllerBase
{
    private readonly ProcessService _processService;

    public ProcessController(ProcessService processService)
    {
        _processService = processService;
    }

    [HttpPost("{processKey}/event")]
    public IActionResult PostEvent(string processKey, [FromBody] ProcessRequest request)
    {
        var response = _processService.ProcessEvent(processKey, request);
        return Ok(response);
    }

    [HttpGet("{processKey}/state")]
    public IActionResult GetState(string processKey)
    {
        var state = _processService.GetState(processKey);
        if (state == null)
            return NotFound();
        return Ok(new { processKey, state });
    }
}