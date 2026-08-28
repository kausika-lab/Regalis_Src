namespace Regalis.Application.Auth;

public interface IMenuQueryService
{
    /// <summary>The caller's sidebar tree: menus mapped to any of their roles,
    /// visible, ordered by DisplayOrder, nested Module → Menu → Sub-menu → Screen.</summary>
    Task<List<MenuNodeDto>> GetMyMenusAsync(CancellationToken ct = default);
}
