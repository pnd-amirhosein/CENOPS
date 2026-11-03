using System.Security.Claims;
using NTSW_CENOPS.Application.Knowledge;

namespace NTSW_CENOPS.Application.Notes;

public interface INoteService
{
    Task<IReadOnlyList<NoteModel>> ListAsync(ClaimsPrincipal actor, CancellationToken ct = default);
    Task<NoteModel?> FindAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeAuthorModel>> AuthorsAsync(ClaimsPrincipal actor, CancellationToken ct = default);
    Task<bool> CanChooseAuthorAsync(ClaimsPrincipal actor, CancellationToken ct = default);
    Task<bool> CanManageAsync(NoteModel note, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<KnowledgeSaveResult> SaveAsync(NoteModel note, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<KnowledgeSaveResult> DeleteAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default);
}
