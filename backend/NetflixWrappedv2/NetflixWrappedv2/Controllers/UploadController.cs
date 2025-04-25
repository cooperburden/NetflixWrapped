using Microsoft.AspNetCore.Mvc;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Text.RegularExpressions;

public class EnrichedTitle
{
    public string Title { get; set; }
    public bool Found { get; set; }
    public string Media_Type { get; set; }
    public int? Runtime { get; set; }
    public List<string> Genres { get; set; }
    public List<string> Directors { get; set; }
    public List<string> Actors { get; set; }
    public string Poster { get; set; }
}

namespace NetflixWrappedv2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UploadController : ControllerBase
    {
        public class NetflixWatchEntry
        {
            public string Title { get; set; }
            public DateTime Date { get; set; }
        }

        private string CleanTitle(string title)
        {
            return Regex.Split(title, @": Season \d+: Episode \d+|: Season \d+|: Episode \d+")[0].Trim();
        }

        [HttpPost("csv")]
        public async Task<IActionResult> UploadCsv(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var entries = new List<NetflixWatchEntry>();

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                PrepareHeaderForMatch = args => args.Header.ToLower(),
                MissingFieldFound = null,
                HeaderValidated = null,
                Delimiter = ","
            };

            using (var stream = new StreamReader(file.OpenReadStream()))
            using (var csv = new CsvReader(stream, config))
            {
                try
                {
                    csv.Context.TypeConverterOptionsCache.GetOptions<DateTime>().Formats = new[] { "M/d/yy", "MM/dd/yy" };
                    entries = csv.GetRecords<NetflixWatchEntry>().ToList();
                }
                catch (Exception ex)
                {
                    return BadRequest("CSV parsing error: " + ex.Message);
                }
            }

            var cleanedTitles = entries
                .Where(e => !string.IsNullOrEmpty(e.Title))
                .Select(e => CleanTitle(e.Title))
                .Distinct()
                .ToList();
            
            int chunkSize = int.TryParse(Request.Form["chunkSize"], out var sizeVal) ? sizeVal : 25;
            int offset = int.TryParse(Request.Form["offset"], out var offsetVal) ? offsetVal : 0;

            var titlesToEnrich = cleanedTitles
                .Skip(offset)
                .Take(chunkSize)
                .ToList();




            Console.WriteLine("📤 Sending titles to enrichment API...");

            List<EnrichedTitle> enrichedResults = new();

            using (var httpClient = new HttpClient())
            {
                var payload = new { titles = titlesToEnrich };
                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                try
                {
                    var response = await httpClient.PostAsync("http://127.0.0.1:5001/enrich", content);
                    response.EnsureSuccessStatusCode();

                    var responseStream = await response.Content.ReadAsStreamAsync();
                    enrichedResults = await System.Text.Json.JsonSerializer.DeserializeAsync<List<EnrichedTitle>>(responseStream, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Error calling enrichment API: {ex.Message}");
                }
            }

            return Ok(new
            {
                Message = "Enrichment successful",
                TotalTitles = cleanedTitles.Count,
                Enriched = enrichedResults.Take(5)
            });
        }

        [HttpGet("test-enrich")]
        public async Task<IActionResult> TestEnrichment()
        {
            var payload = new { titles = new List<string> { "Inception", "Breaking Bad" } };
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            using var httpClient = new HttpClient();
            try
            {
                var response = await httpClient.PostAsync("http://127.0.0.1:5001/enrich", content);
                response.EnsureSuccessStatusCode();

                var responseStream = await response.Content.ReadAsStreamAsync();
                var enriched = await System.Text.Json.JsonSerializer.DeserializeAsync<List<EnrichedTitle>>(responseStream, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return Ok(enriched);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Enrichment call failed: {ex.Message}");
            }
        }
    }
}
