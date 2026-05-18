using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Books;
using Readarr.Http;

namespace Readarr.Api.V1.Narrator
{
    [V1ApiController]
    public class NarratorController : Controller
    {
        private readonly INarratorService _narratorService;

        public NarratorController(INarratorService narratorService)
        {
            _narratorService = narratorService;
        }

        [HttpGet("{id:int}")]
        public ActionResult<NarratorResource> GetById(int id)
        {
            var model = _narratorService.GetById(id);
            if (model == null)
            {
                return NotFound();
            }

            return model.ToResource();
        }

        [HttpGet]
        public List<NarratorResource> GetByEdition([FromQuery] int editionId)
        {
            return _narratorService.GetNarratorsForEdition(editionId).ToResource();
        }
    }
}
