using PLErpTool.Domain.Shared;

namespace PLErpTool.Domain.Account;

public sealed record ImportFileResult(
    int ExtractedRecordCount,
    int AddedRecordCount,
    int DuplicateRecordCount,
    int FilteredZeroQuantityCount);