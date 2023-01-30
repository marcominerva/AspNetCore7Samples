using AspNetCore7Samples.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace AspNetCore7Samples.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PeopleController : ControllerBase
{
    private static readonly List<Person> people = new();

    [HttpGet]
    [OutputCache(PolicyName = "People")]
    public IActionResult Get() => Ok(people);

    [HttpPost]
    public async Task<IActionResult> Save(Person person, IOutputCacheStore cacheStore)
    {
        people.Add(person);

        // After adding a new person, invalidates the cache so a subsequent call to Get endpoint will get fresh data.
        await cacheStore.EvictByTagAsync("People", CancellationToken.None);

        return NoContent();
    }
}
