using Application.Features.Documents.ProcessDocument;
using Application.Features.Documents.SearchDocuments;
using MediatR;
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces(MediaTypeNames.Application.Json)]
    public class DocumentsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<DocumentsController> _logger;

        public DocumentsController(IMediator mediator, ILogger<DocumentsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        /// <summary>
        /// Process a PDF document and generate embeddings
        /// </summary>
        /// <param name="file">The PDF file to process</param>
        /// <returns>The processed document ID</returns>
        [HttpPost("process")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ProcessDocumentResult>> ProcessDocument(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file was uploaded.");
            }

            if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Only PDF files are supported.");
            }

            try
            {
                // Save the file to a temporary location
                var tempPath = Path.GetTempFileName() + ".pdf";
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Process the document
                var result = await _mediator.Send(new ProcessDocumentCommand(tempPath));

                // Clean up the temporary file
                try
                {
                    System.IO.File.Delete(tempPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary file: {Path}", tempPath);
                }

                if (!result.Success)
                {
                    return BadRequest(result.Error);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document");
                return StatusCode(500, "An error occurred while processing the document.");
            }
        }

        /// <summary>
        /// Search through processed documents using semantic search
        /// </summary>
        /// <param name="query">The search query</param>
        /// <returns>A list of relevant document chunks</returns>
        [HttpGet("search")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SearchDocumentsResult>> SearchDocuments([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Search query cannot be empty.");
            }

            try
            {
                var result = await _mediator.Send(new SearchDocumentsQuery(query));
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching documents");
                return StatusCode(500, "An error occurred while searching documents.");
            }
        }
    }
}
