using MediatR;
using Microsoft.AspNetCore.Mvc;
using PAFA.Extraction.Commands.Import;

namespace PAFA.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImportController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator _mediator = mediator;

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] int periodYear, [FromForm] int periodMonth)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Le fichier est vide ou invalide.");

            // 1. On extrait les octets du fichier Web
            byte[] fileBytes;
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                fileBytes = memoryStream.ToArray();
            }

            // 2. On crée la commande 
            var command = new UploadParrFilesCommand(
                file.FileName,
                fileBytes,
                periodYear,
                periodMonth,
                "User_POC" 
            );

            // 3. On l'envoie à la couche métier
            var result = await _mediator.Send(command);

            if (!result.Success)
                return BadRequest(result.ErrorMessage);

            return Accepted(result);
        }
    }
}