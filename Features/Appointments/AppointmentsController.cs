using Microsoft.AspNetCore.Mvc;

namespace Vyracare.Api.Appointments.Features.Appointments;

[ApiController]
[Route("api/appointments")]
public sealed class AppointmentsController : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(
        string id,
        [FromServices] AppointmentService service,
        CancellationToken cancellationToken)
    {
        var appointment = await service.GetByIdAsync(id, cancellationToken);
        return appointment is null ? NotFound(new { message = "Agendamento nao encontrado." }) : Ok(appointment);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromServices] AppointmentService service,
        CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(from, to, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateAppointmentRequest request,
        [FromServices] AppointmentService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(
        string id,
        [FromBody] UpdateAppointmentStatusRequest request,
        [FromServices] AppointmentService service,
        CancellationToken cancellationToken)
    {
        var appointment = await service.UpdateStatusAsync(id, request, cancellationToken);
        return appointment is null ? NotFound(new { message = "Agendamento nao encontrado." }) : Ok(appointment);
    }

    [HttpGet("dashboard/summary")]
    public async Task<IActionResult> DashboardSummary(
        [FromQuery] DateOnly? referenceDate,
        [FromServices] AppointmentService service,
        CancellationToken cancellationToken) =>
        Ok(await service.GetDashboardSummaryAsync(referenceDate, cancellationToken));
}
