using Juice.Workflows.Designer.Commands;
using Microsoft.AspNetCore.Mvc;

namespace Juice.Workflows.Designer.Controllers
{
    [ApiController]
    [Route("api/workflow-definitions")]
    public class WorkflowDefinitionsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public WorkflowDefinitionsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // GET /api/workflow-definitions?status=Draft
        [HttpGet]
        public async Task<IActionResult> ListAsync([FromQuery] WorkflowDefinitionStatus? status, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new ListWorkflowDefinitionsQuery(status), cancellationToken);
            return Ok(result);
        }

        // GET /api/workflow-definitions/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetAsync(string id, [FromServices] IDefinitionRepository repository, CancellationToken cancellationToken)
        {
            var definition = await repository.GetAsync(id, cancellationToken);
            if (definition == null) return NotFound(Error($"Workflow definition '{id}' not found."));
            return Ok(new
            {
                definition.Id,
                definition.Name,
                definition.RawData,
                definition.RawFormat,
                Status = definition.Status.ToString(),
                definition.ModifiedDate
            });
        }

        // POST /api/workflow-definitions
        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateRequest body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new CreateWorkflowDefinitionCommand(body.Name, body.RawData, body.RawFormat), cancellationToken);
            if (!result.Succeeded)
            {
                return result.Message?.Contains("already exists") == true
                    ? Conflict(Error(result.Message))
                    : UnprocessableEntity(Error(result.Message, result.Exception?.Message));
            }
            return CreatedAtAction(nameof(GetAsync), new { id = result.Data }, new { id = result.Data });
        }

        // PUT /api/workflow-definitions/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAsync(string id, [FromBody] UpdateRequest body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new UpdateWorkflowDefinitionCommand(id, body.RawData, body.RawFormat), cancellationToken);
            if (!result.Succeeded)
            {
                if (result.Message?.Contains("not found") == true) return NotFound(Error(result.Message));
                return UnprocessableEntity(Error(result.Message, result.Exception?.Message));
            }
            return Ok(new { });
        }

        // PATCH /api/workflow-definitions/{id}/name
        [HttpPatch("{id}/name")]
        public async Task<IActionResult> RenameAsync(string id, [FromBody] RenameRequest body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new RenameWorkflowDefinitionCommand(id, body.Name), cancellationToken);
            if (!result.Succeeded)
            {
                if (result.Message?.Contains("not found") == true) return NotFound(Error(result.Message));
                if (result.Message?.Contains("already exists") == true) return Conflict(Error(result.Message));
                return BadRequest(Error(result.Message));
            }
            return Ok(new { });
        }

        // POST /api/workflow-definitions/{id}/publish
        [HttpPost("{id}/publish")]
        public async Task<IActionResult> PublishAsync(string id, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new PublishWorkflowDefinitionCommand(id), cancellationToken);
            if (!result.Succeeded)
            {
                if (result.Message?.Contains("not found") == true) return NotFound(Error(result.Message));
                if (result.Message?.Contains("no execution data") == true) return UnprocessableEntity(Error(result.Message));
                return Conflict(Error(result.Message));
            }
            return Ok(new { });
        }

        // POST /api/workflow-definitions/{id}/archive
        [HttpPost("{id}/archive")]
        public async Task<IActionResult> ArchiveAsync(string id, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new ArchiveWorkflowDefinitionCommand(id), cancellationToken);
            if (!result.Succeeded)
            {
                if (result.Message?.Contains("not found") == true) return NotFound(Error(result.Message));
                return Conflict(Error(result.Message));
            }
            return Ok(new { });
        }

        // DELETE /api/workflow-definitions/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new DeleteWorkflowDefinitionCommand(id), cancellationToken);
            if (!result.Succeeded)
            {
                if (result.Message?.Contains("not found") == true) return NotFound(Error(result.Message));
                return BadRequest(Error(result.Message));
            }
            return NoContent();
        }

        private static object Error(string? message, string? details = null) =>
            details != null
                ? new { error = message, details }
                : (object)new { error = message };

        public record CreateRequest(string Name, string RawData, string RawFormat);
        public record UpdateRequest(string RawData, string RawFormat);
        public record RenameRequest(string Name);
    }
}
