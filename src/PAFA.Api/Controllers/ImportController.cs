using MediatR;
using Microsoft.AspNetCore.Mvc;
using PAFA.Extraction.Commands.Import;

namespace PAFA.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImportController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ImportController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] int periodYear, [FromForm] int periodMonth)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Le fichier est vide ou invalide.");

            // On crée la commande en y passant le fichier
            var command = new UploadParrFilesCommand(file, periodYear, periodMonth, "User_POC");

            // On envoie la commande au Handler via MediatR
            var result = await _mediator.Send(command);

            if (!result.Success)
                return BadRequest(result.ErrorMessage);

            return Accepted(result);
        }
    }
}