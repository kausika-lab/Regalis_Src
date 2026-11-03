using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance.Billing;
using Regalis.Shared.Api;

namespace Regalis.API.Controllers;

/// <summary>Importing a bank statement (build doc §8). Parse saves nothing; commit re-reads
/// the same file with the confirmed mapping.</summary>
[Route("api/bank-import")]
[StaffOnly]
public class BankImportController(IBankImportService imports) : ApiControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static BankColumnMappingDto? ReadMapping(string? mapping)
    {
        if (string.IsNullOrWhiteSpace(mapping)) return null;
        try { return JsonSerializer.Deserialize<BankColumnMappingDto>(mapping, Json); }
        catch (JsonException) { throw new BusinessRuleException("The column mapping could not be read."); }
    }

    [HttpGet("accounts/{bankAccountId:int}/column-mapping")]
    [HasPermission("BANK.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<BankColumnMappingDto>), 200)]
    public async Task<IActionResult> GetMapping(int bankAccountId, CancellationToken ct) => OkEnvelope(await imports.GetMappingAsync(bankAccountId, ct));

    /// <summary>Multipart: file, bankAccountId, optional mapping (JSON). Returns headers and a preview.</summary>
    [HttpPost("parse")]
    [HasPermission("BANK.IMPORT")]
    [RequestSizeLimit(12582912L)]
    [ProducesResponseType(typeof(ApiResponse<BankImportPreviewDto>), 200)]
    public async Task<IActionResult> Parse([FromForm] int bankAccountId, [FromForm] string? mapping, IFormFile file, CancellationToken ct)
    {
        await using Stream stream = file.OpenReadStream();
        return OkEnvelope(await imports.ParseAsync(bankAccountId, new BankUploadInput(file.FileName, stream, file.Length), ReadMapping(mapping), ct));
    }

    [HttpPost("commit")]
    [HasPermission("BANK.IMPORT")]
    [RequestSizeLimit(12582912L)]
    [ProducesResponseType(typeof(ApiResponse<BankImportResultDto>), 200)]
    public async Task<IActionResult> Commit([FromForm] int bankAccountId, [FromForm] string mapping, IFormFile file, CancellationToken ct)
    {
        BankColumnMappingDto map = ReadMapping(mapping) ?? throw new BusinessRuleException("Tell us which column holds the date, the description and the amount.");
        await using Stream stream = file.OpenReadStream();
        BankImportResultDto result = await imports.CommitAsync(bankAccountId, new BankUploadInput(file.FileName, stream, file.Length), map, ct);
        return OkEnvelope(result, result.Message);
    }

    [HttpGet("batches")]
    [HasPermission("BANK.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<BankImportBatchDto[]>), 200)]
    public async Task<IActionResult> GetBatches([FromQuery] int? bankAccountId, CancellationToken ct) => OkEnvelope(await imports.GetBatchesAsync(bankAccountId, ct));

    /// <summary>Undoes a whole batch while none of its lines is matched.</summary>
    [HttpPost("{batchId:long}/undo")]
    [HasPermission("BANK.IMPORT")]
    [ProducesResponseType(typeof(ApiResponse<BankImportBatchDto>), 200)]
    public async Task<IActionResult> Undo(long batchId, CancellationToken ct) => OkEnvelope(await imports.UndoAsync(batchId, ct), "Import undone.");
}

/// <summary>Bank lines and matching (build doc §9): many documents per line, and every rupee
/// of difference explained, before a line reconciles.</summary>
[Route("api/bank-lines")]
[StaffOnly]
public class BankLinesController(IBankMatchService matching) : ApiControllerBase
{
    [HttpGet]
    [HasPermission("BANK.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<BankLinesResultDto>), 200)]
    public async Task<IActionResult> GetLines([FromQuery] BankLineListRequest request, CancellationToken ct) => OkEnvelope(await matching.GetLinesAsync(request, ct));

    /// <summary>The line, ranked candidate documents and the allowed difference reasons.</summary>
    [HttpGet("{id:long}/suggestions")]
    [HasPermission("BANK.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<BankMatchContextDto>), 200)]
    public async Task<IActionResult> GetSuggestions(long id, [FromQuery] string? q, CancellationToken ct) => OkEnvelope(await matching.GetContextAsync(id, q, ct));

    /// <summary>Matches and reconciles. Refused unless applied + differences = the bank line.</summary>
    [HttpPost("{id:long}/match")]
    [HasPermission("BANK.MATCH")]
    [ProducesResponseType(typeof(ApiResponse<BankLineDto>), 200)]
    public async Task<IActionResult> Match(long id, MatchBankLineRequest request, CancellationToken ct) => OkEnvelope(await matching.MatchAsync(id, request, ct), "Matched and reconciled.");

    /// <summary>Unreconciles with a reason, reversing whatever the match created.</summary>
    [HttpPost("{id:long}/unmatch")]
    [HasPermission("BANK.MATCH")]
    [ProducesResponseType(typeof(ApiResponse<BankLineDto>), 200)]
    public async Task<IActionResult> Unmatch(long id, UnmatchBankLineRequest request, CancellationToken ct) => OkEnvelope(await matching.UnmatchAsync(id, request, ct), "Line unmatched.");
}
