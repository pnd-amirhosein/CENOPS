using System.Security.Claims;

namespace NTSW_CENOPS.Application.Knowledge;

public interface IKnowledgeService
{
    Task<IReadOnlyList<KnowledgeItemModel>> ListAsync(ClaimsPrincipal actor, CancellationToken ct = default);
    Task<KnowledgeItemModel?> FindAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeAuthorModel>> AuthorsAsync(ClaimsPrincipal actor, CancellationToken ct = default);
    Task<bool> CanManageAsync(KnowledgeItemModel item, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<bool> CanChooseAuthorAsync(ClaimsPrincipal actor, CancellationToken ct = default);
    Task<KnowledgeSaveResult> SaveAsync(KnowledgeItemModel item, ClaimsPrincipal actor, CancellationToken ct = default);
    Task<KnowledgeSaveResult> DeleteAsync(int id, ClaimsPrincipal actor, CancellationToken ct = default);
}

public interface ICurlService : IKnowledgeService { }
public interface ISqlQueryService : IKnowledgeService { }
